using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PhongKhamBackend.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Authorize]   // Tất cả endpoint trong controller này đều yêu cầu đăng nhập
public class NguoiDungController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public NguoiDungController(QuanLyPhongKhamDbContext context, IConfiguration configuration, IMemoryCache cache)
    {
        _context = context;
        _configuration = configuration;
        _cache = cache;
    }

    
    // DTO
    
    public class DoiMatKhauRequest
    {
        public string MatKhauCu         { get; set; } = string.Empty;
        public string MatKhauMoi        { get; set; } = string.Empty;
        public string NhapLaiMatKhauMoi { get; set; } = string.Empty;
    }

    

    
    // PUT api/NhanSu/DoiMatKhau
 
    /// Đổi mật khẩu người dùng đang đăng nhập.
    /// UserID được lấy tự động từ JWT token, không cần truyền thủ công.
    /// Sau khi đổi thành công, token hiện tại bị vô hiệu hóa → yêu cầu đăng nhập lại.
    [HttpPut("api/NhanSu/DoiMatKhau")]
    public async Task<IActionResult> DoiMatKhau([FromBody] DoiMatKhauRequest request)
    {
        
        // Không để trống bất kỳ trường nào
        if (string.IsNullOrWhiteSpace(request.MatKhauCu)
            || string.IsNullOrWhiteSpace(request.MatKhauMoi)
            || string.IsNullOrWhiteSpace(request.NhapLaiMatKhauMoi))
        {
            return BadRequest(new { message = "Người dùng bắt buộc nhập đủ các dữ liệu cần thiết" });
        }

        // ----------------------------------------------------------------
        // Mật khẩu mới phải đạt quy tắc
        //   + Độ dài 8–12 ký tự
        //   + Ít nhất 1 chữ thường (a-z)
        //   + Ít nhất 1 chữ hoa (A-Z)
        //   + Ít nhất 1 chữ số (0-9)
        //   + Ít nhất 1 ký tự đặc biệt
        //   + Không chứa khoảng trắng
        // ----------------------------------------------------------------
        var lỗiMatKhau = KiemTraQuyTacMatKhau(request.MatKhauMoi);
        if (lỗiMatKhau.Count > 0)
        {
            return BadRequest(new
            {
                message = "Mật khẩu mới không đạt yêu cầu",
                chiTiet = lỗiMatKhau   // danh sách điều kiện chưa đạt (để frontend hiển thị real-time)
            });
        }

        //  Nhập lại mật khẩu mới phải khớp
        if (request.MatKhauMoi != request.NhapLaiMatKhauMoi)
        {
            return BadRequest(new { message = "Nhập lại mật khẩu không khớp" });
        }

       
        // Lấy UserID từ JWT token
        // ([Authorize] đã đảm bảo token hợp lệ)
        
        string? userIdClaim = User.FindFirstValue("userID");
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });
        }

       
        //Truy vấn user từ DB
        
        User? user;
        try
        {
            user = await _context.Users.FindAsync(userId);
        }
        catch (Exception)
        {
           
            return StatusCode(503, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }

        if (user == null)
        {
            return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });
        }

        
        //  Mật khẩu cũ phải chính xác
        
        if (!BCrypt.Net.BCrypt.Verify(request.MatKhauCu, user.PasswordHash))
        {
            return BadRequest(new { message = "Mật khẩu cũ không chính xác" });
        }

        
        // Mật khẩu mới không được trùng mật khẩu cũ
       
        if (BCrypt.Net.BCrypt.Verify(request.MatKhauMoi, user.PasswordHash))
        {
            return BadRequest(new { message = "Mật khẩu mới không được trùng với mật khẩu cũ" });
        }

        
        //  Hash mật khẩu mới và cập nhật vào DB
        try
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.MatKhauMoi);
            await _context.SaveChangesAsync();
        }
        catch (Exception)
        {
            return StatusCode(503, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }

        
        //  Vô hiệu hóa token hiện tại → yêu cầu đăng nhập lại
        
        string? authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            string token = authHeader["Bearer ".Length..].Trim();
            int expiryHours = int.Parse(_configuration["Jwt:ExpiryHours"] ?? "8");
            _cache.Set($"blacklist_{token}", true, TimeSpan.FromHours(expiryHours));
        }

        return Ok(new { message = "Cập nhật mật khẩu mới thành công. Vui lòng đăng nhập lại" });
    }
   
    // Kiểm tra quy tắc mật khẩu (trả về danh sách lỗi)
    
    private static List<string> KiemTraQuyTacMatKhau(string matKhau)
    {
        var lỗi = new List<string>();

        if (matKhau.Length < 8 || matKhau.Length > 12)
            lỗi.Add("Mật khẩu phải có độ dài từ 8 đến 12 ký tự");

        if (!Regex.IsMatch(matKhau, @"[a-z]"))
            lỗi.Add("Mật khẩu phải có ít nhất 1 chữ thường (a-z)");

        if (!Regex.IsMatch(matKhau, @"[A-Z]"))
            lỗi.Add("Mật khẩu phải có ít nhất 1 chữ hoa (A-Z)");

        if (!Regex.IsMatch(matKhau, @"[0-9]"))
            lỗi.Add("Mật khẩu phải có ít nhất 1 chữ số (0-9)");

        if (!Regex.IsMatch(matKhau, @"[!@#$%^&*()\-_=+\[\]{};:'"",.<>/?\\|`~]"))
            lỗi.Add("Mật khẩu phải có ít nhất 1 ký tự đặc biệt (!@#$%...)");

        if (matKhau.Contains(' '))
            lỗi.Add("Mật khẩu không được chứa khoảng trắng");

        return lỗi;
    }
}
