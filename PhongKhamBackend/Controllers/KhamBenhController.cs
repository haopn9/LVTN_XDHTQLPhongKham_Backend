using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Route("api/KhamBenh")]
[Authorize]
public class KhamBenhController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;

    public KhamBenhController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }

    public class CapNhatKhamBenhRequest
    {
        public int? Mach { get; set; }
        public double? NhietDo { get; set; }
        public string? HuyetAp { get; set; }
        public double? CanNang { get; set; }
        public double? ChieuCao { get; set; }
        public List<string>? ChiDinhCLSMoi { get; set; }
        public string? LoiDan { get; set; }
        public List<DonThuocItemRequest>? DonThuoc { get; set; }
        public string? KetLuan { get; set; }
        public List<string>? IcdList { get; set; }
        public int? TrangThaiKham { get; set; }
    }

    public class DonThuocItemRequest
    {
        public string MaThuoc { get; set; } = string.Empty;
        public int? SoLuong { get; set; }
        public string CachDung { get; set; } = string.Empty;
    }

    private class ThongTinDangNhap
    {
        public bool IsAdmin { get; set; }
        public bool IsBacSi { get; set; }
        public string? MaNv { get; set; }
    }

    // GET api/KhamBenh/LayDSBenhNhan
    [HttpGet("LayDSBenhNhan")]
    [Authorize(Roles = "BacSi,Admin")]
    public async Task<IActionResult> LayDanhSachBenhNhanChoKham(
        [FromQuery] string? search = null,
        [FromQuery] int? trangThai = null,
        [FromQuery] string? maBacSi = null,
        [FromQuery] string? ngayKham = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        try
        {
            var thongTinDangNhap = await LayThongTinDangNhapAsync();
            if (thongTinDangNhap == null)
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });

            if (trangThai.HasValue && !new[] { 0, 1, 2 }.Contains(trangThai.Value))
                return BadRequest(new { message = "Giá trị trạng thái không hợp lệ. Chỉ chấp nhận: 0 | 1 | 2" });

            if (page <= 0 || limit <= 0)
                return BadRequest(new { message = "Giá trị phân trang không hợp lệ" });

            if (limit > 100) limit = 100;

            DateTime ngayLoc;
            if (string.IsNullOrWhiteSpace(ngayKham))
            {
                ngayLoc = DateTime.Today;
            }
            else if (!DateTime.TryParseExact(ngayKham.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                         DateTimeStyles.None, out ngayLoc))
            {
                return BadRequest(new { message = "Định dạng ngày lọc không hợp lệ. Vui lòng nhập theo định dạng YYYY-MM-DD!" });
            }

            DateTime tuNgay = ngayLoc.Date;
            DateTime denNgay = tuNgay.AddDays(1);

            var query = _context.PhieuKhams
                .AsNoTracking()
                .Include(pk => pk.MaBnNavigation)
                .Include(pk => pk.MaNvNavigation)
                .Where(pk => pk.NgayKham >= tuNgay && pk.NgayKham < denNgay);

            if (trangThai.HasValue)
                query = query.Where(pk => pk.TrangThaiKham == trangThai.Value);
            else
                query = query.Where(pk => pk.TrangThaiKham == 0 || pk.TrangThaiKham == 1 || pk.TrangThaiKham == 2);

            if (thongTinDangNhap.IsBacSi && !thongTinDangNhap.IsAdmin)
                query = query.Where(pk => pk.MaNv == thongTinDangNhap.MaNv);
            else if (thongTinDangNhap.IsAdmin && !string.IsNullOrWhiteSpace(maBacSi))
                query = query.Where(pk => pk.MaNv == maBacSi.Trim());

            if (!string.IsNullOrWhiteSpace(search))
            {
                string tuKhoa = search.Trim().ToLower();
                query = query.Where(pk =>
                    pk.MaPhieu.ToLower().Contains(tuKhoa) ||
                    (pk.MaBn != null && pk.MaBn.ToLower().Contains(tuKhoa)) ||
                    (pk.MaBnNavigation != null && pk.MaBnNavigation.HoTen.ToLower().Contains(tuKhoa)));
            }

            int total = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)total / limit);

            var data = await query
                .OrderByDescending(pk => pk.NgayKham)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(pk => new
                {
                    maPhieu = pk.MaPhieu,
                    maBN = pk.MaBn,
                    hoTen = pk.MaBnNavigation != null ? pk.MaBnNavigation.HoTen : null,
                    gioiTinh = pk.MaBnNavigation != null ? pk.MaBnNavigation.GioiTinh : null,
                    sdt = pk.MaBnNavigation != null ? pk.MaBnNavigation.Sdt : null,
                    lyDoKham = pk.LyDoKham,
                    maBacSi = pk.MaNv,
                    tenBacSi = pk.MaNvNavigation != null ? pk.MaNvNavigation.HoTen : null,
                    ngayKham = pk.NgayKham,
                    trangThaiKham = pk.TrangThaiKham
                })
                .ToListAsync();

            return Ok(new
            {
                data = data.Select(pk => new
                {
                    pk.maPhieu,
                    pk.maBN,
                    pk.hoTen,
                    pk.gioiTinh,
                    pk.sdt,
                    pk.lyDoKham,
                    pk.maBacSi,
                    pk.tenBacSi,
                    ngayKham = pk.ngayKham?.ToString("o"),
                    pk.trangThaiKham
                }),
                pagination = new
                {
                    page,
                    limit,
                    total,
                    totalPages
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // GET api/KhamBenh/{maPhieu}
    [HttpGet("{maPhieu}")]
    [Authorize(Roles = "BacSi,Admin")]
    public async Task<IActionResult> XemChiTietPhieuKham(string maPhieu)
    {
        try
        {
            var thongTinDangNhap = await LayThongTinDangNhapAsync();
            if (thongTinDangNhap == null)
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });

            var phieuKham = await LayPhieuKhamDayDuAsync(maPhieu);
            if (phieuKham == null)
                return NotFound(new { message = "Không tìm thấy phiếu khám cần xem" });

            if (!CoQuyenTruyCapPhieu(phieuKham, thongTinDangNhap))
                return StatusCode(403, new { message = "Bạn không có quyền xem phiếu khám này" });

            return Ok(new { data = TaoResponsePhieuKham(phieuKham) });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // PUT api/KhamBenh/{maPhieu}
    [HttpPut("{maPhieu}")]
    [Authorize(Roles = "BacSi,Admin")]
    public async Task<IActionResult> CapNhatThongTinKhamBenh(string maPhieu, [FromBody] CapNhatKhamBenhRequest request)
    {
        try
        {
            var thongTinDangNhap = await LayThongTinDangNhapAsync();
            if (thongTinDangNhap == null)
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });

            var phieuKham = await _context.PhieuKhams
                .Include(pk => pk.MaIcds)
                .FirstOrDefaultAsync(pk => pk.MaPhieu == maPhieu);

            if (phieuKham == null)
                return NotFound(new { message = "Không tìm thấy phiếu khám cần cập nhật" });

            if (!CoQuyenTruyCapPhieu(phieuKham, thongTinDangNhap))
                return StatusCode(403, new { message = "Bạn không có quyền cập nhật phiếu khám này" });

            if (phieuKham.TrangThaiKham == 3 && !thongTinDangNhap.IsAdmin)
                return StatusCode(403, new { message = "Phiếu khám đã hoàn thành, không thể chỉnh sửa" });

            var loiSinhHieu = ValidateSinhHieu(request);
            if (loiSinhHieu != null)
                return BadRequest(new { message = loiSinhHieu });

            if (request.TrangThaiKham.HasValue && !new[] { 1, 2, 3 }.Contains(request.TrangThaiKham.Value))
                return BadRequest(new { message = "Giá trị trạng thái không hợp lệ. Chỉ chấp nhận: 1 | 2 | 3" });

            await using var transaction = await _context.Database.BeginTransactionAsync();

            if (request.Mach.HasValue) phieuKham.Mach = request.Mach.Value;
            if (request.NhietDo.HasValue) phieuKham.NhietDo = request.NhietDo.Value;
            if (!string.IsNullOrWhiteSpace(request.HuyetAp)) phieuKham.HuyetAp = request.HuyetAp.Trim();
            if (request.CanNang.HasValue) phieuKham.CanNang = request.CanNang.Value;
            if (request.ChieuCao.HasValue) phieuKham.ChieuCao = request.ChieuCao.Value;
            if (request.KetLuan != null) phieuKham.KetLuan = ChuanHoaChuoi(request.KetLuan);

            if (request.ChiDinhCLSMoi != null)
            {
                var maDichVuMoi = request.ChiDinhCLSMoi
                    .Where(maDv => !string.IsNullOrWhiteSpace(maDv))
                    .Select(maDv => maDv.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (string maDv in maDichVuMoi)
                {
                    bool dichVuHopLe = await _context.ChiTietDichVuYtes
                        .AnyAsync(dv => dv.MaDv == maDv && dv.TrangThai == true);
                    if (!dichVuHopLe)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = "Dịch vụ CLS không tồn tại hoặc đã ngừng cung cấp. Vui lòng kiểm tra lại" });
                    }

                    bool daChiDinh = await _context.DichVuYtes
                        .AnyAsync(dv => dv.MaPhieu == maPhieu && dv.MaDv == maDv);
                    if (daChiDinh)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = "Dịch vụ CLS này đã được chỉ định cho phiếu khám" });
                    }

                    _context.DichVuYtes.Add(new DichVuYte
                    {
                        MaPhieu = maPhieu,
                        MaDv = maDv,
                        TrangThaiDichVu = 0
                    });
                }
            }

            if (request.DonThuoc != null || request.LoiDan != null)
            {
                var donThuoc = await _context.DonThuocs
                    .Include(dt => dt.ChiTietDonThuocs)
                    .OrderBy(dt => dt.NgayKeDon)
                    .FirstOrDefaultAsync(dt => dt.MaPhieu == maPhieu);

                if (donThuoc == null)
                {
                    donThuoc = new DonThuoc
                    {
                        MaDonThuoc = await TaoMaDonThuocMoiAsync(),
                        MaPhieu = maPhieu,
                        NgayKeDon = DateTime.Now,
                        LoiDan = ChuanHoaChuoi(request.LoiDan)
                    };
                    _context.DonThuocs.Add(donThuoc);
                    await _context.SaveChangesAsync();
                }
                else if (request.LoiDan != null)
                {
                    donThuoc.LoiDan = ChuanHoaChuoi(request.LoiDan);
                }

                if (request.DonThuoc != null)
                {
                    var maThuocTrongRequest = request.DonThuoc
                        .Where(item => !string.IsNullOrWhiteSpace(item.MaThuoc))
                        .Select(item => item.MaThuoc.Trim())
                        .ToList();

                    if (maThuocTrongRequest.Count != maThuocTrongRequest.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = "Danh sách thuốc không được chứa mã thuốc trùng nhau" });
                    }

                    foreach (var item in request.DonThuoc)
                    {
                        if (string.IsNullOrWhiteSpace(item.MaThuoc))
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { message = "Thuốc không tồn tại hoặc đã ngừng sử dụng. Vui lòng kiểm tra lại" });
                        }

                        if (!item.SoLuong.HasValue || item.SoLuong.Value <= 0)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { message = "Số lượng thuốc phải là số nguyên dương. Vui lòng nhập lại" });
                        }

                        if (string.IsNullOrWhiteSpace(item.CachDung))
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { message = "Vui lòng nhập cách dùng thuốc" });
                        }

                        bool thuocHopLe = await _context.DanhMucThuocs
                            .AnyAsync(t => t.MaThuoc == item.MaThuoc.Trim() && t.IsActive);
                        if (!thuocHopLe)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { message = "Thuốc không tồn tại hoặc đã ngừng sử dụng. Vui lòng kiểm tra lại" });
                        }
                    }

                    bool trungThuocDaPhat = donThuoc.ChiTietDonThuocs
                        .Any(ct => ct.TrangThaiPhatThuoc == true &&
                                   maThuocTrongRequest.Contains(ct.MaThuoc, StringComparer.OrdinalIgnoreCase));
                    if (trungThuocDaPhat)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = "Không thể ghi đè thuốc đã phát trong đơn thuốc" });
                    }

                    var chiTietChuaPhat = donThuoc.ChiTietDonThuocs
                        .Where(ct => ct.TrangThaiPhatThuoc != true)
                        .ToList();
                    _context.ChiTietDonThuocs.RemoveRange(chiTietChuaPhat);

                    foreach (var item in request.DonThuoc)
                    {
                        _context.ChiTietDonThuocs.Add(new ChiTietDonThuoc
                        {
                            MaDonThuoc = donThuoc.MaDonThuoc,
                            MaThuoc = item.MaThuoc.Trim(),
                            SoLuong = item.SoLuong,
                            CachDung = item.CachDung.Trim(),
                            TrangThaiPhatThuoc = false
                        });
                    }
                }
            }

            if (request.IcdList != null)
            {
                var maIcdList = request.IcdList
                    .Where(maIcd => !string.IsNullOrWhiteSpace(maIcd))
                    .Select(maIcd => maIcd.Trim().ToUpper())
                    .Distinct()
                    .ToList();

                var icdEntities = await _context.DanhMucIcds
                    .Where(icd => maIcdList.Contains(icd.MaIcd))
                    .ToListAsync();

                if (icdEntities.Count != maIcdList.Count)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { message = "Mã bệnh ICD không tồn tại trong danh mục. Vui lòng kiểm tra lại" });
                }

                phieuKham.MaIcds.Clear();
                foreach (var icd in icdEntities)
                    phieuKham.MaIcds.Add(icd);
            }

            if (request.TrangThaiKham == 3 &&
                string.IsNullOrWhiteSpace(request.KetLuan ?? phieuKham.KetLuan))
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = "Vui lòng nhập Kết luận khám trước khi hoàn thành" });
            }

            if (request.TrangThaiKham.HasValue)
                phieuKham.TrangThaiKham = request.TrangThaiKham.Value;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var phieuSauCapNhat = await LayPhieuKhamDayDuAsync(maPhieu);
            return Ok(new
            {
                message = "Cập nhật thông tin khám bệnh thành công",
                data = TaoResponsePhieuKham(phieuSauCapNhat!)
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    private async Task<ThongTinDangNhap?> LayThongTinDangNhapAsync()
    {
        string? userIdClaim = User.FindFirstValue("userID");
        if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return null;

        var nhanVien = await _context.NhanViens
            .AsNoTracking()
            .FirstOrDefaultAsync(nv => nv.UserId == userId);

        bool isAdmin = User.IsInRole("Admin");
        bool isBacSi = User.IsInRole("BacSi");

        if (isBacSi && nhanVien == null)
            return null;

        return new ThongTinDangNhap
        {
            IsAdmin = isAdmin,
            IsBacSi = isBacSi,
            MaNv = nhanVien?.MaNv
        };
    }

    private static bool CoQuyenTruyCapPhieu(PhieuKham phieuKham, ThongTinDangNhap thongTinDangNhap)
    {
        if (thongTinDangNhap.IsAdmin)
            return true;

        return thongTinDangNhap.IsBacSi && phieuKham.MaNv == thongTinDangNhap.MaNv;
    }

    private async Task<PhieuKham?> LayPhieuKhamDayDuAsync(string maPhieu)
    {
        return await _context.PhieuKhams
            .AsNoTracking()
            .Include(pk => pk.MaBnNavigation)
            .Include(pk => pk.MaNvNavigation)
            .Include(pk => pk.MaIcds)
            .Include(pk => pk.DichVuYtes)
                .ThenInclude(dv => dv.MaDvNavigation)
            .Include(pk => pk.DonThuocs)
                .ThenInclude(dt => dt.ChiTietDonThuocs)
                    .ThenInclude(ct => ct.MaThuocNavigation)
            .FirstOrDefaultAsync(pk => pk.MaPhieu == maPhieu);
    }

    private static object TaoResponsePhieuKham(PhieuKham phieuKham)
    {
        var donThuoc = phieuKham.DonThuocs
            .OrderByDescending(dt => dt.NgayKeDon)
            .FirstOrDefault();

        return new
        {
            maPhieu = phieuKham.MaPhieu,
            trangThaiKham = phieuKham.TrangThaiKham,
            ngayKham = phieuKham.NgayKham?.ToString("o"),
            lyDoKham = phieuKham.LyDoKham,
            ketLuan = phieuKham.KetLuan,
            maBacSi = phieuKham.MaNv,
            tenBacSi = phieuKham.MaNvNavigation?.HoTen,
            benhNhan = new
            {
                maBN = phieuKham.MaBnNavigation?.MaBn ?? phieuKham.MaBn,
                hoTen = phieuKham.MaBnNavigation?.HoTen,
                ngaySinh = phieuKham.MaBnNavigation?.NgaySinh?.ToString("yyyy-MM-dd"),
                gioiTinh = phieuKham.MaBnNavigation?.GioiTinh,
                sdt = phieuKham.MaBnNavigation?.Sdt,
                diaChi = phieuKham.MaBnNavigation?.DiaChi,
                tienSuBenh = phieuKham.MaBnNavigation?.TienSuBenh
            },
            sinhHieu = new
            {
                mach = phieuKham.Mach,
                nhietDo = phieuKham.NhietDo,
                huyetAp = phieuKham.HuyetAp,
                canNang = phieuKham.CanNang,
                chieuCao = phieuKham.ChieuCao
            },
            icdList = phieuKham.MaIcds
                .OrderBy(icd => icd.MaIcd)
                .Select(icd => new
                {
                    maICD = icd.MaIcd,
                    tenBenh = icd.TenBenh
                })
                .ToList(),
            chiDinhCLS = phieuKham.DichVuYtes
                .OrderBy(dv => dv.MaChiTiet)
                .Select(dv => new
                {
                    maChiTiet = dv.MaChiTiet,
                    maDV = dv.MaDv,
                    tenDV = dv.MaDvNavigation?.TenDv,
                    giaTien = dv.MaDvNavigation?.GiaTien,
                    ketQua = dv.KetQua,
                    trangThaiDichVu = dv.TrangThaiDichVu
                })
                .ToList(),
            donThuoc = donThuoc == null ? new
            {
                maDonThuoc = (string?)null,
                loiDan = (string?)null,
                chiTiet = new List<object>()
            } : new
            {
                maDonThuoc = (string?)donThuoc.MaDonThuoc,
                loiDan = (string?)donThuoc.LoiDan,
                chiTiet = donThuoc.ChiTietDonThuocs
                    .OrderBy(ct => ct.MaThuoc)
                    .Select(ct => (object)new
                    {
                        maThuoc = ct.MaThuoc,
                        tenThuoc = ct.MaThuocNavigation.TenThuoc,
                        soLuong = ct.SoLuong,
                        cachDung = ct.CachDung,
                        trangThaiPhatThuoc = ct.TrangThaiPhatThuoc ?? false
                    })
                    .ToList()
            }
        };
    }

    private static string? ValidateSinhHieu(CapNhatKhamBenhRequest request)
    {
        if (request.Mach.HasValue && request.Mach.Value <= 0)
            return "Giá trị sinh hiệu không hợp lệ. Vui lòng nhập lại";

        if (request.NhietDo.HasValue && request.NhietDo.Value <= 0)
            return "Giá trị sinh hiệu không hợp lệ. Vui lòng nhập lại";

        if (request.CanNang.HasValue && request.CanNang.Value <= 0)
            return "Giá trị sinh hiệu không hợp lệ. Vui lòng nhập lại";

        if (request.ChieuCao.HasValue && request.ChieuCao.Value <= 0)
            return "Giá trị sinh hiệu không hợp lệ. Vui lòng nhập lại";

        if (!string.IsNullOrWhiteSpace(request.HuyetAp) &&
            !Regex.IsMatch(request.HuyetAp.Trim(), @"^\d{2,3}/\d{2,3}$"))
            return "Huyết áp không đúng định dạng (VD: 120/80)";

        return null;
    }

    private async Task<string> TaoMaDonThuocMoiAsync()
    {
        string today = DateTime.Now.ToString("yyMMdd");
        string prefix = $"DT{today}";
        int count = await _context.DonThuocs.CountAsync(dt => dt.MaDonThuoc.StartsWith(prefix));
        return $"{prefix}{count + 1:D3}";
    }

    private static string? ChuanHoaChuoi(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
