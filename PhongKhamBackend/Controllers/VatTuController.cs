using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Authorize]
public class VatTuController : ControllerBase
{
    private static readonly HashSet<string> DonViTinhHopLe = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cái",
        "Cuộn",
        "Hộp",
        "Chai",
        "Gói",
        "Thùng",
        "Bộ"
    };

    private readonly QuanLyPhongKhamDbContext _context;

    public VatTuController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }

    public class ThemVatTuRequest
    {
        public string MaVatTu { get; set; } = string.Empty;
        public string TenVatTu { get; set; } = string.Empty;
        public string? QuyCach { get; set; }
        public string DonViTinh { get; set; } = string.Empty;
    }

    public class CapNhatVatTuRequest
    {
        public string TenVatTu { get; set; } = string.Empty;
        public string? QuyCach { get; set; }
        public string DonViTinh { get; set; } = string.Empty;

        [JsonConverter(typeof(BoolOrNumberJsonConverter))]
        public bool IsActive { get; set; } = true;
    }

    // ================================================================
    // LAY DANH SACH DANH MUC VAT TU
    // GET api/VatTu
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpGet("api/VatTu")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> LayDanhSachVatTu(
        [FromQuery] string? maVatTu = null,
        [FromQuery] string? tenVatTu = null,
        [FromQuery] string? quyCach = null,
        [FromQuery] string? donViTinh = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            if (page <= 0 || pageSize <= 0)
                return BadRequest(new { message = "Giá trị phân trang không hợp lệ" });

            if (pageSize > 100) pageSize = 100;

            var query = _context.DanhMucVatTus.AsQueryable();

            if (!string.IsNullOrWhiteSpace(maVatTu))
            {
                string maVatTuTrim = maVatTu.Trim().ToLower();
                query = query.Where(vt => vt.MaVatTu.ToLower().Contains(maVatTuTrim));
            }

            if (!string.IsNullOrWhiteSpace(tenVatTu))
            {
                string tenVatTuTrim = tenVatTu.Trim().ToLower();
                query = query.Where(vt => vt.TenVatTu.ToLower().Contains(tenVatTuTrim));
            }

            if (!string.IsNullOrWhiteSpace(quyCach))
            {
                string quyCachTrim = quyCach.Trim().ToLower();
                query = query.Where(vt => vt.QuyCach != null && vt.QuyCach.ToLower().Contains(quyCachTrim));
            }

            if (!string.IsNullOrWhiteSpace(donViTinh))
            {
                string donViTinhTrim = donViTinh.Trim().ToLower();
                query = query.Where(vt => vt.DonViTinh.ToLower().Contains(donViTinhTrim));
            }

            int total = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)total / pageSize);

            var danhSach = await query
                .OrderBy(vt => vt.MaVatTu)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(vt => new
                {
                    vt.MaVatTu,
                    vt.TenVatTu,
                    vt.QuyCach,
                    vt.DonViTinh,
                    vt.IsActive
                })
                .ToListAsync();

            var data = danhSach.Select((item, index) => new
            {
                stt = (page - 1) * pageSize + index + 1,
                maVatTu = item.MaVatTu,
                tenVatTu = item.TenVatTu,
                quyCach = item.QuyCach,
                donViTinh = item.DonViTinh,
                isActive = item.IsActive
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
    // THEM DANH MUC VAT TU
    // POST api/VatTu
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpPost("api/VatTu")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> ThemVatTu([FromBody] ThemVatTuRequest request)
    {
        try
        {
            var validationResult = ValidateThemVatTuRequest(request);
            if (validationResult != null) return validationResult;

            string maVatTuUpper = request.MaVatTu.Trim().ToUpperInvariant();
            string tenVatTuTrim = request.TenVatTu.Trim();
            string? quyCachTrim = string.IsNullOrWhiteSpace(request.QuyCach) ? null : request.QuyCach.Trim();
            string donViTinhTrim = request.DonViTinh.Trim();

            bool maVatTuExists = await _context.DanhMucVatTus.AnyAsync(vt => vt.MaVatTu == maVatTuUpper);
            if (maVatTuExists)
                return Conflict(new { message = "Mã vật tư đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại mã khác" });

            bool tenVatTuExists = await _context.DanhMucVatTus.AnyAsync(vt => vt.TenVatTu.ToLower() == tenVatTuTrim.ToLower());
            if (tenVatTuExists)
                return Conflict(new { message = "Tên vật tư đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại tên khác" });

            var vatTu = new DanhMucVatTu
            {
                MaVatTu = maVatTuUpper,
                TenVatTu = tenVatTuTrim,
                QuyCach = quyCachTrim,
                DonViTinh = donViTinhTrim,
                IsActive = true
            };

            _context.DanhMucVatTus.Add(vatTu);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Thêm mới danh mục vật tư thành công",
                data = new
                {
                    maVatTu = vatTu.MaVatTu,
                    tenVatTu = vatTu.TenVatTu,
                    quyCach = vatTu.QuyCach,
                    donViTinh = vatTu.DonViTinh
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ================================================================
    // SUA THONG TIN DANH MUC VAT TU
    // PUT api/VatTu/{maVatTu}
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpPut("api/VatTu/{maVatTu}")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> CapNhatVatTu(string maVatTu, [FromBody] CapNhatVatTuRequest request)
    {
        try
        {
            var vatTu = await _context.DanhMucVatTus.FindAsync(maVatTu);
            if (vatTu == null)
                return NotFound(new { message = "Không tìm thấy danh mục vật tư cần cập nhật" });

            var validationResult = ValidateCapNhatVatTuRequest(request);
            if (validationResult != null) return validationResult;

            string tenVatTuTrim = request.TenVatTu.Trim();
            string? quyCachTrim = string.IsNullOrWhiteSpace(request.QuyCach) ? null : request.QuyCach.Trim();
            string donViTinhTrim = request.DonViTinh.Trim();

            bool tenVatTuDuplicate = await _context.DanhMucVatTus
                .AnyAsync(vt => vt.TenVatTu.ToLower() == tenVatTuTrim.ToLower() && vt.MaVatTu != vatTu.MaVatTu);
            if (tenVatTuDuplicate)
                return Conflict(new { message = "Tên vật tư đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại tên khác" });

            vatTu.TenVatTu = tenVatTuTrim;
            vatTu.QuyCach = quyCachTrim;
            vatTu.DonViTinh = donViTinhTrim;
            vatTu.IsActive = request.IsActive;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật thông tin danh mục vật tư thành công",
                data = new
                {
                    maVatTu = vatTu.MaVatTu,
                    tenVatTu = vatTu.TenVatTu,
                    quyCach = vatTu.QuyCach,
                    donViTinh = vatTu.DonViTinh,
                    isActive = vatTu.IsActive
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ================================================================
    // XOA DANH MUC VAT TU (SOFT DELETE)
    // DELETE api/VatTu/{maVatTu}
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpDelete("api/VatTu/{maVatTu}")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> XoaVatTu(string maVatTu)
    {
        try
        {
            var vatTu = await _context.DanhMucVatTus.FindAsync(maVatTu);
            if (vatTu == null)
                return NotFound(new { message = "Không tìm thấy danh mục vật tư cần tắt" });

            if (!vatTu.IsActive)
                return BadRequest(new { message = "Danh mục vật tư này đã được tắt trước đó" });

            vatTu.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Tắt danh mục vật tư thành công",
                data = new
                {
                    maVatTu = vatTu.MaVatTu,
                    tenVatTu = vatTu.TenVatTu,
                    quyCach = vatTu.QuyCach,
                    donViTinh = vatTu.DonViTinh,
                    isActive = vatTu.IsActive
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể thực hiện thao tác. Xin hãy thử lại" });
        }
    }

    private IActionResult? ValidateThemVatTuRequest(ThemVatTuRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Người dùng bắt buộc nhập đủ các dữ liệu cần thiết" });

        if (string.IsNullOrWhiteSpace(request.MaVatTu))
            return BadRequest(new { message = "Vui lòng nhập mã vật tư" });

        if (string.IsNullOrWhiteSpace(request.TenVatTu))
            return BadRequest(new { message = "Vui lòng nhập tên vật tư" });

        if (string.IsNullOrWhiteSpace(request.DonViTinh))
            return BadRequest(new { message = "Vui lòng chọn đơn vị tính" });

        if (Regex.IsMatch(request.MaVatTu, @"\s"))
            return BadRequest(new { message = "Mã vật tư không được chứa khoảng trắng. Vui lòng nhập lại" });

        if (request.MaVatTu.Trim().Length > 10)
            return BadRequest(new { message = "Mã vật tư không được vượt quá 10 ký tự. Vui lòng nhập lại" });

        if (!Regex.IsMatch(request.MaVatTu.Trim(), @"^[A-Za-z0-9]+$"))
            return BadRequest(new { message = "Mã vật tư không được chứa ký tự đặc biệt. Vui lòng nhập lại" });

        return ValidateThongTinVatTu(request.TenVatTu, request.QuyCach, request.DonViTinh);
    }

    private IActionResult? ValidateCapNhatVatTuRequest(CapNhatVatTuRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Người dùng bắt buộc nhập đủ các dữ liệu cần thiết" });

        if (string.IsNullOrWhiteSpace(request.TenVatTu))
            return BadRequest(new { message = "Vui lòng nhập tên vật tư" });

        if (string.IsNullOrWhiteSpace(request.DonViTinh))
            return BadRequest(new { message = "Vui lòng chọn đơn vị tính" });

        return ValidateThongTinVatTu(request.TenVatTu, request.QuyCach, request.DonViTinh);
    }

    private IActionResult? ValidateThongTinVatTu(string tenVatTu, string? quyCach, string donViTinh)
    {
        if (tenVatTu.Trim().Length > 100)
            return BadRequest(new { message = "Tên vật tư không được vượt quá 100 ký tự. Vui lòng nhập lại" });

        if (!string.IsNullOrWhiteSpace(quyCach) && quyCach.Trim().Length > 100)
            return BadRequest(new { message = "Quy cách đóng gói của vật tư không được vượt quá 100 ký tự. Vui lòng nhập lại" });

        if (!DonViTinhHopLe.Contains(donViTinh.Trim()))
            return BadRequest(new { message = "Đơn vị tính không hợp lệ. Chỉ chấp nhận: Cái, Cuộn, Hộp, Chai, Gói, Thùng, Bộ" });

        return null;
    }

    private class BoolOrNumberJsonConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.True) return true;
            if (reader.TokenType == JsonTokenType.False) return false;

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int number))
            {
                if (number == 1) return true;
                if (number == 0) return false;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string? value = reader.GetString();
                if (bool.TryParse(value, out bool boolValue)) return boolValue;
                if (value == "1") return true;
                if (value == "0") return false;
            }

            throw new JsonException("Trạng thái vật tư không hợp lệ");
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }
}
