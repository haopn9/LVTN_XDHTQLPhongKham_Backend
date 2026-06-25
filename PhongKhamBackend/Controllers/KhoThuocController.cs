using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Authorize]
public class KhoThuocController : ControllerBase
{
    private static readonly HashSet<string> TrangThaiHanSuDungHopLe = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tất cả",
        "An toàn",
        "Hạn ngắn (<6 th)",
        "Đã hết hạn"
    };

    private readonly QuanLyPhongKhamDbContext _context;

    public KhoThuocController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }

    public class ThemLoThuocRequest
    {
        public string MaLo { get; set; } = string.Empty;
        public string MaThuoc { get; set; } = string.Empty;

        [JsonPropertyName("maNCC")]
        public int? MaNCC { get; set; }

        public int? SoLuongNhap { get; set; }
        public int? SoLuongTon { get; set; }
        public decimal? GiaNhap { get; set; }
        public decimal? GiaBan { get; set; }
        public string? NgaySanXuat { get; set; }
        public string? HanSuDung { get; set; }
    }

    public class CapNhatLoThuocRequest
    {
        public string MaThuoc { get; set; } = string.Empty;

        [JsonPropertyName("maNCC")]
        public int? MaNCC { get; set; }

        public int? SoLuongNhap { get; set; }
        public int? SoLuongTon { get; set; }
        public decimal? GiaNhap { get; set; }
        public decimal? GiaBan { get; set; }
        public string? NgaySanXuat { get; set; }
        public string? HanSuDung { get; set; }
    }

    private class ThongTinLoThuocHopLe
    {
        public string MaThuoc { get; set; } = string.Empty;
        public int MaNCC { get; set; }
        public int SoLuongNhap { get; set; }
        public int SoLuongTon { get; set; }
        public decimal GiaNhap { get; set; }
        public decimal GiaBan { get; set; }
        public DateOnly? NgaySanXuat { get; set; }
        public DateOnly HanSuDung { get; set; }
    }

    // ================================================================
    // LAY DANH SACH LO THUOC NHAP KHO
    // GET api/KhoThuoc
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpGet("api/KhoThuoc")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> LayDanhSachLoThuoc(
        [FromQuery] string? maLo = null,
        [FromQuery] string? tenThuoc = null,
        [FromQuery] string? tenNCC = null,
        [FromQuery] string? hanSuDung = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            if (page <= 0 || pageSize <= 0)
                return BadRequest(new { message = "Giá trị phân trang không hợp lệ" });

            if (pageSize > 100) pageSize = 100;

            var query = _context.LoThuocs
                .Include(lo => lo.MaThuocNavigation)
                .Include(lo => lo.MaNccNavigation)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(maLo))
            {
                string maLoTrim = maLo.Trim().ToLower();
                query = query.Where(lo => lo.MaLo.ToLower().Contains(maLoTrim));
            }

            if (!string.IsNullOrWhiteSpace(tenThuoc))
            {
                string tenThuocTrim = tenThuoc.Trim().ToLower();
                query = query.Where(lo => lo.MaThuocNavigation != null
                    && lo.MaThuocNavigation.TenThuoc.ToLower().Contains(tenThuocTrim));
            }

            if (!string.IsNullOrWhiteSpace(tenNCC))
            {
                string tenNCCTrim = tenNCC.Trim().ToLower();
                query = query.Where(lo => lo.MaNccNavigation != null
                    && lo.MaNccNavigation.TenNcc.ToLower().Contains(tenNCCTrim));
            }

            if (!string.IsNullOrWhiteSpace(hanSuDung))
            {
                string hanSuDungTrim = hanSuDung.Trim();
                if (!TrangThaiHanSuDungHopLe.Contains(hanSuDungTrim))
                    return BadRequest(new { message = "Trạng thái hạn sử dụng không hợp lệ" });

                if (!hanSuDungTrim.Equals("Tất cả", StringComparison.OrdinalIgnoreCase))
                {
                    DateOnly today = DateOnly.FromDateTime(DateTime.Today);
                    DateOnly mocHanNgan = today.AddDays(180);

                    if (hanSuDungTrim.Equals("An toàn", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(lo => lo.HanSuDung.HasValue && lo.HanSuDung.Value >= mocHanNgan);
                    }
                    else if (hanSuDungTrim.Equals("Hạn ngắn (<6 th)", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(lo => lo.HanSuDung.HasValue
                            && lo.HanSuDung.Value > today
                            && lo.HanSuDung.Value < mocHanNgan);
                    }
                    else if (hanSuDungTrim.Equals("Đã hết hạn", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(lo => lo.HanSuDung.HasValue && lo.HanSuDung.Value <= today);
                    }
                }
            }

            int total = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)total / pageSize);

            var danhSach = await query
                .OrderBy(lo => lo.MaLo)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(lo => new
                {
                    lo.MaLo,
                    lo.MaThuoc,
                    TenThuoc = lo.MaThuocNavigation != null ? lo.MaThuocNavigation.TenThuoc : null,
                    MaNCC = lo.MaNcc,
                    TenNCC = lo.MaNccNavigation != null ? lo.MaNccNavigation.TenNcc : null,
                    lo.SoLuongNhap,
                    lo.SoLuongTon,
                    lo.GiaNhap,
                    lo.GiaBan,
                    lo.NgaySanXuat,
                    lo.HanSuDung
                })
                .ToListAsync();

            var data = danhSach.Select((item, index) => new
            {
                stt = (page - 1) * pageSize + index + 1,
                maLo = item.MaLo,
                maThuoc = item.MaThuoc,
                tenThuoc = item.TenThuoc,
                maNCC = item.MaNCC,
                tenNCC = item.TenNCC,
                soLuongNhap = item.SoLuongNhap,
                soLuongTon = item.SoLuongTon,
                giaNhap = item.GiaNhap,
                giaBan = item.GiaBan,
                ngaySanXuat = item.NgaySanXuat,
                hanSuDung = item.HanSuDung,
                trangThaiHSD = TinhTrangThaiHanSuDung(item.HanSuDung)
            });

            return Ok(new
            {
                data,
                total,
                page,
                pageSize,
                totalPages
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ================================================================
    // THEM LO THUOC NHAP KHO
    // POST api/KhoThuoc
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpPost("api/KhoThuoc")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> ThemLoThuoc([FromBody] ThemLoThuocRequest request)
    {
        try
        {
            var validationResult = ValidateThemLoThuocRequest(request, out ThongTinLoThuocHopLe thongTin);
            if (validationResult != null) return validationResult;

            string maLoUpper = request.MaLo.Trim().ToUpperInvariant();

            bool maLoExists = await _context.LoThuocs.AnyAsync(lo => lo.MaLo == maLoUpper);
            if (maLoExists)
                return Conflict(new { message = "Mã lô thuốc đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại mã khác" });

            var thuoc = await _context.DanhMucThuocs.FindAsync(thongTin.MaThuoc);
            if (thuoc == null)
                return NotFound(new { message = "Mã thuốc không tồn tại trong hệ thống. Vui lòng kiểm tra lại" });

            var nhaCungCap = await _context.NhaCungCaps.FindAsync(thongTin.MaNCC);
            if (nhaCungCap == null)
                return NotFound(new { message = "Mã nhà cung cấp không tồn tại trong hệ thống. Vui lòng kiểm tra lại" });

            var loThuoc = new LoThuoc
            {
                MaLo = maLoUpper,
                MaThuoc = thongTin.MaThuoc,
                MaNcc = thongTin.MaNCC,
                SoLuongNhap = thongTin.SoLuongNhap,
                SoLuongTon = thongTin.SoLuongTon,
                GiaNhap = thongTin.GiaNhap,
                GiaBan = thongTin.GiaBan,
                NgaySanXuat = thongTin.NgaySanXuat,
                HanSuDung = thongTin.HanSuDung
            };

            _context.LoThuocs.Add(loThuoc);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Thêm mới lô thuốc nhập kho thành công",
                data = ToLoThuocResponse(loThuoc, thuoc, nhaCungCap)
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ================================================================
    // SUA THONG TIN LO THUOC NHAP KHO
    // PUT api/KhoThuoc/{maLo}
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpPut("api/KhoThuoc/{maLo}")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> CapNhatLoThuoc(string maLo, [FromBody] CapNhatLoThuocRequest request)
    {
        try
        {
            string maLoUpper = maLo.Trim().ToUpperInvariant();
            var loThuoc = await _context.LoThuocs.FindAsync(maLoUpper);
            if (loThuoc == null)
                return NotFound(new { message = "Không tìm thấy thông tin lô thuốc cần cập nhật" });

            var validationResult = ValidateCapNhatLoThuocRequest(request, out ThongTinLoThuocHopLe thongTin);
            if (validationResult != null) return validationResult;

            var thuoc = await _context.DanhMucThuocs.FindAsync(thongTin.MaThuoc);
            if (thuoc == null)
                return NotFound(new { message = "Mã thuốc không tồn tại trong hệ thống. Vui lòng kiểm tra lại" });

            var nhaCungCap = await _context.NhaCungCaps.FindAsync(thongTin.MaNCC);
            if (nhaCungCap == null)
                return NotFound(new { message = "Mã nhà cung cấp không tồn tại trong hệ thống. Vui lòng kiểm tra lại" });

            loThuoc.MaThuoc = thongTin.MaThuoc;
            loThuoc.MaNcc = thongTin.MaNCC;
            loThuoc.SoLuongNhap = thongTin.SoLuongNhap;
            loThuoc.SoLuongTon = thongTin.SoLuongTon;
            loThuoc.GiaNhap = thongTin.GiaNhap;
            loThuoc.GiaBan = thongTin.GiaBan;
            loThuoc.NgaySanXuat = thongTin.NgaySanXuat;
            loThuoc.HanSuDung = thongTin.HanSuDung;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật thông tin lô thuốc thành công",
                data = ToLoThuocResponse(loThuoc, thuoc, nhaCungCap)
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ================================================================
    // XOA LO THUOC NHAP KHO (HARD DELETE)
    // DELETE api/KhoThuoc/{maLo}
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpDelete("api/KhoThuoc/{maLo}")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> XoaLoThuoc(string maLo)
    {
        try
        {
            string maLoUpper = maLo.Trim().ToUpperInvariant();
            var loThuoc = await _context.LoThuocs
                .Include(lo => lo.MaThuocNavigation)
                .Include(lo => lo.MaNccNavigation)
                .FirstOrDefaultAsync(lo => lo.MaLo == maLoUpper);

            if (loThuoc == null)
                return NotFound(new { message = "Không tìm thấy lô thuốc cần xóa" });

            var data = ToLoThuocResponse(loThuoc, loThuoc.MaThuocNavigation, loThuoc.MaNccNavigation);

            _context.LoThuocs.Remove(loThuoc);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Xóa lô thuốc thành công",
                data
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể thực hiện thao tác. Xin hãy thử lại" });
        }
    }

    private IActionResult? ValidateThemLoThuocRequest(ThemLoThuocRequest request, out ThongTinLoThuocHopLe thongTin)
    {
        thongTin = new ThongTinLoThuocHopLe();

        if (request == null)
            return BadRequest(new { message = "Người dùng bắt buộc nhập đủ các dữ liệu cần thiết" });

        if (string.IsNullOrWhiteSpace(request.MaLo))
            return BadRequest(new { message = "Vui lòng nhập mã lô thuốc" });

        if (Regex.IsMatch(request.MaLo, @"\s"))
            return BadRequest(new { message = "Mã lô thuốc không được chứa khoảng trắng. Vui lòng nhập lại" });

        if (request.MaLo.Trim().Length > 10)
            return BadRequest(new { message = "Mã lô thuốc không được vượt quá 10 ký tự. Vui lòng nhập lại" });

        if (!Regex.IsMatch(request.MaLo.Trim(), @"^[A-Za-z0-9]+$"))
            return BadRequest(new { message = "Mã lô thuốc không được chứa ký tự đặc biệt. Vui lòng nhập lại" });

        return ValidateThongTinLoThuoc(
            request.MaThuoc,
            request.MaNCC,
            request.SoLuongNhap,
            request.SoLuongTon,
            request.GiaNhap,
            request.GiaBan,
            request.NgaySanXuat,
            request.HanSuDung,
            out thongTin);
    }

    private IActionResult? ValidateCapNhatLoThuocRequest(CapNhatLoThuocRequest request, out ThongTinLoThuocHopLe thongTin)
    {
        thongTin = new ThongTinLoThuocHopLe();

        if (request == null)
            return BadRequest(new { message = "Người dùng bắt buộc nhập đủ các dữ liệu cần thiết" });

        return ValidateThongTinLoThuoc(
            request.MaThuoc,
            request.MaNCC,
            request.SoLuongNhap,
            request.SoLuongTon,
            request.GiaNhap,
            request.GiaBan,
            request.NgaySanXuat,
            request.HanSuDung,
            out thongTin);
    }

    private IActionResult? ValidateThongTinLoThuoc(
        string maThuoc,
        int? maNCC,
        int? soLuongNhap,
        int? soLuongTon,
        decimal? giaNhap,
        decimal? giaBan,
        string? ngaySanXuatText,
        string? hanSuDungText,
        out ThongTinLoThuocHopLe thongTin)
    {
        thongTin = new ThongTinLoThuocHopLe();

        if (string.IsNullOrWhiteSpace(maThuoc))
            return BadRequest(new { message = "Vui lòng nhập mã thuốc" });

        if (!maNCC.HasValue)
            return BadRequest(new { message = "Vui lòng nhập mã nhà cung cấp" });

        if (!soLuongNhap.HasValue)
            return BadRequest(new { message = "Vui lòng nhập số lượng nhập của lô thuốc" });

        if (!soLuongTon.HasValue)
            return BadRequest(new { message = "Vui lòng nhập số lượng tồn kho của lô thuốc" });

        if (!giaNhap.HasValue)
            return BadRequest(new { message = "Vui lòng nhập giá nhập của lô thuốc" });

        if (!giaBan.HasValue)
            return BadRequest(new { message = "Vui lòng  nhập giá bán niêm yết của lô thuốc" });

        if (string.IsNullOrWhiteSpace(hanSuDungText))
            return BadRequest(new { message = "Vui lòng  nhập hạn sử dụng của lô thuốc" });

        if (soLuongNhap.Value < 0)
            return BadRequest(new { message = "Số lượng nhập của lô thuốc phải là 1 số nguyên dương. Vui lòng nhập lại" });

        if (soLuongTon.Value < 0)
            return BadRequest(new { message = "Số lượng tồn kho của lô thuốc phải là 1 số nguyên dương. Vui lòng nhập lại" });

        if (soLuongTon.Value > soLuongNhap.Value)
            return BadRequest(new { message = "Số lượng tồn kho không được lớn hơn số lượng nhập về của lô thuốc. Vui lòng nhập lại" });

        if (giaNhap.Value < 0)
            return BadRequest(new { message = "Gía nhập về của lô thuốc phải là 1 số dương. Vui lòng nhập lại" });

        if (giaBan.Value < 0)
            return BadRequest(new { message = " Gía bán niêm yết của lô thuốc phải là 1 số dương. Vui lòng nhập lại" });

        if (VuotQuaGioiHanDecimal18Phan2(giaNhap.Value))
            return BadRequest(new { message = "Giá nhập về của lô thuốc không được vượt quá 16 chữ số phần nguyên. Vui lòng nhập lại" });

        if (VuotQuaGioiHanDecimal18Phan2(giaBan.Value))
            return BadRequest(new { message = "Giá bán niêm yết của lô thuốc không được vượt quá 16 chữ số phần nguyên. Vui lòng nhập lại" });

        DateOnly? ngaySanXuat = null;
        if (!string.IsNullOrWhiteSpace(ngaySanXuatText)
            && !TryParseNgay(ngaySanXuatText, out ngaySanXuat))
            return BadRequest(new { message = "Ngày sản xuất không hợp lệ. Vui lòng nhập đúng định dạng dd/MM/yyyy" });

        if (!TryParseNgay(hanSuDungText, out DateOnly? hanSuDung) || !hanSuDung.HasValue)
            return BadRequest(new { message = "Hạn sử dụng không hợp lệ. Vui lòng nhập đúng định dạng dd/MM/yyyy" });

        if (ngaySanXuat.HasValue && hanSuDung.Value < ngaySanXuat.Value)
            return BadRequest(new { message = "Hạn sử dụng phải lớn hơn hoặc bằng ngày sản xuất của lô thuốc. Vui lòng nhập lại" });

        thongTin = new ThongTinLoThuocHopLe
        {
            MaThuoc = maThuoc.Trim().ToUpperInvariant(),
            MaNCC = maNCC.Value,
            SoLuongNhap = soLuongNhap.Value,
            SoLuongTon = soLuongTon.Value,
            GiaNhap = giaNhap.Value,
            GiaBan = giaBan.Value,
            NgaySanXuat = ngaySanXuat,
            HanSuDung = hanSuDung.Value
        };

        return null;
    }

    private static bool TryParseNgay(string? value, out DateOnly? ngay)
    {
        ngay = null;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        string[] formats = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
        if (!DateOnly.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
            return false;

        ngay = parsed;
        return true;
    }

    private static bool VuotQuaGioiHanDecimal18Phan2(decimal value)
    {
        return decimal.Truncate(Math.Abs(value)) > 9999999999999999m;
    }

    private static string TinhTrangThaiHanSuDung(DateOnly? hanSuDung)
    {
        if (!hanSuDung.HasValue)
            return "Không xác định";

        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        int soNgayConLai = hanSuDung.Value.DayNumber - today.DayNumber;

        if (soNgayConLai >= 180)
            return "An toàn";

        if (soNgayConLai > 0)
            return "Hạn ngắn (<6 th)";

        return "Đã hết hạn";
    }

    private static object ToLoThuocResponse(LoThuoc loThuoc, DanhMucThuoc? thuoc, NhaCungCap? nhaCungCap)
    {
        return new
        {
            maLo = loThuoc.MaLo,
            maThuoc = loThuoc.MaThuoc,
            tenThuoc = thuoc?.TenThuoc,
            maNCC = loThuoc.MaNcc,
            tenNCC = nhaCungCap?.TenNcc,
            soLuongNhap = loThuoc.SoLuongNhap,
            soLuongTon = loThuoc.SoLuongTon,
            giaNhap = loThuoc.GiaNhap,
            giaBan = loThuoc.GiaBan,
            ngaySanXuat = loThuoc.NgaySanXuat,
            hanSuDung = loThuoc.HanSuDung
        };
    }
}
