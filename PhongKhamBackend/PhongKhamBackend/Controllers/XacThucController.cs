using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using PhongKhamBackend.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Route("api/xacthuc")]
public class XacThucController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    // Cấu hình giới hạn đăng nhập
    private const int MAX_LOGIN_ATTEMPTS = 5;
    private const int LOCKOUT_MINUTES = 5;

    public XacThucController(QuanLyPhongKhamDbContext context, IConfiguration configuration, IMemoryCache cache)
    {
        _context = context;
        _configuration = configuration;
        _cache = cache;
    }

    // DTO
    public class DangNhapRequest
    {
        // Email đăng nhập — được lưu trong cột Username của bảng Users
        public string Email    { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // POST api/xacthuc/DangNhap
    // Đăng nhập tài khoản. Trả về JWT token và thông tin người dùng.
    [HttpPost("DangNhap")]
    public async Task<IActionResult> DangNhap([FromBody] DangNhapRequest request)
    {
        // Không để trống Email hoặc Password
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Người dùng bắt buộc nhập đủ email & mật khẩu để đăng nhập tài khoản"
            });
        }

        // Email phải đúng định dạng
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                request.Email.Trim(),
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            return BadRequest(new
            {
                message = "Người dùng bắt buộc nhập đủ email & mật khẩu để đăng nhập tài khoản"
            });
        }

        // Kiểm tra tài khoản đang bị khóa tạm thời
        string lockKey = $"lockout_{request.Email.Trim().ToLower()}";
        if (_cache.TryGetValue(lockKey, out DateTime lockoutUntil) && lockoutUntil > DateTime.UtcNow)
        {
            return StatusCode(423, new
            {
                message = "Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau 5 phút"
            });
        }

        // Truy vấn DB
        // Email đăng nhập được lưu trong cột Username của bảng Users
        string emailInput = request.Email.Trim();
        User? user;
        try
        {
            user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.NhanVien)
                .FirstOrDefaultAsync(u => u.Username == emailInput);
        }
        catch (Exception)
        {
            // Không kết nối được DB
            return StatusCode(503, new
            {
                message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại"
            });
        }

        // Kiểm tra Username tồn tại và Password khớp 
        // Dùng một thông báo chung để không lộ thông tin 
        bool credentialsValid = user != null
                                && BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!credentialsValid)
        {
            // Cộng dồn số lần đăng nhập sai
            string attemptKey = $"attempts_{emailInput.ToLower()}";
            int attempts = _cache.GetOrCreate(attemptKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(LOCKOUT_MINUTES);
                return 0;
            });

            attempts++;
            _cache.Set(attemptKey, attempts, TimeSpan.FromMinutes(LOCKOUT_MINUTES));

            // Sau 5 lần sai → khóa tạm thời 5 phút
            if (attempts >= MAX_LOGIN_ATTEMPTS)
            {
                _cache.Set(lockKey, DateTime.UtcNow.AddMinutes(LOCKOUT_MINUTES),
                           TimeSpan.FromMinutes(LOCKOUT_MINUTES));
                _cache.Remove(attemptKey);

                return StatusCode(423, new
                {
                    message = "Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau 5 phút"
                });
            }

            // Thông báo chung, không phân biệt sai email hay sai password
            return Unauthorized(new
            {
                message = "Email hoặc mật khẩu không chính xác"
            });
        }

        // Kiểm tra trạng thái tài khoản (IsActive)
        if (user!.IsActive == false)
        {
            // Tài khoản bị vô hiệu hóa
            return StatusCode(403, new
            {
                message = "Tài khoản đã bị vô hiệu hóa, vui lòng liên hệ quản trị viên"
            });
        }

        // Đăng nhập thành công → xóa bộ đếm lần sai 
        _cache.Remove($"attempts_{emailInput.ToLower()}");
        _cache.Remove(lockKey);

        // Lấy thông tin Role và NhanVien 
        int    roleId   = user.RoleId ?? 0;
        string roleName = MapRoleName(roleId);
        string hoTen    = user.NhanVien?.HoTen ?? string.Empty;
        string maNV     = user.NhanVien?.MaNv  ?? string.Empty;

        // Tạo JWT token
        string token = TaoJwtToken(user.UserId, user.Username, roleId, roleName);

        // Trả về client
        return Ok(new
        {
            message  = "Đăng nhập thành công",
            token,
            userID   = user.UserId,
            roleID   = roleId,
            roleName,
            hoTen,
            maNV
        });
    }

    
    // POST api/xacthuc/DangXuat
    // Đăng xuất tài khoản. Vô hiệu hóa token hiện tại bằng blacklist.
    [HttpPost("DangXuat")]
    [Authorize]
    public IActionResult DangXuat()
    {
        // Lấy token từ header Authorization
        string? authHeader = Request.Headers["Authorization"].FirstOrDefault();

        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            string token = authHeader["Bearer ".Length..].Trim();

            // Đưa token vào blacklist cho đến khi hết thời gian hợp lệ
            int expiryHours = int.Parse(_configuration["Jwt:ExpiryHours"] ?? "8");
            _cache.Set($"blacklist_{token}", true, TimeSpan.FromHours(expiryHours));
        }

        // Dù token không hợp lệ vẫn trả thành công
        return Ok(new { message = "Đăng xuất thành công" });
    }

    // Tạo JWT token
    private string TaoJwtToken(int userId, string username, int roleId, string roleName)
    {
        var    jwtConfig   = _configuration.GetSection("Jwt");
        string secretKey   = jwtConfig["SecretKey"]!;
        string issuer      = jwtConfig["Issuer"]!;
        string audience    = jwtConfig["Audience"]!;
        int    expiryHours = int.Parse(jwtConfig["ExpiryHours"] ?? "8");

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,  username),
            new Claim(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString()),  
            new Claim("userID",              userId.ToString()),
            new Claim("roleID",              roleId.ToString()),
            new Claim("roleName",            roleName),
            new Claim(ClaimTypes.Role,       roleName)
        };

        var jwtToken = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(jwtToken);
    }

   
    // Map RoleID → RoleName

    private static string MapRoleName(int roleId) => roleId switch
    {
        1 => "Admin",
        2 => "BacSi",
        3 => "LeTan",
        4 => "ThuNgan",
        5 => "QuanLyKhoThuoc",
        _ => "Unknown"
    };
}
