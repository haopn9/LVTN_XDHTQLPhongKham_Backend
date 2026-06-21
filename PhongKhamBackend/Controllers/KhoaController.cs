using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Authorize]   // Tất cả endpoint trong controller này đều yêu cầu đăng nhập
public class KhoaController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;

    public KhoaController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }


    // DTO

    public class ThemKhoaRequest
    {
        public string MaKhoa  { get; set; } = string.Empty;
        public string TenKhoa { get; set; } = string.Empty;
    }

    public class CapNhatKhoaRequest
    {
        public string TenKhoa { get; set; } = string.Empty;
    }


    // ================================================================
    // LẤY DANH SÁCH KHOA
    // GET api/Khoa
    // Lấy danh sách khoa chuyên môn từ bảng DanhMucKhoa, có hỗ trợ
    // lọc theo mã/tên khoa và phân trang.
    // Phân quyền: Chỉ Admin
    // ================================================================

    [HttpGet("api/Khoa")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> LayDanhSachKhoa(
        [FromQuery] string? maKhoa = null,
        [FromQuery] string? tenKhoa = null,
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

            // Xây dựng query
            var query = _context.DanhMucKhoas.AsQueryable();

            // Lọc theo mã khoa (tìm kiếm gần đúng, không phân biệt hoa thường)
            if (!string.IsNullOrWhiteSpace(maKhoa))
            {
                string maKhoaTrim = maKhoa.Trim().ToLower();
                query = query.Where(k => k.MaKhoa.ToLower().Contains(maKhoaTrim));
            }

            // Lọc theo tên khoa (tìm kiếm gần đúng, không phân biệt hoa thường)
            if (!string.IsNullOrWhiteSpace(tenKhoa))
            {
                string tenKhoaTrim = tenKhoa.Trim().ToLower();
                query = query.Where(k => k.TenKhoa.ToLower().Contains(tenKhoaTrim));
            }

            // Đếm tổng số bản ghi sau khi lọc
            int total = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)total / pageSize);

            // Phân trang và lấy dữ liệu
            // JOIN với bảng NhanVien để đếm số bác sĩ đang hoạt động
            // (User.IsActive = true) theo từng MaKhoa
            var danhSach = await query
                .OrderBy(k => k.MaKhoa)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(k => new
                {
                    MaKhoa  = k.MaKhoa,
                    TenKhoa = k.TenKhoa,
                    SoLuongBacSi = k.NhanViens
                        .Count(nv => nv.User != null
                                  && nv.User.IsActive == true
                                  && nv.User.Role != null
                                  && nv.User.Role.RoleName == "BacSi")
                })
                .ToListAsync();

            // Thêm số thứ tự (tính theo trang hiện tại)
            var data = danhSach.Select((item, index) => new
            {
                stt           = (page - 1) * pageSize + index + 1,
                maKhoa        = item.MaKhoa,
                tenKhoa       = item.TenKhoa,
                soLuongBacSi  = item.SoLuongBacSi
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
    // LẤY DANH SÁCH BÁC SĨ THUỘC 1 KHOA CHUYÊN MÔN
    // GET api/Khoa/{maKhoa}/BacSi
    // Lấy danh sách bác sĩ (Role = BacSi) thuộc một khoa chuyên môn
    // cụ thể, tra cứu qua cột MaKhoa trong bảng NhanVien.
    // Phân quyền: Chỉ Admin
    // ================================================================

    [HttpGet("api/Khoa/{maKhoa}/BacSi")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> LayDanhSachBacSiTheoKhoa(string maKhoa)
    {
        try
        {
            // KIỂM TRA KHOA TỒN TẠI
            // maKhoa trên URL không tồn tại trong hệ thống
            var khoa = await _context.DanhMucKhoas.FindAsync(maKhoa);

            if (khoa == null)
                return NotFound(new { message = "Không tìm thấy khoa chuyên môn" });

            // JOIN NhanVien → Users → Roles
            // WHERE NhanVien.MaKhoa = {maKhoa} AND Roles.RoleName = 'BacSi'
            var danhSachBacSi = await _context.NhanViens
                .Where(nv => nv.MaKhoa == maKhoa
                          && nv.User != null
                          && nv.User.Role != null
                          && nv.User.Role.RoleName == "BacSi")
                .Include(nv => nv.User)
                .OrderBy(nv => nv.MaNv)
                .Select(nv => new
                {
                    MaNV     = nv.MaNv,
                    HoTen    = nv.HoTen,
                    IsActive = nv.User != null ? nv.User.IsActive : false
                })
                .ToListAsync();

            // Thêm số thứ tự
            var data = danhSachBacSi.Select((item, index) => new
            {
                stt      = index + 1,
                maNV     = item.MaNV,
                hoTen    = item.HoTen,
                isActive = item.IsActive
            });

            return Ok(new
            {
                maKhoa  = khoa.MaKhoa,
                tenKhoa = khoa.TenKhoa,
                data
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // ================================================================
    // THÊM KHOA CHUYÊN MÔN
    // POST api/Khoa
    // Admin thêm mới một khoa chuyên môn vào bảng DanhMucKhoa.
    // Phân quyền: Chỉ Admin
    // ================================================================

    [HttpPost("api/Khoa")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ThemKhoa([FromBody] ThemKhoaRequest request)
    {
        try
        {
            // VALIDATE CÁC TRƯỜNG BẮT BUỘC

            // Không nhập mã khoa
            if (string.IsNullOrWhiteSpace(request.MaKhoa))
                return BadRequest(new { message = "Vui lòng nhập mã khoa chuyên môn" });

            // Không nhập tên khoa
            if (string.IsNullOrWhiteSpace(request.TenKhoa))
                return BadRequest(new { message = "Vui lòng nhập tên khoa chuyên môn" });

            // VALIDATE ĐỊNH DẠNG & RÀNG BUỘC

            // Mã khoa chứa khoảng trắng
            if (request.MaKhoa.Contains(' '))
                return BadRequest(new { message = "Mã khoa chuyên môn không được chứa khoảng trắng. Vui lòng nhập lại" });

            // Mã khoa vượt quá 10 ký tự
            if (request.MaKhoa.Trim().Length > 10)
                return BadRequest(new { message = "Mã khoa chuyên môn không được vượt quá 10 ký tự. Vui lòng nhập lại" });

            // Mã khoa chứa ký tự đặc biệt (chỉ cho phép chữ cái và chữ số)
            if (!Regex.IsMatch(request.MaKhoa.Trim(), @"^[A-Za-z0-9]+$"))
                return BadRequest(new { message = "Mã khoa chuyên môn không được chứa ký tự đặc biệt. Vui lòng nhập lại" });

            // Tên khoa vượt quá 100 ký tự
            string tenKhoaTrim = request.TenKhoa.Trim();
            if (tenKhoaTrim.Length > 100)
                return BadRequest(new { message = "Tên khoa chuyên môn không được vượt quá 100 ký tự. Vui lòng nhập lại" });

            // Server tự động chuyển mã khoa về chữ HOA trước khi lưu
            string maKhoaUpper = request.MaKhoa.Trim().ToUpper();

            // KIỂM TRA TRÙNG DỮ LIỆU

            // Mã khoa đã tồn tại trong hệ thống
            bool maKhoaExists = await _context.DanhMucKhoas.AnyAsync(k => k.MaKhoa == maKhoaUpper);
            if (maKhoaExists)
                return Conflict(new { message = "Mã khoa chuyên môn đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại mã khác" });

            // Tên khoa đã tồn tại trong hệ thống
            bool tenKhoaExists = await _context.DanhMucKhoas
                .AnyAsync(k => k.TenKhoa.ToLower() == tenKhoaTrim.ToLower());
            if (tenKhoaExists)
                return Conflict(new { message = "Tên khoa chuyên môn đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại tên khác" });

            // TẠO BẢN GHI MỚI TRONG DATABASE
            var newKhoa = new DanhMucKhoa
            {
                MaKhoa  = maKhoaUpper,
                TenKhoa = tenKhoaTrim
            };

            _context.DanhMucKhoas.Add(newKhoa);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Thêm mới khoa chuyên môn thành công",
                data = new
                {
                    maKhoa  = newKhoa.MaKhoa,
                    tenKhoa = newKhoa.TenKhoa
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // ================================================================
    // SỬA THÔNG TIN KHOA CHUYÊN MÔN
    // PUT api/Khoa/{maKhoa}
    // Admin cập nhật tên của một khoa chuyên môn đã tồn tại trong
    // bảng DanhMucKhoa. Chỉ cho phép sửa tenKhoa, không cho sửa
    // maKhoa vì đây là PK và là FK trong bảng NhanVien.
    // Phân quyền: Chỉ Admin
    // ================================================================

    [HttpPut("api/Khoa/{maKhoa}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CapNhatKhoa(string maKhoa, [FromBody] CapNhatKhoaRequest request)
    {
        try
        {
            // KIỂM TRA KHOA TỒN TẠI
            // maKhoa trên URL không tồn tại trong hệ thống
            var khoa = await _context.DanhMucKhoas.FindAsync(maKhoa);

            if (khoa == null)
                return NotFound(new { message = "Không tìm thấy khoa chuyên môn cần cập nhật" });

            // VALIDATE CÁC TRƯỜNG BẮT BUỘC
            // Không nhập tên khoa
            if (string.IsNullOrWhiteSpace(request.TenKhoa))
                return BadRequest(new { message = "Vui lòng nhập tên khoa chuyên môn" });

            // VALIDATE RÀNG BUỘC
            string tenKhoaTrim = request.TenKhoa.Trim();

            // Tên khoa vượt quá 100 ký tự
            if (tenKhoaTrim.Length > 100)
                return BadRequest(new { message = "Tên khoa chuyên môn không được vượt quá 100 ký tự. Vui lòng nhập lại" });

            // Tên khoa đã tồn tại trong hệ thống (ngoại trừ chính bản ghi đang cập nhật)
            bool tenKhoaDuplicate = await _context.DanhMucKhoas
                .AnyAsync(k => k.TenKhoa.ToLower() == tenKhoaTrim.ToLower() && k.MaKhoa != maKhoa);
            if (tenKhoaDuplicate)
                return Conflict(new { message = "Tên khoa chuyên môn đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại tên khác" });

            // CẬP NHẬT DỮ LIỆU
            khoa.TenKhoa = tenKhoaTrim;

            await _context.SaveChangesAsync();

            // Đếm soLuongBacSi hiện tại của khoa
            int soLuongBacSi = await _context.NhanViens
                .CountAsync(nv => nv.MaKhoa == maKhoa
                               && nv.User != null
                               && nv.User.IsActive == true
                               && nv.User.Role != null
                               && nv.User.Role.RoleName == "BacSi");

            return Ok(new
            {
                message = "Cập nhật thông tin khoa chuyên môn thành công",
                data = new
                {
                    maKhoa       = khoa.MaKhoa,
                    tenKhoa      = khoa.TenKhoa,
                    soLuongBacSi = soLuongBacSi
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // ================================================================
    // XÓA KHOA CHUYÊN MÔN
    // DELETE api/Khoa/{maKhoa}
    // Admin xóa một khoa chuyên môn khỏi bảng DanhMucKhoa. Áp dụng
    // chiến lược xóa thông minh dựa trên dữ liệu liên quan:
    // - Nếu CHƯA có nhân viên tham chiếu → Xóa cứng (Hard Delete)
    // - Nếu ĐÃ có nhân viên tham chiếu → KHÔNG xóa, trả HTTP 409
    // Phân quyền: Chỉ Admin
    // ================================================================

    [HttpDelete("api/Khoa/{maKhoa}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> XoaKhoa(string maKhoa)
    {
        try
        {
            // KIỂM TRA KHOA TỒN TẠI
            // maKhoa trên URL không tồn tại trong hệ thống
            var khoa = await _context.DanhMucKhoas.FindAsync(maKhoa);

            if (khoa == null)
                return NotFound(new { message = "Không tìm thấy khoa chuyên môn cần xóa" });

            // KIỂM TRA RÀNG BUỘC KHÓA NGOẠI
            // Kiểm tra có bản ghi nào trong NhanVien.MaKhoa = {maKhoa} không
            bool hasNhanVien = await _context.NhanViens
                .AnyAsync(nv => nv.MaKhoa == maKhoa);

            if (hasNhanVien)
            {
                return Conflict(new
                {
                    message = "Khoa vẫn còn bác sĩ trực thuộc. Không thể xóa khoa này"
                });
            }

            // XÓA BẢN GHI
            _context.DanhMucKhoas.Remove(khoa);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Xóa khoa chuyên môn thành công",
                data = new
                {
                    maKhoa  = khoa.MaKhoa,
                    tenKhoa = khoa.TenKhoa
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể xóa dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }
}
