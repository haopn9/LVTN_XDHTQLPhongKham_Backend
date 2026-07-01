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
public class NhanSuController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public NhanSuController(QuanLyPhongKhamDbContext context, IConfiguration configuration, IMemoryCache cache)
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

    public class ThemNhanVienRequest
    {
        public string MaNV      { get; set; } = string.Empty;
        public string HoTen     { get; set; } = string.Empty;
        public string Sdt       { get; set; } = string.Empty;
        public string Email     { get; set; } = string.Empty;  // BẮT BUỘC — dùng làm username đăng nhập
        public string? ChuyenMon { get; set; }
        public string Password  { get; set; } = string.Empty;
        public int RoleID       { get; set; }
        public bool IsActive    { get; set; }
    }

    public class CapNhatNhanVienRequest
    {
        public string HoTen     { get; set; } = string.Empty;
        public string Sdt       { get; set; } = string.Empty;
        public string Email     { get; set; } = string.Empty;  // BẮT BUỘC — đồng bộ làm username
        public string? ChuyenMon { get; set; }
        public string? Password { get; set; }
        public int RoleID       { get; set; }
        public bool IsActive    { get; set; }
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

    // GET api/nhan-vien  —  Lấy danh sách nhân viên
    //Lấy danh sách nhân viên có hỗ trợ lọc theo trạng thái, tìm kiếm và phân trang.
    //Mặc định trả về danh sách nhân viên còn hoạt động.
   
    [HttpGet("api/nhan-su")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> LayDanhSachNhanVien(
        [FromQuery] string status = "active",
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? roleID = null)
    {
        try
        {
            // kiểm tra status
            if (status != "active" && status != "inactive")
            {
                return BadRequest(new { message = "Giá trị status không hợp lệ. Chỉ chấp nhận: active | inactive" });
            }

            // kiểm tra page và limit
            if (page <= 0 || limit <= 0)
            {
                return BadRequest(new { message = "Giá trị phân trang không hợp lệ" });
            }

            // Giới hạn tối đa 100 bản ghi mỗi trang
            if (limit > 100) limit = 100;

           
            // Xây dựng query
            bool isActive = status == "active";

            var query = _context.NhanViens
                .Include(nv => nv.User)
                    .ThenInclude(u => u!.Role)
                .Where(nv => nv.User != null && nv.User.IsActive == isActive);

            // Lọc theo roleID
            if (roleID.HasValue)
            {
                query = query.Where(nv => nv.User!.RoleId == roleID.Value);
            }

            // Tìm kiếm theo Họ tên hoặc Mã nhân viên
            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchTrim = search.Trim().ToLower();
                query = query.Where(nv =>
                    nv.HoTen.ToLower().Contains(searchTrim) ||
                    nv.MaNv.ToLower().Contains(searchTrim));
            }

        
            // Đếm tổng số bản ghi thỏa điều kiệ
            int total = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)total / limit);

          
            // Phân trang và lấy dữ liệu
            var danhSach = await query
                .OrderBy(nv => nv.MaNv)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(nv => new
                {
                    maNV     = nv.MaNv,
                    hoTen    = nv.HoTen,
                    sdt      = nv.Sdt,
                    email    = nv.Email,
                    chuyenMon = nv.ChuyenMon,
                    username = nv.User!.Username,
                    roleID   = nv.User.RoleId,
                    roleName = nv.User.Role!.RoleName,
                    isActive = nv.User.IsActive
                })
                .ToListAsync();

            // Thêm số thứ tự (tính theo trang hiện tại)
            var data = danhSach.Select((item, index) => new
            {
                stt      = (page - 1) * limit + index + 1,
                item.maNV,
                item.hoTen,
                item.sdt,
                item.email,
                item.chuyenMon,
                item.username,
                item.roleID,
                item.roleName,
                item.isActive
            });

            return Ok(new
            {
                data,
                pagination = new
                {
                    page,
                    limit,
                    total,
                    totalPages
                },
                filter = new
                {
                    status,
                    search = search ?? (object?)null,
                    roleID = roleID ?? (object?)null
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // POST api/nhan-vien  —  Thêm nhân viên
    // Admin tạo tài khoản nhân viên mới vào hệ thống.
    // Đồng thời tạo bản ghi trong cả 2 bảng: Users và NhanVien.
    // Mật khẩu được hash BCrypt trước khi lưu vào database.
    [HttpPost("api/nhan-su")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ThemNhanVien([FromBody] ThemNhanVienRequest request)
    {
        try
        {
          
            // VALIDATE CÁC TRƯỜNG BẮT BUỘC

            // Không nhập mã nhân viên
            if (string.IsNullOrWhiteSpace(request.MaNV))
                return BadRequest(new { message = "Vui lòng nhập Mã nhân viên!" });

            // Không nhập họ tên
            if (string.IsNullOrWhiteSpace(request.HoTen))
                return BadRequest(new { message = "Vui lòng nhập Họ tên nhân viên!" });

            // Không nhập số điện thoại
            if (string.IsNullOrWhiteSpace(request.Sdt))
                return BadRequest(new { message = "Vui lòng nhập Số điện thoại!" });

            // Không nhập email (email dùng làm tên đăng nhập)
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Vui lòng nhập Tên đăng nhập tài khoản!" });

            // Không nhập mật khẩu
            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Vui lòng nhập Mật khẩu tài khoản!" });

            // VALIDATE ĐỊNH DẠNG & RÀNG BUỘC

            // Không chứa khoảng trắng trong mã nhân viên
            if (request.MaNV.Contains(' '))
                return BadRequest(new { message = "Mã nhân viên không được chứa khoảng trắng. Vui lòng nhập lại!" });

            // Mã nhân viên đã tồn tại
            bool maNvExists = await _context.NhanViens.AnyAsync(nv => nv.MaNv == request.MaNV);
            if (maNvExists)
                return Conflict(new { message = "Mã nhân viên đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại mã khác!" });

            // Số điện thoại sai định dạng (10 chữ số, bắt đầu bằng 0, không chứa khoảng trắng)
            if (!Regex.IsMatch(request.Sdt, @"^0\d{9}$"))
                return BadRequest(new { message = "Số điện thoại không đúng định dạng (phải gồm đúng 10 chữ số và bắt đầu bằng số 0, không chứa khoảng trắng)!" });

            // Số điện thoại đã tồn tại
            bool sdtExists = await _context.NhanViens.AnyAsync(nv => nv.Sdt == request.Sdt);
            if (sdtExists)
                return Conflict(new { message = "Số điện thoại này đã được sử dụng bởi nhân viên khác. Vui lòng kiểm tra & nhập lại số khác!" });

            // Email sai định dạng (email bắt buộc vì dùng làm username)
            if (request.Email.Contains(' ') || !Regex.IsMatch(request.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return BadRequest(new { message = "Địa chỉ Email không đúng định dạng (example@mail.com, không chứa khoảng trắng)!" });

            // Email đã tồn tại trong bảng NhanVien
            bool emailExists = await _context.NhanViens.AnyAsync(nv => nv.Email == request.Email);
            if (emailExists)
                return Conflict(new { message = "Địa chỉ Email này đã được sử dụng bởi nhân viên khác. Vui lòng kiểm tra & nhập lại email khác!" });

            // Tên đăng nhập (= email) đã tồn tại trong bảng Users
            bool usernameExists = await _context.Users.AnyAsync(u => u.Username == request.Email);
            if (usernameExists)
                return Conflict(new { message = "Tên đăng nhập này đã được sử dụng bởi tài khoản khác. Vui lòng kiểm tra & nhập lại tên đăng nhập khác!" });

            // Mật khẩu chứa khoảng trắng
            if (request.Password.Contains(' '))
                return BadRequest(new { message = "Mật khẩu đăng nhập không được chứa khoảng trắng. Vui lòng nhập lại!" });

            // Chuyên môn phải tồn tại trong Danh mục khoa (ChuyenMon = TenKhoa)
            DanhMucKhoa? khoaTheoChuyenMon = null;
            if (!string.IsNullOrWhiteSpace(request.ChuyenMon))
            {
                khoaTheoChuyenMon = await _context.DanhMucKhoas
                    .FirstOrDefaultAsync(k => k.TenKhoa == request.ChuyenMon.Trim());
                if (khoaTheoChuyenMon == null)
                    return BadRequest(new { message = "Chuyên môn không hợp lệ. Vui lòng chọn chuyên môn từ danh mục khoa!" });
            }

            // RoleID không hợp lệ
            int[] validRoles = { 1, 2, 3, 4, 5 };
            if (!validRoles.Contains(request.RoleID))
                return BadRequest(new { message = "Vai trò không hợp lệ. Vui lòng chọn lại!" });

            
            // TẠO BẢN GHI TRONG DATABASE
            // Tạo User (tài khoản)
            var newUser = new User
            {
                Username     = request.Email,     // Email làm username đăng nhập
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RoleId       = request.RoleID,
                IsActive     = request.IsActive
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync(); // Lưu để lấy UserID tự sinh

            // Tạo NhanVien (hồ sơ)
            var newNhanVien = new NhanVien
            {
                MaNv      = request.MaNV,
                UserId    = newUser.UserId,
                HoTen     = request.HoTen,
                Sdt       = request.Sdt,
                Email     = request.Email,
                ChuyenMon = khoaTheoChuyenMon?.TenKhoa,   // ChuyenMon = TenKhoa từ DanhMucKhoa
                MaKhoa    = khoaTheoChuyenMon?.MaKhoa     // Tự động gán MaKhoa tương ứng
            };

            _context.NhanViens.Add(newNhanVien);
            await _context.SaveChangesAsync();

            // Lấy tên vai trò để trả về response
            var role = await _context.Roles.FindAsync(request.RoleID);

            return StatusCode(201, new
            {
                message = "Thêm nhân viên thành công",
                data = new
                {
                    maNV      = newNhanVien.MaNv,
                    hoTen     = newNhanVien.HoTen,
                    chuyenMon = newNhanVien.ChuyenMon,
                    maKhoa    = newNhanVien.MaKhoa,
                    username  = newUser.Username,
                    roleName  = role?.RoleName ?? "",
                    isActive  = newUser.IsActive
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // PUT api/nhan-vien/{maNV}  —  Cập nhật nhân viên
    // Admin cập nhật thông tin nhân viên. Mã nhân viên (maNV) truyền qua URL
    // và là trường chỉ đọc — không được thay đổi.
    // Nếu admin nhập mật khẩu mới → server hash BCrypt và cập nhật.
    // Nếu admin để trống mật khẩu → giữ nguyên mật khẩu cũ.
    
    [HttpPut("api/nhan-su/{maNV}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CapNhatNhanVien(string maNV, [FromBody] CapNhatNhanVienRequest request)
    {
        try
        {
            // KIỂM TRA NHÂN VIÊN TỒN TẠI
            // maNV trên URL không tồn tại
            var nhanVien = await _context.NhanViens
                .Include(nv => nv.User)
                    .ThenInclude(u => u!.Role)
                .FirstOrDefaultAsync(nv => nv.MaNv == maNV);

            if (nhanVien == null || nhanVien.User == null)
                return NotFound(new { message = "Không tìm thấy nhân viên cần cập nhật" });

            // VALIDATE CÁC TRƯỜNG BẮT BUỘC
            // Không nhập họ tên
            if (string.IsNullOrWhiteSpace(request.HoTen))
                return BadRequest(new { message = "Vui lòng nhập Họ tên nhân viên!" });

            // Không nhập số điện thoại
            if (string.IsNullOrWhiteSpace(request.Sdt))
                return BadRequest(new { message = "Vui lòng nhập Số điện thoại!" });

            // Số điện thoại sai định dạng
            if (!Regex.IsMatch(request.Sdt, @"^0\d{9}$"))
                return BadRequest(new { message = "Số điện thoại không đúng định dạng (phải gồm đúng 10 chữ số và bắt đầu bằng số 0, không chứa khoảng trắng)!" });

            // Số điện thoại đã được sử dụng bởi nhân viên KHÁC
            bool sdtDuplicate = await _context.NhanViens
                .AnyAsync(nv => nv.Sdt == request.Sdt && nv.MaNv != maNV);
            if (sdtDuplicate)
                return Conflict(new { message = "Số điện thoại này đã được sử dụng bởi nhân viên khác. Vui lòng kiểm tra & nhập lại số khác!" });

            // Không nhập email (email dùng làm tên đăng nhập)
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Vui lòng nhập Tên đăng nhập tài khoản!" });

            // Email sai định dạng
            if (request.Email.Contains(' ') || !Regex.IsMatch(request.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return BadRequest(new { message = "Địa chỉ Email không đúng định dạng (example@mail.com, không chứa khoảng trắng)!" });

            // Email đã được sử dụng bởi nhân viên KHÁC
            bool emailDuplicate = await _context.NhanViens
                .AnyAsync(nv => nv.Email == request.Email && nv.MaNv != maNV);
            if (emailDuplicate)
                return Conflict(new { message = "Địa chỉ Email này đã được sử dụng bởi nhân viên khác. Vui lòng kiểm tra & nhập lại email khác!" });

            // Tên đăng nhập (= email) đã được sử dụng bởi tài khoản KHÁC
            bool usernameDuplicate = await _context.Users
                .AnyAsync(u => u.Username == request.Email && u.UserId != nhanVien.UserId);
            if (usernameDuplicate)
                return Conflict(new { message = "Tên đăng nhập này đã được sử dụng bởi tài khoản khác. Vui lòng kiểm tra & nhập lại tên đăng nhập khác!" });

            // Mật khẩu chứa khoảng trắng (nếu có nhập)
            if (!string.IsNullOrEmpty(request.Password) && request.Password.Contains(' '))
                return BadRequest(new { message = "Mật khẩu đăng nhập không được chứa khoảng trắng. Vui lòng nhập lại!" });

            // Chuyên môn phải tồn tại trong Danh mục khoa (ChuyenMon = TenKhoa)
            DanhMucKhoa? khoaTheoChuyenMon = null;
            if (!string.IsNullOrWhiteSpace(request.ChuyenMon))
            {
                khoaTheoChuyenMon = await _context.DanhMucKhoas
                    .FirstOrDefaultAsync(k => k.TenKhoa == request.ChuyenMon.Trim());
                if (khoaTheoChuyenMon == null)
                    return BadRequest(new { message = "Chuyên môn không hợp lệ. Vui lòng chọn chuyên môn từ danh mục khoa!" });
            }

            // roleID không hợp lệ
            int[] validRoles = { 1, 2, 3, 4, 5 };
            if (!validRoles.Contains(request.RoleID))
                return BadRequest(new { message = "Vai trò không hợp lệ. Vui lòng chọn lại!" });

          
            // CẬP NHẬT DỮ LIỆU
            // Cập nhật bảng NhanVien
            nhanVien.HoTen     = request.HoTen;
            nhanVien.Sdt       = request.Sdt;
            nhanVien.Email     = request.Email;
            nhanVien.ChuyenMon = khoaTheoChuyenMon?.TenKhoa;   // ChuyenMon = TenKhoa từ DanhMucKhoa
            nhanVien.MaKhoa    = khoaTheoChuyenMon?.MaKhoa;    // Tự động gán MaKhoa tương ứng

            // Cập nhật bảng Users — Email đồng bộ làm Username
            nhanVien.User!.Username = request.Email;
            nhanVien.User.RoleId    = request.RoleID;
            nhanVien.User.IsActive  = request.IsActive;

            // Cập nhật mật khẩu nếu có nhập mới (để trống → giữ nguyên)
            if (!string.IsNullOrEmpty(request.Password))
            {
                nhanVien.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            }

            await _context.SaveChangesAsync();

            // Lấy lại roleName sau khi cập nhật
            var role = await _context.Roles.FindAsync(request.RoleID);

            return Ok(new
            {
                message = "Cập nhật thông tin nhân viên thành công",
                data = new
                {
                    maNV      = nhanVien.MaNv,
                    hoTen     = nhanVien.HoTen,
                    chuyenMon = nhanVien.ChuyenMon,
                    maKhoa    = nhanVien.MaKhoa,
                    username  = nhanVien.User.Username,
                    roleName  = role?.RoleName ?? "",
                    isActive  = nhanVien.User.IsActive
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // DELETE api/nhan-vien/{maNV}  —  Xóa nhân viên
    // Admin xóa nhân viên khỏi hệ thống. Áp dụng chiến lược xóa thông minh:
    // - Nếu CHƯA có dữ liệu liên quan → Xóa cứng (Hard Delete)
    // - Nếu ĐÃ có dữ liệu liên quan → Đề nghị tạm khóa thay thế
    
    [HttpDelete("api/nhan-su/{maNV}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> XoaNhanVien(string maNV)
    {
        try
        {
            // KIỂM TRA NHÂN VIÊN TỒN TẠI
            // maNV trên URL không tồn tại
            var nhanVien = await _context.NhanViens
                .Include(nv => nv.User)
                .FirstOrDefaultAsync(nv => nv.MaNv == maNV);

            if (nhanVien == null)
                return NotFound(new { message = "Không tìm thấy nhân viên cần xóa" });

            // ADMIN TỰ XÓA CHÍNH MÌNH
            string? userIdClaim = User.FindFirstValue("userID");
            if (!string.IsNullOrEmpty(userIdClaim)
                && int.TryParse(userIdClaim, out int currentUserId)
                && nhanVien.UserId == currentUserId)
            {
                return BadRequest(new { message = "Không thể xóa tài khoản đang đăng nhập" });
            }

            // KIỂM TRA DỮ LIỆU LIÊN QUAN
            bool hasPhieuKham = await _context.PhieuKhams
                .AnyAsync(pk => pk.MaNv == maNV);

            bool hasHoaDon = await _context.HoaDons
                .AnyAsync(hd => hd.MaNv == maNV);

            // CÓ DỮ LIỆU LIÊN QUAN → KHÔNG XÓA, GỢI Ý TẠM KHÓA
            if (hasPhieuKham || hasHoaDon)
            {
                return Conflict(new
                {
                    message         = "Không thể xóa nhân viên này vì đã có dữ liệu trong hệ thống (phiếu khám, hóa đơn...)",
                    suggestion      = "Bạn có thể tạm khóa tài khoản nhân viên này thay thế. Sử dụng API cập nhật với isActive = false",
                    suggestedAction = $"PUT api/nhan-su/{maNV}  với  isActive: false"
                });
            }

            // KHÔNG CÓ DỮ LIỆU LIÊN QUAN → XÓA CỨNG
            var userToDelete = nhanVien.User;

            // Xóa NhanVien trước (vì NhanVien FK → Users)
            _context.NhanViens.Remove(nhanVien);
            await _context.SaveChangesAsync();

            // Xóa User (tài khoản đăng nhập)
            if (userToDelete != null)
            {
                _context.Users.Remove(userToDelete);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                message    = "Xóa nhân viên thành công",
                deletedMaNV = maNV
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

}
