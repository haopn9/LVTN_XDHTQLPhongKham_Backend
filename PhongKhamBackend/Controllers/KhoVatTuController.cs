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
public class KhoVatTuController : ControllerBase
{
    private static readonly HashSet<string> TrangThaiHanSuDungHopLe = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tất cả",
        "An toàn",
        "Hạn ngắn (<6 th)",
        "Đã hết hạn"
    };

    private readonly QuanLyPhongKhamDbContext _context;

    public KhoVatTuController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }

    public class ThemLoVatTuRequest
    {
        public string MaLo { get; set; } = string.Empty;
        public string MaVatTu { get; set; } = string.Empty;

        [JsonPropertyName("maNCC")]
        public int? MaNCC { get; set; }

        public int? SoLuongNhap { get; set; }
        public int? SoLuongTon { get; set; }
        public decimal? GiaNhap { get; set; }
        public decimal? GiaBan { get; set; }
        public string? NgaySanXuat { get; set; }
        public string? HanSuDung { get; set; }
    }

    public class CapNhatLoVatTuRequest
    {
        public string MaVatTu { get; set; } = string.Empty;

        [JsonPropertyName("maNCC")]
        public int? MaNCC { get; set; }

        public int? SoLuongNhap { get; set; }
        public int? SoLuongTon { get; set; }
        public decimal? GiaNhap { get; set; }
        public decimal? GiaBan { get; set; }
        public string? NgaySanXuat { get; set; }
        public string? HanSuDung { get; set; }
    }

    private class ThongTinLoVatTuHopLe
    {
        public string MaVatTu { get; set; } = string.Empty;
        public int MaNCC { get; set; }
        public int SoLuongNhap { get; set; }
        public int SoLuongTon { get; set; }
        public decimal GiaNhap { get; set; }
        public decimal GiaBan { get; set; }
        public DateOnly? NgaySanXuat { get; set; }
        public DateOnly HanSuDung { get; set; }
    }

    // ================================================================
    // LAY DANH SACH LO VAT TU NHAP KHO
    // GET api/KhoVatTu
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpGet("api/KhoVatTu")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> LayDanhSachLoVatTu(
        [FromQuery] string? maLo = null,
        [FromQuery] string? tenVatTu = null,
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

            var query = _context.LoVatTus
                .Include(lo => lo.MaVatTuNavigation)
                .Include(lo => lo.MaNccNavigation)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(maLo))
            {
                string maLoTrim = maLo.Trim().ToLower();
                query = query.Where(lo => lo.MaLo.ToLower().Contains(maLoTrim));
            }

            if (!string.IsNullOrWhiteSpace(tenVatTu))
            {
                string tenVatTuTrim = tenVatTu.Trim().ToLower();
                query = query.Where(lo => lo.MaVatTuNavigation != null
                    && lo.MaVatTuNavigation.TenVatTu.ToLower().Contains(tenVatTuTrim));
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
                        query = query.Where(lo => lo.HanSuDung >= mocHanNgan);
                    }
                    else if (hanSuDungTrim.Equals("Hạn ngắn (<6 th)", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(lo => lo.HanSuDung > today && lo.HanSuDung < mocHanNgan);
                    }
                    else if (hanSuDungTrim.Equals("Đã hết hạn", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(lo => lo.HanSuDung <= today);
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
                    lo.MaVatTu,
                    TenVatTu = lo.MaVatTuNavigation != null ? lo.MaVatTuNavigation.TenVatTu : null,
                    MaNCC = lo.MaNcc,
                    TenNCC = lo.MaNccNavigation != null ? lo.MaNccNavigation.TenNcc : null,
                    lo.SoLuongNhap,
                    lo.SoLuongTon,
                    lo.GiaBan,
                    lo.HanSuDung
                })
                .ToListAsync();

            var data = danhSach.Select((item, index) => new
            {
                stt = (page - 1) * pageSize + index + 1,
                maLo = item.MaLo,
                maVatTu = item.MaVatTu,
                tenVatTu = item.TenVatTu,
                maNCC = item.MaNCC,
                tenNCC = item.TenNCC,
                soLuongNhap = item.SoLuongNhap,
                soLuongTon = item.SoLuongTon,
                giaBan = item.GiaBan,
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
    // THEM LO VAT TU NHAP KHO
    // POST api/KhoVatTu
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpPost("api/KhoVatTu")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> ThemLoVatTu([FromBody] ThemLoVatTuRequest request)
    {
        try
        {
            var validationResult = ValidateThemLoVatTuRequest(request, out ThongTinLoVatTuHopLe thongTin);
            if (validationResult != null) return validationResult;

            string maLoUpper = request.MaLo.Trim().ToUpperInvariant();

            bool maLoExists = await _context.LoVatTus.AnyAsync(lo => lo.MaLo == maLoUpper);
            if (maLoExists)
                return Conflict(new { message = "Mã lô vật tư đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại mã khác" });

            var vatTu = await _context.DanhMucVatTus.FindAsync(thongTin.MaVatTu);
            if (vatTu == null)
                return NotFound(new { message = "Không tìm thấy danh mục vật tư tương ứng" });

            var nhaCungCap = await _context.NhaCungCaps.FindAsync(thongTin.MaNCC);
            if (nhaCungCap == null)
                return NotFound(new { message = "Không tìm thấy nhà cung cấp tương ứng" });

            var loVatTu = new LoVatTu
            {
                MaLo = maLoUpper,
                MaVatTu = thongTin.MaVatTu,
                MaNcc = thongTin.MaNCC,
                SoLuongNhap = thongTin.SoLuongNhap,
                SoLuongTon = thongTin.SoLuongTon,
                GiaNhap = thongTin.GiaNhap,
                GiaBan = thongTin.GiaBan,
                NgaySanXuat = thongTin.NgaySanXuat,
                HanSuDung = thongTin.HanSuDung
            };

            _context.LoVatTus.Add(loVatTu);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Thêm mới lô vật tư nhập kho thành công",
                data = ToLoVatTuResponse(loVatTu, vatTu, nhaCungCap)
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ================================================================
    // SUA THONG TIN LO VAT TU NHAP KHO
    // PUT api/KhoVatTu/{maLo}
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpPut("api/KhoVatTu/{maLo}")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> CapNhatLoVatTu(string maLo, [FromBody] CapNhatLoVatTuRequest request)
    {
        try
        {
            string maLoUpper = maLo.Trim().ToUpperInvariant();
            var loVatTu = await _context.LoVatTus.FindAsync(maLoUpper);
            if (loVatTu == null)
                return NotFound(new { message = "Không tìm thấy thông tin lô vật tư cần cập nhật" });

            var validationResult = ValidateCapNhatLoVatTuRequest(request, out ThongTinLoVatTuHopLe thongTin);
            if (validationResult != null) return validationResult;

            var vatTu = await _context.DanhMucVatTus.FindAsync(thongTin.MaVatTu);
            if (vatTu == null)
                return NotFound(new { message = "Không tìm thấy danh mục vật tư tương ứng" });

            var nhaCungCap = await _context.NhaCungCaps.FindAsync(thongTin.MaNCC);
            if (nhaCungCap == null)
                return NotFound(new { message = "Không tìm thấy nhà cung cấp tương ứng" });

            loVatTu.MaVatTu = thongTin.MaVatTu;
            loVatTu.MaNcc = thongTin.MaNCC;
            loVatTu.SoLuongNhap = thongTin.SoLuongNhap;
            loVatTu.SoLuongTon = thongTin.SoLuongTon;
            loVatTu.GiaNhap = thongTin.GiaNhap;
            loVatTu.GiaBan = thongTin.GiaBan;
            loVatTu.NgaySanXuat = thongTin.NgaySanXuat;
            loVatTu.HanSuDung = thongTin.HanSuDung;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật thông tin lô vật tư thành công",
                data = ToLoVatTuResponse(loVatTu, vatTu, nhaCungCap)
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ================================================================
    // XOA LO VAT TU NHAP KHO (HARD DELETE)
    // DELETE api/KhoVatTu/{maLo}
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpDelete("api/KhoVatTu/{maLo}")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> XoaLoVatTu(string maLo)
    {
        try
        {
            string maLoUpper = maLo.Trim().ToUpperInvariant();
            var loVatTu = await _context.LoVatTus
                .Include(lo => lo.MaVatTuNavigation)
                .Include(lo => lo.MaNccNavigation)
                .FirstOrDefaultAsync(lo => lo.MaLo == maLoUpper);

            if (loVatTu == null)
                return NotFound(new { message = "Không tìm thấy lô vật tư cần xóa" });

            // Kiem tra lo da tung duoc su dung de ke vat tu cho benh nhan (SoLuongTon < SoLuongNhap)
            if (loVatTu.SoLuongTon < loVatTu.SoLuongNhap)
                return Conflict(new { message = "Lô vật tư này đã được sử dụng để kê cho bệnh nhân, không thể xóa" });

            var data = ToLoVatTuResponse(loVatTu, loVatTu.MaVatTuNavigation, loVatTu.MaNccNavigation);

            _context.LoVatTus.Remove(loVatTu);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Xóa lô vật tư thành công",
                data
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể thực hiện thao tác. Xin hãy thử lại" });
        }
    }

    // ================================================================
    // VALIDATION HELPERS
    // ================================================================

    private IActionResult? ValidateThemLoVatTuRequest(ThemLoVatTuRequest request, out ThongTinLoVatTuHopLe thongTin)
    {
        thongTin = new ThongTinLoVatTuHopLe();

        if (request == null)
            return BadRequest(new { message = "Người dùng bắt buộc nhập đủ các dữ liệu cần thiết" });

        if (string.IsNullOrWhiteSpace(request.MaLo))
            return BadRequest(new { message = "Vui lòng nhập mã lô vật tư" });

        if (Regex.IsMatch(request.MaLo, @"\s"))
            return BadRequest(new { message = "Mã lô vật tư không được chứa khoảng trắng. Vui lòng nhập lại" });

        if (request.MaLo.Trim().Length > 10)
            return BadRequest(new { message = "Mã lô vật tư không được vượt quá 10 ký tự. Vui lòng nhập lại" });

        if (!Regex.IsMatch(request.MaLo.Trim(), @"^[A-Za-z0-9]+$"))
            return BadRequest(new { message = "Mã lô vật tư không được chứa ký tự đặc biệt. Vui lòng nhập lại" });

        return ValidateThongTinLoVatTu(
            request.MaVatTu,
            request.MaNCC,
            request.SoLuongNhap,
            request.SoLuongTon,
            request.GiaNhap,
            request.GiaBan,
            request.NgaySanXuat,
            request.HanSuDung,
            out thongTin);
    }

    private IActionResult? ValidateCapNhatLoVatTuRequest(CapNhatLoVatTuRequest request, out ThongTinLoVatTuHopLe thongTin)
    {
        thongTin = new ThongTinLoVatTuHopLe();

        if (request == null)
            return BadRequest(new { message = "Người dùng bắt buộc nhập đủ các dữ liệu cần thiết" });

        return ValidateThongTinLoVatTu(
            request.MaVatTu,
            request.MaNCC,
            request.SoLuongNhap,
            request.SoLuongTon,
            request.GiaNhap,
            request.GiaBan,
            request.NgaySanXuat,
            request.HanSuDung,
            out thongTin);
    }

    private IActionResult? ValidateThongTinLoVatTu(
        string maVatTu,
        int? maNCC,
        int? soLuongNhap,
        int? soLuongTon,
        decimal? giaNhap,
        decimal? giaBan,
        string? ngaySanXuatText,
        string? hanSuDungText,
        out ThongTinLoVatTuHopLe thongTin)
    {
        thongTin = new ThongTinLoVatTuHopLe();

        if (string.IsNullOrWhiteSpace(maVatTu))
            return BadRequest(new { message = "Vui lòng nhập mã vật tư" });

        if (!maNCC.HasValue)
            return BadRequest(new { message = "Vui lòng nhập mã nhà cung cấp" });

        if (!soLuongNhap.HasValue)
            return BadRequest(new { message = "Vui lòng nhập số lượng nhập của lô vật tư" });

        if (!soLuongTon.HasValue)
            return BadRequest(new { message = "Vui lòng nhập số lượng tồn kho của lô vật tư" });

        if (!giaNhap.HasValue)
            return BadRequest(new { message = "Vui lòng nhập giá nhập của lô vật tư" });

        if (!giaBan.HasValue)
            return BadRequest(new { message = "Vui lòng nhập giá bán niêm yết của lô vật tư" });

        if (string.IsNullOrWhiteSpace(hanSuDungText))
            return BadRequest(new { message = "Vui lòng nhập hạn sử dụng của lô vật tư" });

        if (soLuongNhap.Value < 0)
            return BadRequest(new { message = "Số lượng nhập của lô vật tư phải là 1 số nguyên dương. Vui lòng nhập lại" });

        if (soLuongTon.Value < 0)
            return BadRequest(new { message = "Số lượng tồn kho của lô vật tư phải là 1 số nguyên dương. Vui lòng nhập lại" });

        if (soLuongTon.Value > soLuongNhap.Value)
            return BadRequest(new { message = "Số lượng tồn kho không được lớn hơn số lượng nhập về của lô vật tư. Vui lòng nhập lại" });

        if (giaNhap.Value < 0)
            return BadRequest(new { message = "Gía nhập về của lô vật tư phải là 1 số dương. Vui lòng nhập lại" });

        if (giaBan.Value < 0)
            return BadRequest(new { message = "Gía bán niêm yết của lô vật tư phải là 1 số dương. Vui lòng nhập lại" });

        if (VuotQuaGioiHanDecimal18Phan2(giaNhap.Value))
            return BadRequest(new { message = "Giá nhập về của lô vật tư không được vượt quá 16 chữ số phần nguyên. Vui lòng nhập lại" });

        if (VuotQuaGioiHanDecimal18Phan2(giaBan.Value))
            return BadRequest(new { message = "Giá bán niêm yết của lô vật tư không được vượt quá 16 chữ số phần nguyên. Vui lòng nhập lại" });

        DateOnly? ngaySanXuat = null;
        if (!string.IsNullOrWhiteSpace(ngaySanXuatText)
            && !TryParseNgay(ngaySanXuatText, out ngaySanXuat))
            return BadRequest(new { message = "Ngày sản xuất không hợp lệ. Vui lòng nhập đúng định dạng dd/MM/yyyy" });

        if (!TryParseNgay(hanSuDungText, out DateOnly? hanSuDung) || !hanSuDung.HasValue)
            return BadRequest(new { message = "Hạn sử dụng không hợp lệ. Vui lòng nhập đúng định dạng dd/MM/yyyy" });

        if (ngaySanXuat.HasValue && hanSuDung.Value < ngaySanXuat.Value)
            return BadRequest(new { message = "Hạn sử dụng phải lớn hơn hoặc bằng ngày sản xuất của lô vật tư. Vui lòng nhập lại" });

        thongTin = new ThongTinLoVatTuHopLe
        {
            MaVatTu = maVatTu.Trim().ToUpperInvariant(),
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

    // ================================================================
    // PRIVATE UTILITY METHODS
    // ================================================================

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

    private static string TinhTrangThaiHanSuDung(DateOnly hanSuDung)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        int soNgayConLai = hanSuDung.DayNumber - today.DayNumber;

        if (soNgayConLai >= 180)
            return "An toàn";

        if (soNgayConLai > 0)
            return "Hạn ngắn (<6 th)";

        return "Đã hết hạn";
    }

    private static object ToLoVatTuResponse(LoVatTu loVatTu, DanhMucVatTu? vatTu, NhaCungCap? nhaCungCap)
    {
        return new
        {
            maLo = loVatTu.MaLo,
            maVatTu = loVatTu.MaVatTu,
            tenVatTu = vatTu?.TenVatTu,
            maNCC = loVatTu.MaNcc,
            tenNCC = nhaCungCap?.TenNcc,
            soLuongNhap = loVatTu.SoLuongNhap,
            soLuongTon = loVatTu.SoLuongTon,
            giaNhap = loVatTu.GiaNhap,
            giaBan = loVatTu.GiaBan,
            ngaySanXuat = loVatTu.NgaySanXuat,
            hanSuDung = loVatTu.HanSuDung
        };
    }
}
