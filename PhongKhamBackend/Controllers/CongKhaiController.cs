using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PhongKhamBackend.Models;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

// ================================================================
// CongKhaiController — Cổng thông tin khách vãng lai
// Base Route : api/CongKhai
// Phân quyền: Anonymous — KHÔNG yêu cầu đăng nhập / token
// ================================================================
[ApiController]
[Route("api/CongKhai")]
public class CongKhaiController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;
    private readonly IMemoryCache             _cache;

    // Cache key cố định cho danh sách bác sĩ công khai
    private const string CACHE_KEY_BAC_SI = "CongKhai_DanhSachBacSi";

    public CongKhaiController(QuanLyPhongKhamDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache   = cache;
    }

    // ================================================================
    // 1. LẤY DANH SÁCH BÁC SĨ CÔNG KHAI
    // GET api/CongKhai/bac-si
    // ================================================================
    /// <summary>
    /// Trả danh sách bác sĩ đang hoạt động để hiển thị trên trang
    /// giới thiệu phòng khám (anonymous — không cần token).
    /// Chỉ trả: maNV, hoTen, chuyenMon, tenKhoa.
    /// Kết quả được cache 5 phút để giảm tải DB.
    /// </summary>
    [HttpGet("bac-si")]
    public async Task<IActionResult> LayDanhSachBacSiCongKhai()
    {
        try
        {
            // Đọc từ cache nếu còn hạn
            if (_cache.TryGetValue(CACHE_KEY_BAC_SI, out List<object>? cachedList) && cachedList != null)
                return Ok(new { data = cachedList });

            // B1: SELECT NhanVien JOIN Users JOIN DanhMucKhoa
            //     WHERE RoleId = 2 (BacSi) AND IsActive = true
            //     ORDER BY HoTen ASC
            var danhSachBacSi = await _context.NhanViens
                .AsNoTracking()
                .Include(nv => nv.User)
                .Include(nv => nv.MaKhoaNavigation)
                .Where(nv => nv.User != null
                          && nv.User.RoleId == 2
                          && nv.User.IsActive == true)
                .OrderBy(nv => nv.HoTen)
                .Select(nv => (object)new
                {
                    maNV      = nv.MaNv,
                    hoTen     = nv.HoTen,
                    chuyenMon = nv.ChuyenMon,
                    tenKhoa   = nv.MaKhoaNavigation != null ? nv.MaKhoaNavigation.TenKhoa : null
                })
                .ToListAsync();

            // Lưu cache 5 phút (danh sách bác sĩ gần như không đổi trong ngày)
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            _cache.Set(CACHE_KEY_BAC_SI, danhSachBacSi, cacheOptions);

            // B2: HTTP 200 — danh sách có thể rỗng [] (không coi là lỗi)
            return Ok(new { data = danhSachBacSi });
        }
        catch (Exception)
        {
            return StatusCode(500, new
            {
                message = "Không thể tải danh sách bác sĩ. Vui lòng thử lại sau"
            });
        }
    }

    // ================================================================
    // 2. TRA CỨU HỒ SƠ BỆNH ÁN CÔNG KHAI
    // GET api/CongKhai/tra-cuu-ho-so?maBN={maBN}&sdt={sdt}
    // ================================================================
    /// <summary>
    /// Khách vãng lai tự tra cứu hồ sơ của chính mình bằng cách cung
    /// cấp ĐỒNG THỜI mã bệnh nhân và số điện thoại đã đăng ký.
    /// Chỉ trả thông tin hành chính + phiếu khám gần nhất đã hoàn thành.
    /// Không trả: giá tiền, thông tin nhân viên tiếp đón, mã khoa nội bộ.
    /// </summary>
    [HttpGet("tra-cuu-ho-so")]
    public async Task<IActionResult> TraCuuHoSoBenhAn(
        [FromQuery] string? maBN,
        [FromQuery] string? sdt)
    {
        try
        {
            // B1: Validate — không được thiếu bất kỳ trường nào
            if (string.IsNullOrWhiteSpace(maBN) || string.IsNullOrWhiteSpace(sdt))
                return BadRequest(new
                {
                    message = "Vui lòng nhập đầy đủ Mã bệnh nhân và Số điện thoại"
                });

            // B2: Validate định dạng SĐT (10 chữ số, bắt đầu bằng 0)
            if (!Regex.IsMatch(sdt.Trim(), @"^0\d{9}$"))
                return BadRequest(new
                {
                    message = "Số điện thoại không đúng định dạng"
                });

            // B3: Tìm bệnh nhân — phải khớp CẢ maBN VÀ sdt
            var benhNhan = await _context.BenhNhans
                .AsNoTracking()
                .Where(bn => bn.MaBn == maBN.Trim() && bn.Sdt == sdt.Trim())
                .Select(bn => new
                {
                    bn.MaBn,
                    bn.HoTen,
                    bn.NgaySinh,
                    bn.GioiTinh,
                    bn.DiaChi,
                    bn.TienSuBenh
                })
                .FirstOrDefaultAsync();

            // B4a: Không khớp → 404 với thông báo chung (không lộ lý do)
            if (benhNhan == null)
                return NotFound(new
                {
                    message = "Không tìm thấy hồ sơ khớp với thông tin đã nhập"
                });

            // B5: SELECT phiếu khám gần nhất đã hoàn thành (TrangThaiKham = 3)
            var phieuKham = await _context.PhieuKhams
                .AsNoTracking()
                .Where(pk => pk.MaBn == maBN.Trim() && pk.TrangThaiKham == 3)
                .OrderByDescending(pk => pk.NgayKham)
                .FirstOrDefaultAsync();

            object? lastVisit = null;

            if (phieuKham != null)
            {
                // B6a: Lấy tên bác sĩ
                string? tenBacSi = null;
                if (!string.IsNullOrEmpty(phieuKham.MaNv))
                {
                    tenBacSi = await _context.NhanViens
                        .AsNoTracking()
                        .Where(nv => nv.MaNv == phieuKham.MaNv)
                        .Select(nv => nv.HoTen)
                        .FirstOrDefaultAsync();
                }

                // B6b: Danh sách ICD (Many-to-Many qua navigation property)
                var phieuKhamVoiIcd = await _context.PhieuKhams
                    .AsNoTracking()
                    .Include(pk => pk.MaIcds)
                    .Where(pk => pk.MaPhieu == phieuKham.MaPhieu)
                    .FirstOrDefaultAsync();

                var icdList = phieuKhamVoiIcd?.MaIcds
                    .Select(icd => new
                    {
                        maICD   = icd.MaIcd,
                        tenBenh = icd.TenBenh
                    })
                    .ToList();

                // B6c: Danh sách CLS (DichVuYte JOIN ChiTietDichVuYte)
                var clsList = await _context.DichVuYtes
                    .AsNoTracking()
                    .Include(dv => dv.MaDvNavigation)
                    .Where(dv => dv.MaPhieu == phieuKham.MaPhieu)
                    .Select(dv => new
                    {
                        tenDV           = dv.MaDvNavigation != null ? dv.MaDvNavigation.TenDv : null,
                        ketQua          = dv.KetQua,
                        trangThaiDichVu = dv.TrangThaiDichVu
                    })
                    .ToListAsync();

                // B6d: Đơn thuốc (DonThuoc → ChiTietDonThuoc → DanhMucThuoc)
                var donThuocList = await _context.DonThuocs
                    .AsNoTracking()
                    .Include(dt => dt.ChiTietDonThuocs)
                        .ThenInclude(ctdt => ctdt.MaThuocNavigation)
                    .Where(dt => dt.MaPhieu == phieuKham.MaPhieu)
                    .SelectMany(dt => dt.ChiTietDonThuocs.Select(ctdt => new
                    {
                        tenThuoc = ctdt.MaThuocNavigation != null ? ctdt.MaThuocNavigation.TenThuoc : null,
                        soLuong  = ctdt.SoLuong,
                        cachDung = ctdt.CachDung
                    }))
                    .ToListAsync();

                // B7: Gộp kết quả lastVisit
                lastVisit = new
                {
                    maPhieu  = phieuKham.MaPhieu,
                    ngayKham = phieuKham.NgayKham,
                    tenBacSi,
                    lyDoKham = phieuKham.LyDoKham,
                    mach     = phieuKham.Mach,
                    nhietDo  = phieuKham.NhietDo,
                    huyetAp  = phieuKham.HuyetAp,
                    canNang  = phieuKham.CanNang,
                    chieuCao = phieuKham.ChieuCao,
                    ketLuan  = phieuKham.KetLuan,
                    icdList,
                    clsList,
                    donThuoc = donThuocList
                };
            }

            // Trả HTTP 200
            return Ok(new
            {
                patient = new
                {
                    maBN       = benhNhan.MaBn,
                    hoTen      = benhNhan.HoTen,
                    ngaySinh   = benhNhan.NgaySinh,
                    gioiTinh   = benhNhan.GioiTinh,
                    diaChi     = benhNhan.DiaChi,
                    tienSuBenh = benhNhan.TienSuBenh
                },
                lastVisit
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new
            {
                message = "Không thể tra cứu dữ liệu. Vui lòng thử lại sau"
            });
        }
    }
}
