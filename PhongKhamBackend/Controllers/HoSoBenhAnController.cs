using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Route("api/HoSoBenhAn")]
[Authorize]
public class HoSoBenhAnController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;

    public HoSoBenhAnController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }

    // GET api/HoSoBenhAn/tra-cuu?query={tuKhoa}
    [HttpGet("tra-cuu")]
    [Authorize(Roles = "BacSi,Admin")]
    public async Task<IActionResult> TraCuuHoSoBenhAn([FromQuery] string? query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(new { message = "Vui lòng nhập mã bệnh nhân hoặc mã hồ sơ khám bệnh để tra cứu!" });

            string tuKhoa = query.Trim();

            var phieuKham = await _context.PhieuKhams
                .AsNoTracking()
                .Include(pk => pk.MaBnNavigation)
                .Where(pk => pk.MaBnNavigation != null &&
                             (pk.MaPhieu == tuKhoa || pk.MaBn == tuKhoa))
                .OrderByDescending(pk => pk.NgayKham)
                .FirstOrDefaultAsync();

            if (phieuKham == null)
            {
                phieuKham = await _context.PhieuKhams
                    .AsNoTracking()
                    .Include(pk => pk.MaBnNavigation)
                    .Where(pk => pk.MaBnNavigation != null &&
                                 (pk.MaPhieu.Contains(tuKhoa) ||
                                  (pk.MaBn != null && pk.MaBn.Contains(tuKhoa)) ||
                                  pk.MaBnNavigation.HoTen.Contains(tuKhoa) ||
                                  (pk.MaBnNavigation.Sdt != null && pk.MaBnNavigation.Sdt.Contains(tuKhoa))))
                    .OrderByDescending(pk => pk.NgayKham)
                    .FirstOrDefaultAsync();
            }

            if (phieuKham?.MaBnNavigation == null)
            {
                return NotFound(new
                {
                    found = false,
                    message = "Không tìm thấy thông tin bệnh nhân nào trùng khớp!"
                });
            }

            return Ok(new
            {
                found = true,
                data = TaoThongTinBenhNhan(phieuKham.MaBnNavigation)
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // GET api/HoSoBenhAn/gan-day
    [HttpGet("gan-day")]
    [Authorize(Roles = "BacSi,Admin")]
    public async Task<IActionResult> LayDanhSachBenhNhanGanDay()
    {
        try
        {
            var danhSachGanDay = await _context.PhieuKhams
                .AsNoTracking()
                .Where(pk => pk.MaBn != null)
                .GroupBy(pk => pk.MaBn!)
                .Select(group => new
                {
                    MaBn = group.Key,
                    LastVisit = group.Max(pk => pk.NgayKham)
                })
                .OrderByDescending(item => item.LastVisit)
                .Take(5)
                .Join(
                    _context.BenhNhans.AsNoTracking(),
                    item => item.MaBn,
                    benhNhan => benhNhan.MaBn,
                    (item, benhNhan) => new
                    {
                        BenhNhan = benhNhan,
                        item.LastVisit
                    })
                .ToListAsync();

            return Ok(new
            {
                data = danhSachGanDay.Select(item => new
                {
                    maBN = item.BenhNhan.MaBn,
                    hoTen = item.BenhNhan.HoTen,
                    ngaySinh = item.BenhNhan.NgaySinh?.ToString("yyyy-MM-dd"),
                    gioiTinh = item.BenhNhan.GioiTinh,
                    sdt = item.BenhNhan.Sdt,
                    diaChi = item.BenhNhan.DiaChi,
                    tienSuBenh = item.BenhNhan.TienSuBenh,
                    lastVisit = item.LastVisit?.ToString("o")
                })
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // GET api/HoSoBenhAn/{maBN}/lich-su
    [HttpGet("{maBN}/lich-su")]
    [Authorize(Roles = "BacSi,Admin")]
    public async Task<IActionResult> LayLichSuKhamBenh(string maBN)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(maBN))
                return BadRequest(new { message = "Vui lòng nhập mã bệnh nhân!" });

            string maBenhNhan = maBN.Trim();
            bool benhNhanTonTai = await _context.BenhNhans
                .AsNoTracking()
                .AnyAsync(bn => bn.MaBn == maBenhNhan);

            if (!benhNhanTonTai)
                return NotFound(new { message = "Không tìm thấy hồ sơ bệnh nhân" });

            var lichSuKham = await _context.PhieuKhams
                .AsNoTracking()
                .Include(pk => pk.MaNvNavigation)
                .Include(pk => pk.MaIcds)
                .Where(pk => pk.MaBn == maBenhNhan)
                .OrderByDescending(pk => pk.NgayKham)
                .Select(pk => new
                {
                    maPhieu = pk.MaPhieu,
                    ngayKham = pk.NgayKham,
                    trangThaiKham = pk.TrangThaiKham,
                    tenBacSi = pk.MaNvNavigation != null ? pk.MaNvNavigation.HoTen : null,
                    ketLuan = pk.KetLuan,
                    icdList = pk.MaIcds
                        .OrderBy(icd => icd.MaIcd)
                        .Select(icd => new
                        {
                            maICD = icd.MaIcd,
                            tenBenh = icd.TenBenh
                        })
                        .ToList()
                })
                .ToListAsync();

            return Ok(new
            {
                data = lichSuKham.Select(pk => new
                {
                    pk.maPhieu,
                    ngayKham = pk.ngayKham?.ToString("o"),
                    pk.trangThaiKham,
                    pk.tenBacSi,
                    pk.ketLuan,
                    pk.icdList
                })
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    private static object TaoThongTinBenhNhan(BenhNhan benhNhan)
    {
        return new
        {
            maBN = benhNhan.MaBn,
            hoTen = benhNhan.HoTen,
            ngaySinh = benhNhan.NgaySinh?.ToString("yyyy-MM-dd"),
            gioiTinh = benhNhan.GioiTinh,
            sdt = benhNhan.Sdt,
            diaChi = benhNhan.DiaChi,
            tienSuBenh = benhNhan.TienSuBenh
        };
    }
}
