using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Authorize]
public class ThuocController : ControllerBase
{
    private static readonly HashSet<string> DonViTinhHopLe = new(StringComparer.OrdinalIgnoreCase)
    {
        "Viên",
        "Vỉ",
        "Hộp",
        "Chai",
        "Gói",
        "Ống",
        "Tuýp"
    };

    private readonly QuanLyPhongKhamDbContext _context;

    public ThuocController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }

    public class ThemThuocRequest
    {
        public string MaThuoc { get; set; } = string.Empty;
        public string TenThuoc { get; set; } = string.Empty;
        public string? HoatChat { get; set; }
        public string DonViTinh { get; set; } = string.Empty;
    }

    public class CapNhatThuocRequest
    {
        public string TenThuoc { get; set; } = string.Empty;
        public string? HoatChat { get; set; }
        public string DonViTinh { get; set; } = string.Empty;
    }

    // ================================================================
    // LAY DANH SACH DANH MUC THUOC
    // GET api/Thuoc
    // Phan quyen: Admin, BacSi, LeTan, ThuNgan, QuanLyKhoThuoc
    // ================================================================

    [HttpGet("api/Thuoc")]
    [Authorize(Roles = "Admin,BacSi,LeTan,ThuNgan,QuanLyKhoThuoc")]
    public async Task<IActionResult> LayDanhSachThuoc(
        [FromQuery] string? maThuoc = null,
        [FromQuery] string? tenThuoc = null,
        [FromQuery] string? hoatChat = null,
        [FromQuery] string? donViTinh = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            if (page <= 0 || pageSize <= 0)
                return BadRequest(new { message = "Giá trị phân trang không hợp lệ" });

            if (pageSize > 100) pageSize = 100;

            bool coQuyenQuanLyKho = User.IsInRole("Admin") || User.IsInRole("QuanLyKhoThuoc");
            var query = _context.DanhMucThuocs.AsQueryable();

            if (!coQuyenQuanLyKho)
                query = query.Where(t => t.IsActive);

            if (!string.IsNullOrWhiteSpace(maThuoc))
            {
                string maThuocTrim = maThuoc.Trim().ToLower();
                query = query.Where(t => t.MaThuoc.ToLower().Contains(maThuocTrim));
            }

            if (!string.IsNullOrWhiteSpace(tenThuoc))
            {
                string tenThuocTrim = tenThuoc.Trim().ToLower();
                query = query.Where(t => t.TenThuoc.ToLower().Contains(tenThuocTrim));
            }

            if (!string.IsNullOrWhiteSpace(hoatChat))
            {
                string hoatChatTrim = hoatChat.Trim().ToLower();
                query = query.Where(t => t.HoatChat != null && t.HoatChat.ToLower().Contains(hoatChatTrim));
            }

            if (!string.IsNullOrWhiteSpace(donViTinh))
            {
                string donViTinhTrim = donViTinh.Trim().ToLower();
                query = query.Where(t => t.DonViTinh != null && t.DonViTinh.ToLower().Contains(donViTinhTrim));
            }

            int total = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)total / pageSize);

            var danhSach = await query
                .OrderBy(t => t.MaThuoc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.MaThuoc,
                    t.TenThuoc,
                    t.HoatChat,
                    t.DonViTinh,
                    t.IsActive
                })
                .ToListAsync();

            object data = coQuyenQuanLyKho
                ? danhSach.Select((item, index) => new
                {
                    stt = (page - 1) * pageSize + index + 1,
                    maThuoc = item.MaThuoc,
                    tenThuoc = item.TenThuoc,
                    hoatChat = item.HoatChat,
                    donViTinh = item.DonViTinh,
                    isActive = item.IsActive
                })
                : danhSach.Select((item, index) => new
                {
                    stt = (page - 1) * pageSize + index + 1,
                    maThuoc = item.MaThuoc,
                    tenThuoc = item.TenThuoc,
                    hoatChat = item.HoatChat,
                    donViTinh = item.DonViTinh
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
    // THEM DANH MUC THUOC
    // POST api/Thuoc
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpPost("api/Thuoc")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> ThemThuoc([FromBody] ThemThuocRequest request)
    {
        try
        {
            var validationResult = ValidateThemThuocRequest(request);
            if (validationResult != null) return validationResult;

            string maThuocUpper = request.MaThuoc.Trim().ToUpperInvariant();
            string tenThuocTrim = request.TenThuoc.Trim();
            string? hoatChatTrim = string.IsNullOrWhiteSpace(request.HoatChat) ? null : request.HoatChat.Trim();
            string donViTinhTrim = request.DonViTinh.Trim();

            bool maThuocExists = await _context.DanhMucThuocs.AnyAsync(t => t.MaThuoc == maThuocUpper);
            if (maThuocExists)
                return Conflict(new { message = "Mã thuốc đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại mã khác" });

            bool tenThuocExists = await _context.DanhMucThuocs.AnyAsync(t => t.TenThuoc.ToLower() == tenThuocTrim.ToLower());
            if (tenThuocExists)
                return Conflict(new { message = "Tên thuốc đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại tên khác" });

            var thuoc = new DanhMucThuoc
            {
                MaThuoc = maThuocUpper,
                TenThuoc = tenThuocTrim,
                HoatChat = hoatChatTrim,
                DonViTinh = donViTinhTrim,
                IsActive = true
            };

            _context.DanhMucThuocs.Add(thuoc);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Thêm mới danh mục thuốc thành công",
                data = new
                {
                    maThuoc = thuoc.MaThuoc,
                    tenThuoc = thuoc.TenThuoc,
                    hoatChat = thuoc.HoatChat,
                    donViTinh = thuoc.DonViTinh
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ================================================================
    // SUA THONG TIN DANH MUC THUOC
    // PUT api/Thuoc/{maThuoc}
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpPut("api/Thuoc/{maThuoc}")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> CapNhatThuoc(string maThuoc, [FromBody] CapNhatThuocRequest request)
    {
        try
        {
            var thuoc = await _context.DanhMucThuocs.FindAsync(maThuoc);
            if (thuoc == null)
                return NotFound(new { message = "Không tìm thấy danh mục thuốc cần cập nhật" });

            var validationResult = ValidateCapNhatThuocRequest(request);
            if (validationResult != null) return validationResult;

            string tenThuocTrim = request.TenThuoc.Trim();
            string? hoatChatTrim = string.IsNullOrWhiteSpace(request.HoatChat) ? null : request.HoatChat.Trim();
            string donViTinhTrim = request.DonViTinh.Trim();

            bool tenThuocDuplicate = await _context.DanhMucThuocs
                .AnyAsync(t => t.TenThuoc.ToLower() == tenThuocTrim.ToLower() && t.MaThuoc != thuoc.MaThuoc);
            if (tenThuocDuplicate)
                return Conflict(new { message = "Tên thuốc đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại tên khác" });

            thuoc.TenThuoc = tenThuocTrim;
            thuoc.HoatChat = hoatChatTrim;
            thuoc.DonViTinh = donViTinhTrim;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật thông tin danh mục thuốc thành công",
                data = new
                {
                    maThuoc = thuoc.MaThuoc,
                    tenThuoc = thuoc.TenThuoc,
                    hoatChat = thuoc.HoatChat,
                    donViTinh = thuoc.DonViTinh
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ================================================================
    // XOA DANH MUC THUOC (SOFT DELETE)
    // DELETE api/Thuoc/{maThuoc}
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpDelete("api/Thuoc/{maThuoc}")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> XoaThuoc(string maThuoc)
    {
        try
        {
            var thuoc = await _context.DanhMucThuocs.FindAsync(maThuoc);
            if (thuoc == null)
                return NotFound(new { message = "Không tìm thấy danh mục thuốc cần tắt" });

            if (!thuoc.IsActive)
                return BadRequest(new { message = "Danh mục thuốc này đã được tắt trước đó" });

            thuoc.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Tắt danh mục thuốc thành công",
                data = new
                {
                    maThuoc = thuoc.MaThuoc,
                    tenThuoc = thuoc.TenThuoc,
                    hoatChat = thuoc.HoatChat,
                    donViTinh = thuoc.DonViTinh,
                    isActive = thuoc.IsActive
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể thực hiện thao tác. Xin hãy thử lại" });
        }
    }

    private IActionResult? ValidateThemThuocRequest(ThemThuocRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Người dùng bắt buộc nhập đủ các dữ liệu cần thiết" });

        if (string.IsNullOrWhiteSpace(request.MaThuoc))
            return BadRequest(new { message = "Vui lòng nhập mã thuốc" });

        if (string.IsNullOrWhiteSpace(request.TenThuoc))
            return BadRequest(new { message = "Vui lòng nhập tên thuốc" });

        if (string.IsNullOrWhiteSpace(request.DonViTinh))
            return BadRequest(new { message = "Vui lòng chọn đơn vị tính" });

        if (Regex.IsMatch(request.MaThuoc, @"\s"))
            return BadRequest(new { message = "Mã thuốc không được chứa khoảng trắng. Vui lòng nhập lại" });

        if (request.MaThuoc.Trim().Length > 10)
            return BadRequest(new { message = "Mã thuốc không được vượt quá 10 ký tự. Vui lòng nhập lại" });

        if (!Regex.IsMatch(request.MaThuoc.Trim(), @"^[A-Za-z0-9]+$"))
            return BadRequest(new { message = "Mã thuốc không được chứa ký tự đặc biệt. Vui lòng nhập lại" });

        return ValidateThongTinThuoc(request.TenThuoc, request.HoatChat, request.DonViTinh);
    }

    private IActionResult? ValidateCapNhatThuocRequest(CapNhatThuocRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Người dùng bắt buộc nhập đủ các dữ liệu cần thiết" });

        if (string.IsNullOrWhiteSpace(request.TenThuoc))
            return BadRequest(new { message = "Vui lòng nhập tên thuốc" });

        if (string.IsNullOrWhiteSpace(request.DonViTinh))
            return BadRequest(new { message = "Vui lòng chọn đơn vị tính" });

        return ValidateThongTinThuoc(request.TenThuoc, request.HoatChat, request.DonViTinh);
    }

    private IActionResult? ValidateThongTinThuoc(string tenThuoc, string? hoatChat, string donViTinh)
    {
        if (tenThuoc.Trim().Length > 100)
            return BadRequest(new { message = "Tên thuốc không được vượt quá 100 ký tự. Vui lòng nhập lại" });

        if (!string.IsNullOrWhiteSpace(hoatChat) && hoatChat.Trim().Length > 255)
            return BadRequest(new { message = "Hoạt chất chính của thuốc không được vượt quá 255 ký tự. Vui lòng nhập lại" });

        if (!DonViTinhHopLe.Contains(donViTinh.Trim()))
            return BadRequest(new { message = "Đơn vị tính không hợp lệ. Chỉ chấp nhận: Viên, Vỉ, Hộp, Chai, Gói, Ống, Tuýp" });

        return null;
    }
}
