using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Authorize]   // Tất cả endpoint trong controller này đều yêu cầu đăng nhập
public class DichVuYTeCLSController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;

    public DichVuYTeCLSController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }


    // DTO

    public class ThemDichVuCLSRequest
    {
        public string MaDV       { get; set; } = string.Empty;
        public string TenDV      { get; set; } = string.Empty;
        public decimal? GiaTien  { get; set; }
        public bool? TrangThai   { get; set; }
    }

    public class CapNhatDichVuCLSRequest
    {
        public string TenDV      { get; set; } = string.Empty;
        public decimal? GiaTien  { get; set; }
        public bool? TrangThai   { get; set; }
    }


    // ================================================================
    // LẤY DANH SÁCH DỊCH VỤ Y TẾ CLS
    // GET api/DichVuCLS
    // Lấy danh sách danh mục dịch vụ y tế CLS có hỗ trợ lọc theo mã/tên, trạng thái và phân trang.
    // Phân quyền: Admin, BacSi, ThuNgan
    // ================================================================

    [HttpGet("api/DichVuCLS")]
    [Authorize(Roles = "Admin,BacSi,ThuNgan")]
    public async Task<IActionResult> LayDanhSachDichVuCLS(
        [FromQuery] string? maDV = null,
        [FromQuery] string? tenDV = null,
        [FromQuery] int? trangThai = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            // Kiểm tra page và pageSize
            if (page <= 0 || pageSize <= 0)
            {
                return BadRequest(new { message = "Giá trị phân trang không hợp lệ" });
            }

            // Giới hạn tối đa 100 bản ghi mỗi trang
            if (pageSize > 100) pageSize = 100;

            // Kiểm tra giá trị trangThai hợp lệ (chỉ chấp nhận 0 hoặc 1)
            if (trangThai.HasValue && trangThai.Value != 0 && trangThai.Value != 1)
            {
                return BadRequest(new { message = "Giá trị trạng thái không hợp lệ. Chỉ chấp nhận: 0 | 1" });
            }

            // Xây dựng query
            var query = _context.DichVuYtes.AsQueryable();

            // Lọc theo mã dịch vụ (tìm kiếm gần đúng, không phân biệt hoa thường)
            if (!string.IsNullOrWhiteSpace(maDV))
            {
                string maDVTrim = maDV.Trim().ToLower();
                query = query.Where(dv => dv.MaDv.ToLower().Contains(maDVTrim));
            }

            // Lọc theo tên dịch vụ (tìm kiếm gần đúng, không phân biệt hoa thường)
            if (!string.IsNullOrWhiteSpace(tenDV))
            {
                string tenDVTrim = tenDV.Trim().ToLower();
                query = query.Where(dv => dv.TenDv.ToLower().Contains(tenDVTrim));
            }

            // Lọc theo trạng thái
            if (trangThai.HasValue)
            {
                bool trangThaiFilter = trangThai.Value == 1;
                query = query.Where(dv => dv.TrangThai == trangThaiFilter);
            }

            // Đếm tổng số bản ghi sau khi lọc
            int total = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)total / pageSize);

            // Phân trang và lấy dữ liệu
            var danhSach = await query
                .OrderBy(dv => dv.MaDv)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(dv => new
                {
                    MaDV      = dv.MaDv,
                    TenDV     = dv.TenDv,
                    GiaTien   = dv.GiaTien,
                    TrangThai = dv.TrangThai
                })
                .ToListAsync();

            // Thêm số thứ tự (tính theo trang hiện tại)
            var data = danhSach.Select((item, index) => new
            {
                stt       = (page - 1) * pageSize + index + 1,
                item.MaDV,
                item.TenDV,
                item.GiaTien,
                item.TrangThai
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
    // THÊM DỊCH VỤ Y TẾ CLS
    // POST api/DichVuCLS
    // Admin thêm mới một dịch vụ CLS vào danh mục dịch vụ y tế.
    // Phân quyền: Chỉ Admin
    // ================================================================

    [HttpPost("api/DichVuCLS")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ThemDichVuCLS([FromBody] ThemDichVuCLSRequest request)
    {
        try
        {
            // VALIDATE CÁC TRƯỜNG BẮT BUỘC

            // Không nhập mã dịch vụ CLS
            if (string.IsNullOrWhiteSpace(request.MaDV))
                return BadRequest(new { message = "Vui lòng nhập mã dịch vụ CLS" });

            // Không nhập tên dịch vụ kỹ thuật y tế CLS
            if (string.IsNullOrWhiteSpace(request.TenDV))
                return BadRequest(new { message = "Vui lòng nhập tên dịch vụ kỹ thuật y tế (CLS)" });

            // Không nhập giá niêm yết dịch vụ CLS
            if (!request.GiaTien.HasValue)
                return BadRequest(new { message = "Vui lòng nhập giá niêm yết dịch vụ" });


            // VALIDATE ĐỊNH DẠNG & RÀNG BUỘC

            // Mã dịch vụ CLS chứa khoảng trắng
            if (request.MaDV.Contains(' '))
                return BadRequest(new { message = "Mã dịch vụ CLS không được chứa khoảng trắng. Vui lòng nhập lại" });

            // Mã dịch vụ CLS vượt quá 5 ký tự
            if (request.MaDV.Trim().Length > 5)
                return BadRequest(new { message = "Mã dịch vụ CLS không được vượt quá 5 ký tự. Vui lòng nhập lại" });

            // Mã dịch vụ CLS chứa ký tự đặc biệt (chỉ cho phép chữ cái và chữ số)
            if (!Regex.IsMatch(request.MaDV.Trim(), @"^[A-Za-z0-9]+$"))
                return BadRequest(new { message = "Mã dịch vụ CLS không được chứa ký tự đặc biệt. Vui lòng nhập lại" });

            // Tên dịch vụ kỹ thuật y tế CLS vượt quá 100 ký tự
            if (request.TenDV.Trim().Length > 100)
                return BadRequest(new { message = "Tên dịch vụ kỹ thuật y tế CLS không được vượt quá 100 ký tự. Vui lòng nhập lại" });

            // Giá niêm yết dịch vụ CLS là số âm
            if (request.GiaTien.Value < 0)
                return BadRequest(new { message = "Giá niêm yết dịch vụ CLS phải là một số dương >= 0. Vui lòng nhập lại" });

            // Server tự động chuyển mã DV về chữ HOA trước khi lưu
            string maDVUpper = request.MaDV.Trim().ToUpper();

            // Mã dịch vụ CLS đã tồn tại trong hệ thống
            bool maDVExists = await _context.DichVuYtes.AnyAsync(dv => dv.MaDv == maDVUpper);
            if (maDVExists)
                return Conflict(new { message = "Mã dịch vụ CLS đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại mã khác" });

            // Tên dịch vụ kỹ thuật y tế CLS đã tồn tại trong hệ thống
            string tenDVTrim = request.TenDV.Trim();
            bool tenDVExists = await _context.DichVuYtes
                .AnyAsync(dv => dv.TenDv.ToLower() == tenDVTrim.ToLower());
            if (tenDVExists)
                return Conflict(new { message = "Tên dịch vụ kỹ thuật y tế CLS đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại tên khác" });


            // TẠO BẢN GHI MỚI TRONG DATABASE
            var newDichVu = new DichVuYte
            {
                MaDv      = maDVUpper,
                TenDv     = tenDVTrim,
                GiaTien   = request.GiaTien.Value,
                TrangThai = request.TrangThai ?? true  // Mặc định là true (Đang áp dụng) nếu không truyền
            };

            _context.DichVuYtes.Add(newDichVu);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Thêm dịch vụ cận lâm sàng & xét nghiệm thành công",
                data = new
                {
                    maDV      = newDichVu.MaDv,
                    tenDV     = newDichVu.TenDv,
                    giaTien   = newDichVu.GiaTien,
                    trangThai = newDichVu.TrangThai
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // ================================================================
    // SỬA DỊCH VỤ Y TẾ CLS
    // PUT api/DichVuCLS/{maDV}
    // Admin cập nhật thông tin một dịch vụ CLS đã tồn tại trong hệ thống.
    // Lưu ý: Không cho phép thay đổi maDV vì đây là khóa chính (PRIMARY KEY)
    // và được tham chiếu bởi bảng ChiTietCanLamSang.
    // Phân quyền: Chỉ Admin
    // ================================================================

    [HttpPut("api/DichVuCLS/{maDV}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CapNhatDichVuCLS(string maDV, [FromBody] CapNhatDichVuCLSRequest request)
    {
        try
        {
            // KIỂM TRA DỊCH VỤ TỒN TẠI
            // maDV trên URL không tồn tại trong hệ thống
            var dichVu = await _context.DichVuYtes.FindAsync(maDV);

            if (dichVu == null)
                return NotFound(new { message = "Không tìm thấy dịch vụ y tế CLS cần cập nhật" });

            // VALIDATE CÁC TRƯỜNG BẮT BUỘC

            // Không nhập tên dịch vụ kỹ thuật y tế CLS
            if (string.IsNullOrWhiteSpace(request.TenDV))
                return BadRequest(new { message = "Vui lòng nhập tên dịch vụ kỹ thuật y tế (CLS)" });

            // Không nhập giá niêm yết
            if (!request.GiaTien.HasValue)
                return BadRequest(new { message = "Vui lòng nhập giá niêm yết dịch vụ" });

            // VALIDATE RÀNG BUỘC
            string tenDVTrim = request.TenDV.Trim();

            // Tên dịch vụ kỹ thuật y tế CLS vượt quá 100 ký tự
            if (tenDVTrim.Length > 100)
                return BadRequest(new { message = "Tên dịch vụ kỹ thuật y tế CLS không được vượt quá 100 ký tự. Vui lòng nhập lại" });

            // Giá niêm yết dịch vụ CLS là số âm
            if (request.GiaTien.Value < 0)
                return BadRequest(new { message = "Giá niêm yết dịch vụ CLS phải là một số dương >= 0. Vui lòng nhập lại" });

            // Tên dịch vụ kỹ thuật y tế CLS đã tồn tại trong hệ thống (ngoại trừ chính bản ghi đang cập nhật)
            bool tenDVDuplicate = await _context.DichVuYtes
                .AnyAsync(dv => dv.TenDv.ToLower() == tenDVTrim.ToLower() && dv.MaDv != maDV);
            if (tenDVDuplicate)
                return Conflict(new { message = "Tên dịch vụ kỹ thuật y tế CLS đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại tên khác" });

            // CẬP NHẬT DỮ LIỆU
            dichVu.TenDv     = tenDVTrim;
            dichVu.GiaTien   = request.GiaTien.Value;
            dichVu.TrangThai = request.TrangThai ?? dichVu.TrangThai;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật dịch vụ y tế CLS thành công",
                data = new
                {
                    maDV      = dichVu.MaDv,
                    tenDV     = dichVu.TenDv,
                    giaTien   = dichVu.GiaTien,
                    trangThai = dichVu.TrangThai
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // ================================================================
    // XÓA DỊCH VỤ Y TẾ CLS
    // DELETE api/DichVuCLS/{maDV}
    // Admin xóa một dịch vụ y tế CLS khỏi danh mục hệ thống.
    // Áp dụng chiến lược xóa thông minh:
    //   - Chưa có dữ liệu liên quan → Xóa cứng (Hard Delete)
    //   - Đã có dữ liệu liên quan   → Gợi ý chuyển trangThai = false
    // Phân quyền: Chỉ Admin
    // ================================================================

    [HttpDelete("api/DichVuCLS/{maDV}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> XoaDichVuCLS(string maDV)
    {
        try
        {
            // KIỂM TRA DỊCH VỤ TỒN TẠI

            // maDV trên URL không tồn tại trong hệ thống
            var dichVu = await _context.DichVuYtes.FindAsync(maDV);

            if (dichVu == null)
                return NotFound(new { message = "Không tìm thấy dịch vụ y tế CLS cần xóa" });

            // KIỂM TRA RÀNG BUỘC KHÓA NGOẠI

            // Mã dịch vụ đang được sử dụng trong phiếu chỉ định cận lâm sàng
            bool hasChiTietCLS = await _context.ChiTietCanLamSangs
                .AnyAsync(ct => ct.MaDv == maDV);

            if (hasChiTietCLS)
            {
                return Conflict(new
                {
                    message         = "Không thể xóa dịch vụ này vì đã có phiếu chỉ định cận lâm sàng sử dụng dịch vụ này",
                    suggestion      = "Bạn có thể chuyển dịch vụ sang trạng thái 'Ngừng cung cấp' thay thế. Sử dụng API cập nhật với trangThai = false",
                    suggestedAction = $"PUT api/DichVuCLS/{maDV}  với  trangThai: false"
                });
            }

            // XÓA BẢN GHI (Hard Delete)
            _context.DichVuYtes.Remove(dichVu);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Xóa dịch vụ y tế CLS thành công",
                data = new
                {
                    maDV      = dichVu.MaDv,
                    tenDV     = dichVu.TenDv,
                    giaTien   = dichVu.GiaTien,
                    trangThai = dichVu.TrangThai
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể xóa dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }
}
