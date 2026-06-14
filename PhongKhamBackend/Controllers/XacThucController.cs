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
        int    roleId    = user.RoleId ?? 0;
        string roleName  = MapRoleName(roleId);
        string hoTen     = user.NhanVien?.HoTen      ?? string.Empty;
        string maNV      = user.NhanVien?.MaNv        ?? string.Empty;
        string sdt       = user.NhanVien?.Sdt         ?? string.Empty;
        string email     = user.NhanVien?.Email       ?? string.Empty;
        string chuyenMon = user.NhanVien?.ChuyenMon   ?? string.Empty;

        // Tạo JWT token
        string token = TaoJwtToken(user.UserId, user.Username, roleId, roleName);

        // Trả về client
        return Ok(new
        {
            message   = "Đăng nhập thành công",
            token,
            userID    = user.UserId,
            roleID    = roleId,
            roleName,
            hoTen,
            maNV,
            sdt,
            email,
            chuyenMon
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

    // DTO cho Quên mật khẩu
    public class QuenMatKhauRequest
    {
        public string Email             { get; set; } = string.Empty;
        public string SoDienThoai       { get; set; } = string.Empty;
        public string MatKhauMoi        { get; set; } = string.Empty;
        public string NhapLaiMatKhauMoi { get; set; } = string.Empty;
    }

    // POST api/xacthuc/QuenMatKhau
    // Đặt lại mật khẩu khi quên — không cần đăng nhập.
    // Xác thực danh tính qua Email + Số điện thoại.
    [HttpPost("QuenMatKhau")]
    [AllowAnonymous]
    public async Task<IActionResult> QuenMatKhau([FromBody] QuenMatKhauRequest request)
    {
        
        // Không để trống bất kỳ trường nào
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.SoDienThoai)
            || string.IsNullOrWhiteSpace(request.MatKhauMoi)
            || string.IsNullOrWhiteSpace(request.NhapLaiMatKhauMoi))
        {
            return BadRequest(new { message = "Người dùng bắt buộc nhập đủ các dữ liệu cần thiết" });
        }

        
        // Email phải đúng định dạng
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                request.Email.Trim(),
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            return BadRequest(new { message = "Email không đúng định dạng" });
        }

      
        // Mật khẩu mới phải đạt quy tắc
       
        var loiMatKhau = KiemTraQuyTacMatKhau(request.MatKhauMoi);
        if (loiMatKhau.Count > 0)
        {
            return BadRequest(new
            {
                message = "Mật khẩu mới không đạt yêu cầu",
                chiTiet = loiMatKhau
            });
        }

        
        // Nhập lại mật khẩu mới phải trùng khớp
       
        if (request.MatKhauMoi != request.NhapLaiMatKhauMoi)
        {
            return BadRequest(new { message = "Nhập lại mật khẩu không khớp" });
        }

        
        //  Tìm tài khoản theo Email + kiểm tra SĐT
        
        User? user;
        try
        {
            string emailInput = request.Email.Trim();
            user = await _context.Users
                .Include(u => u.NhanVien)
                .FirstOrDefaultAsync(u => u.Username == emailInput);
        }
        catch (Exception)
        {
        
            return StatusCode(503, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }

       
        // Email phải tồn tại & SĐT phải khớp
        // (Gộp chung để không lộ thông tin tài khoản)
       
        string sdtInput = request.SoDienThoai.Trim();
        if (user == null || user.NhanVien == null || user.NhanVien.Sdt != sdtInput)
        {
            return BadRequest(new { message = "Email hoặc số điện thoại không chính xác" });
        }

       
        // Tài khoản phải đang hoạt động
     
        if (user.IsActive == false)
        {
            return BadRequest(new { message = "Tài khoản đã bị vô hiệu hóa, vui lòng liên hệ quản trị viên" });
        }

        
        //  Mật khẩu mới không được trùng mật khẩu hiện tại
       
        if (BCrypt.Net.BCrypt.Verify(request.MatKhauMoi, user.PasswordHash))
        {
            return BadRequest(new { message = "Mật khẩu mới không được trùng với mật khẩu hiện tại" });
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

        return Ok(new { message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại" });
    }

   
    // Kiểm tra quy tắc mật khẩu (trả về danh sách lỗi)
    
    private static List<string> KiemTraQuyTacMatKhau(string matKhau)
    {
        var loi = new List<string>();

        if (matKhau.Length < 8 || matKhau.Length > 12)
            loi.Add("Mật khẩu phải có độ dài từ 8 đến 12 ký tự");

        if (!System.Text.RegularExpressions.Regex.IsMatch(matKhau, @"[a-z]"))
            loi.Add("Mật khẩu phải có ít nhất 1 chữ thường (a-z)");

        if (!System.Text.RegularExpressions.Regex.IsMatch(matKhau, @"[A-Z]"))
            loi.Add("Mật khẩu phải có ít nhất 1 chữ hoa (A-Z)");

        if (!System.Text.RegularExpressions.Regex.IsMatch(matKhau, @"[0-9]"))
            loi.Add("Mật khẩu phải có ít nhất 1 chữ số (0-9)");

        if (!System.Text.RegularExpressions.Regex.IsMatch(matKhau, @"[!@#$%^&*()\-_=+\[\]{};:'"",.<>/?\\|`~]"))
            loi.Add("Mật khẩu phải có ít nhất 1 ký tự đặc biệt (!@#$%...)");

        if (matKhau.Contains(' '))
            loi.Add("Mật khẩu không được chứa khoảng trắng");

        return loi;
    }
}
