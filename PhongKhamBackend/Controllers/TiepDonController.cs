using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Route("api/TiepDon")]
[Authorize]   // Tất cả endpoint trong controller này đều yêu cầu đăng nhập
public class TiepDonController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;

    public TiepDonController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }

    // DTO
    /// DTO cho API Tiếp nhận bệnh nhân (tạo phiếu khám)
    
    public class TiepNhanBenhNhanRequest
    {
        // -- Thông tin bệnh nhân (bảng BenhNhan) --
        public string? MaBN       { get; set; }                    // Mã BN cũ — bỏ trống / null nếu BN mới
        public string  HoTen      { get; set; } = string.Empty;   // Họ và tên (CHỮ HOA) — BẮT BUỘC
        public string  NgaySinh   { get; set; } = string.Empty;   // Ngày sinh dd/MM/yyyy — BẮT BUỘC
        public string  GioiTinh   { get; set; } = string.Empty;   // "Nam" | "Nữ" | "Khác" — BẮT BUỘC
        public string  Sdt        { get; set; } = string.Empty;   // Số điện thoại 10 chữ số — BẮT BUỘC
        public string? DiaChi     { get; set; }                    // Địa chỉ — Tuỳ chọn
        public string? TienSuBenh { get; set; }                    // Tiền sử bệnh — Tuỳ chọn

        // -- Thông tin phiếu khám (bảng PhieuKham) --
        public string?       MaNVBacSi    { get; set; }           // Mã NV bác sĩ chỉ định — Tuỳ chọn
        public string        LyDoKham     { get; set; } = string.Empty;   // Lý do đến khám — BẮT BUỘC
        public List<string>? DanhSachICD  { get; set; }           // Mảng mã ICD chẩn đoán ban đầu — Tuỳ chọn
    }

    //  GET api/TiepDon/tra-cuu?sdt={soDienThoai}
    //    — Tra cứu bệnh nhân cũ theo SĐT
    /// Lễ tân nhập SĐT để kiểm tra bệnh nhân đã có hồ sơ chưa.
    /// Nếu tìm thấy → trả HTTP 200 với found = true kèm dữ liệu BN.
    /// Nếu không tìm thấy → trả HTTP 200 với found = false (luồng bình thường).
  
    [HttpGet("tra-cuu")]
    [Authorize(Roles = "LeTan,Admin")]
    public async Task<IActionResult> TraCuuBenhNhan([FromQuery] string? sdt)
    {
        try
        {
            
            // KIỂM TRA THAM SỐ SDT KHÔNG RỖNG
        

            // Tham số sdt bị rỗng hoặc thiếu
            if (string.IsNullOrWhiteSpace(sdt))
                return BadRequest(new { message = "Vui lòng nhập số điện thoại để tra cứu!" });

            // B3: VALIDATE ĐỊNH DẠNG SDT (10 chữ số, bắt đầu bằng 0)
            
            // Số điện thoại sai định dạng
            if (!Regex.IsMatch(sdt.Trim(), @"^0\d{9}$"))
                return BadRequest(new { message = "Số điện thoại không đúng định dạng (phải gồm đúng 10 chữ số và bắt đầu bằng số 0)!" });

         
            // SELECT TỪ BẢNG BenhNhan WHERE SDT = @sdt

            var benhNhan = await _context.BenhNhans
                .FirstOrDefaultAsync(bn => bn.Sdt == sdt.Trim());

           
            // KHÔNG TÌM THẤY → HTTP 200 với found = false
            // đây là luồng bình thường, KHÔNG trả 404          
            if (benhNhan == null)
            {
                return Ok(new
                {
                    found = false,
                    data = (object?)null,
                    message = "Không tìm thấy hồ sơ bệnh nhân với số điện thoại này. Đây là bệnh nhân mới."
                });
            }

            // TÌM THẤY → HTTP 200 với found = true kèm dữ liệu BN
            return Ok(new
            {
                found = true,
                data = new
                {
                    maBN       = benhNhan.MaBn,
                    hoTen      = benhNhan.HoTen,
                    ngaySinh   = benhNhan.NgaySinh?.ToString("dd/MM/yyyy"),
                    gioiTinh   = benhNhan.GioiTinh,
                    sdt        = benhNhan.Sdt,
                    diaChi     = benhNhan.DiaChi,
                    tienSuBenh = benhNhan.TienSuBenh
                }
            });
        }
        catch (Exception)
        {
            // Hệ thống không kết nối được API hoặc Database
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // POST api/TiepDon  —  Tiếp nhận bệnh nhân (Tạo phiếu khám)
    /// Lễ tân hoàn tất form tiếp đón và lưu lại để tạo phiếu khám mới.
    /// Hỗ trợ hai tình huống:
    ///   A — BN cũ (có maBN): chỉ tạo PhieuKham, liên kết maBN có sẵn.
    ///       Cập nhật diaChi/tienSuBenh nếu có thay đổi.
    ///   B — BN mới (maBN rỗng): server tự sinh maBN, INSERT BenhNhan
    ///       rồi tạo PhieuKham.
    /// MaNV (lễ tân) được lấy tự động từ JWT token.
    /// Toàn bộ B8→B11 chạy trong transaction.
    [HttpPost]
    [Authorize(Roles = "LeTan,Admin")]
    public async Task<IActionResult> TiepNhanBenhNhan([FromBody] TiepNhanBenhNhanRequest request)
    {
        try
        {
            // XÁC THỰC TOKEN, LẤY MaNV CỦA LỄ TÂN TỪ TOKEN
            string? userIdClaim = User.FindFirstValue("userID");
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });
            }

            // Lấy MaNV từ bảng NhanVien theo UserID
            var nhanVienTiepDon = await _context.NhanViens
                .FirstOrDefaultAsync(nv => nv.UserId == userId);

            if (nhanVienTiepDon == null)
            {
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });
            }

            string maNvTiepDon = nhanVienTiepDon.MaNv;

            // VALIDATE CÁC TRƯỜNG BẮT BUỘC

            // Không nhập họ tên
            if (string.IsNullOrWhiteSpace(request.HoTen))
                return BadRequest(new { message = "Vui lòng nhập Họ và tên bệnh nhân!" });

            // Không nhập số điện thoại
            if (string.IsNullOrWhiteSpace(request.Sdt))
                return BadRequest(new { message = "Vui lòng nhập Số điện thoại!" });

            // Không nhập ngày sinh
            if (string.IsNullOrWhiteSpace(request.NgaySinh))
                return BadRequest(new { message = "Vui lòng nhập Ngày tháng năm sinh!" });

            // Không nhập lý do khám
            if (string.IsNullOrWhiteSpace(request.LyDoKham))
                return BadRequest(new { message = "Vui lòng nhập Lý do đến khám!" });

            // Giới tính không hợp lệ
            string[] gioiTinhHopLe = { "Nam", "Nữ", "Khác" };
            if (string.IsNullOrWhiteSpace(request.GioiTinh) || !gioiTinhHopLe.Contains(request.GioiTinh.Trim()))
                return BadRequest(new { message = "Giới tính không hợp lệ. Chỉ chấp nhận: Nam, Nữ, Khác!" });

            // VALIDATE ĐỊNH DẠNG SĐT

            // Số điện thoại sai định dạng
            if (!Regex.IsMatch(request.Sdt, @"^0\d{9}$"))
                return BadRequest(new { message = "Số điện thoại không đúng định dạng (phải gồm đúng 10 chữ số và bắt đầu bằng số 0, không chứa khoảng trắng)!" });

            // VALIDATE NGÀY SINH (dd/MM/yyyy)

            // Hỗ trợ cả dd/MM/yyyy và dd-MM-yyyy
            string[] dateFormats = { "dd/MM/yyyy", "dd-MM-yyyy" };
            if (!DateOnly.TryParseExact(request.NgaySinh.Trim(), dateFormats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateOnly ngaySinh))
            {
                // Ngày sinh sai định dạng
                return BadRequest(new { message = "Ngày sinh không hợp lệ. Vui lòng kiểm tra lại!" });
            }

            // Năm phải từ 1900 trở lên
            if (ngaySinh.Year < 1900)
                return BadRequest(new { message = "Ngày sinh không hợp lệ. Vui lòng kiểm tra lại!" });

            // Ngày sinh không thể là ngày trong tương lai
            if (ngaySinh > DateOnly.FromDateTime(DateTime.Now))
                return BadRequest(new { message = "Ngày sinh không thể là ngày trong tương lai!" });

            // VALIDATE GIỚI TÍNH
            // (Đã validate ở B2 — 9.8.8)

            // B6: VALIDATE danhSachICD NẾU CÓ
            var danhSachICD = request.DanhSachICD?
                .Where(icd => !string.IsNullOrWhiteSpace(icd))
                .Select(icd => icd.Trim())
                .ToList() ?? new List<string>();

            if (danhSachICD.Count > 0)
            {
                foreach (var maICD in danhSachICD)
                {
                    bool icdExists = await _context.DanhMucIcds
                        .AnyAsync(icd => icd.MaIcd == maICD);
                    if (!icdExists)
                    {
                        // 9.8.11  Mã ICD không tồn tại
                        return BadRequest(new { message = $"Mã bệnh ICD '{maICD}' không tồn tại trong danh mục. Vui lòng kiểm tra lại!" });
                    }
                }
            }


            // VALIDATE maNVBacSi NẾU CÓ

            //  maNVBacSi không tồn tại hoặc không phải bác sĩ
            if (!string.IsNullOrWhiteSpace(request.MaNVBacSi))
            {
                var bacSi = await _context.NhanViens
                    .Include(nv => nv.User)
                    .FirstOrDefaultAsync(nv => nv.MaNv == request.MaNVBacSi.Trim());

                if (bacSi == null || bacSi.User == null || bacSi.User.RoleId != 2)
                    return BadRequest(new { message = "Bác sĩ chỉ định không hợp lệ hoặc không tồn tại trong hệ thống!" });
            }

            // B8 → B11: CHẠY TRONG TRANSACTION
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                string maBN;
                bool isNewPatient;

                if (string.IsNullOrWhiteSpace(request.MaBN))
                {
                    
                    // BỆNH NHÂN MỚI → Sinh maBN mới và INSERT BenhNhan
                
                    isNewPatient = true;

                    // Tạo BN mới: không cho phép trùng SĐT
                    bool sdtExists = await _context.BenhNhans
                        .AnyAsync(bn => bn.Sdt == request.Sdt.Trim());
                    if (sdtExists)
                    {
                        await transaction.RollbackAsync();
                        // SDT đã tồn tại
                        return Conflict(new { message = "Số điện thoại này đã có hồ sơ bệnh nhân trong hệ thống. Vui lòng tra cứu theo SĐT và dùng luồng bệnh nhân cũ!" });
                    }

                    // Sinh maBN: "BN" + yyMMdd + stt 3 chữ số
                    string today = DateTime.Now.ToString("yyMMdd");
                    string prefix = $"BN{today}";

                    int count = await _context.BenhNhans
                        .CountAsync(bn => bn.MaBn.StartsWith(prefix));
                    int stt = count + 1;

                    maBN = $"{prefix}{stt:D3}";  // VD: BN260609001

                    // INSERT bệnh nhân mới
                    var newBenhNhan = new BenhNhan
                    {
                        MaBn       = maBN,
                        HoTen      = request.HoTen.Trim(),
                        NgaySinh   = ngaySinh,
                        GioiTinh   = request.GioiTinh.Trim(),
                        Sdt        = request.Sdt.Trim(),
                        DiaChi     = string.IsNullOrWhiteSpace(request.DiaChi) ? null : request.DiaChi.Trim(),
                        TienSuBenh = string.IsNullOrWhiteSpace(request.TienSuBenh) ? null : request.TienSuBenh.Trim()
                    };

                    _context.BenhNhans.Add(newBenhNhan);
                    await _context.SaveChangesAsync();
                }
                else
                {

                    // BỆNH NHÂN CŨ → Kiểm tra maBN tồn tại

                    isNewPatient = false;
                    maBN = request.MaBN.Trim();

                    // maBN truyền lên nhưng không tồn tại
                    var existingBN = await _context.BenhNhans
                        .FirstOrDefaultAsync(bn => bn.MaBn == maBN);

                    if (existingBN == null)
                    {
                        await transaction.RollbackAsync();
                        return NotFound(new { message = "Không tìm thấy hồ sơ bệnh nhân với mã BN đã cung cấp!" });
                    }

                    // Cập nhật diaChi, tienSuBenh nếu có thay đổi
                    if (!string.IsNullOrWhiteSpace(request.DiaChi))
                        existingBN.DiaChi = request.DiaChi.Trim();

                    if (!string.IsNullOrWhiteSpace(request.TienSuBenh))
                        existingBN.TienSuBenh = request.TienSuBenh.Trim();

                    await _context.SaveChangesAsync();
                }

                
                // SINH MÃ PHIẾU KHÁM
                // Format: "PK_" + yyMMdd + "_" + stt 3 chữ số
                string todayPK = DateTime.Now.ToString("yyMMdd");
                string prefixPK = $"PK_{todayPK}_";

                int countPK = await _context.PhieuKhams
                    .CountAsync(pk => pk.MaPhieu.StartsWith(prefixPK));
                int sttPK = countPK + 1;

                string maPhieu = $"{prefixPK}{sttPK:D3}";  // VD: PK_260609_001

                // INSERT PHIẾU KHÁM VỚI TrangThaiKham = 0
                // MaNV trong PhieuKham lưu MaNV lễ tân (từ token)
                // maNVBacSi chỉ validate, KHÔNG lưu vào PhieuKham ở bước tiếp đón
                var newPhieuKham = new PhieuKham
                {
                    MaPhieu       = maPhieu,
                    MaBn          = maBN,
                    MaNv          = maNvTiepDon,       // MaNV = lễ tân từ token
                    NgayKham      = DateTime.Now,
                    LyDoKham      = request.LyDoKham.Trim(),
                    TrangThaiKham = 0                   // Chờ khám
                };

                _context.PhieuKhams.Add(newPhieuKham);
                await _context.SaveChangesAsync();

                
                // INSERT danhSachICD VÀO ChiTietPhieuKhamICD (NẾU CÓ)
                
                if (danhSachICD.Count > 0)
                {
                    // Load lại phiếu khám kèm navigation MaIcds
                    var phieuKham = await _context.PhieuKhams
                        .Include(pk => pk.MaIcds)
                        .FirstAsync(pk => pk.MaPhieu == maPhieu);

                    foreach (var maICD in danhSachICD)
                    {
                        var icdEntity = await _context.DanhMucIcds
                            .FirstAsync(icd => icd.MaIcd == maICD);
                        phieuKham.MaIcds.Add(icdEntity);
                    }

                    await _context.SaveChangesAsync();
                }

                // Commit transaction
                await transaction.CommitAsync();

                
                // TRẢ VỀ HTTP 201 KÈM THÔNG TIN PHIẾU KHÁM VỪA TẠO
                
                return StatusCode(201, new
                {
                    message = "Tiếp nhận bệnh nhân thành công",
                    data = new
                    {
                        maPhieu        = maPhieu,
                        maBN           = maBN,
                        hoTen          = request.HoTen.Trim(),
                        ngayKham       = newPhieuKham.NgayKham?.ToString("o"),   // ISO 8601
                        lyDoKham       = request.LyDoKham.Trim(),
                        trangThaiKham  = 0,
                        danhSachICD    = danhSachICD,
                        isNewPatient   = isNewPatient
                    }
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception)
        {
            // Hệ thống không kết nối được API hoặc Database
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // GET api/TiepDon/danh-sach
    // Lấy danh sách bệnh nhân đã tiếp đón
    
    
    // Trả về danh sách phiếu khám (mặc định trong ngày) kèm thông tin
    // tóm tắt bệnh nhân. Hỗ trợ lọc theo trạng thái, bác sĩ, ngày
    // khám, tìm kiếm theo họ tên / SĐT / mã BN, và phân trang.
    
    [HttpGet("danh-sach")]
    [Authorize(Roles = "LeTan,BacSi,Admin")]
    public async Task<IActionResult> LayDanhSachTiepDon(
        [FromQuery] string? search = null,
        [FromQuery] string? maBacSi = null,
        [FromQuery] int? trangThai = null,
        [FromQuery] string? ngayKham = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 100)
    {
        try
        {
            
            // VALIDATE THAM SỐ ĐẦU VÀO
            

            //  Giá trị trangThai không hợp lệ
            if (trangThai.HasValue && !new[] { 0, 1, 2, 3 }.Contains(trangThai.Value))
                return BadRequest(new { message = "Giá trị trạng thái không hợp lệ. Chỉ chấp nhận: 0 | 1 | 2 | 3" });

            //  Định dạng ngayKham không hợp lệ
            DateOnly ngayLoc;
            if (!string.IsNullOrWhiteSpace(ngayKham))
            {
                if (!DateOnly.TryParseExact(ngayKham.Trim(), "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out ngayLoc))
                {
                    return BadRequest(new { message = "Định dạng ngày lọc không hợp lệ. Vui lòng nhập theo định dạng YYYY-MM-DD!" });
                }
            }
            else
            {
                // Mặc định: ngày hiện tại
                ngayLoc = DateOnly.FromDateTime(DateTime.Now);
            }

            //  Giá trị page hoặc limit không hợp lệ
            if (page <= 0 || limit <= 0)
                return BadRequest(new { message = "Giá trị phân trang không hợp lệ" });

            // Cap limit tối đa 200
            if (limit > 200) limit = 200;

            
            // XÂY DỰNG QUERY
            

            // Lọc theo ngày khám (so sánh phần DATE)
            DateTime ngayBatDau = ngayLoc.ToDateTime(TimeOnly.MinValue);
            DateTime ngayKetThuc = ngayLoc.ToDateTime(TimeOnly.MaxValue);

            var query = _context.PhieuKhams
                .Include(pk => pk.MaBnNavigation)       // BenhNhan
                .Include(pk => pk.MaNvNavigation)       // NhanVien (MaNV dùng chung)
                .Where(pk => pk.NgayKham >= ngayBatDau && pk.NgayKham <= ngayKetThuc);

            // Lọc theo trạng thái khám
            if (trangThai.HasValue)
            {
                query = query.Where(pk => pk.TrangThaiKham == trangThai.Value);
            }

            // Lọc theo bác sĩ (dự phòng — qua MaNV khi bác sĩ nhận ca)
            if (!string.IsNullOrWhiteSpace(maBacSi))
            {
                string maBsTrim = maBacSi.Trim();
                query = query.Where(pk => pk.MaNv == maBsTrim);
            }

            // Tìm kiếm theo họ tên, SĐT hoặc mã BN
            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchTrim = search.Trim().ToLower();
                query = query.Where(pk =>
                    (pk.MaBnNavigation != null && pk.MaBnNavigation.HoTen.ToLower().Contains(searchTrim)) ||
                    (pk.MaBnNavigation != null && pk.MaBnNavigation.Sdt != null && pk.MaBnNavigation.Sdt.Contains(searchTrim)) ||
                    (pk.MaBn != null && pk.MaBn.ToLower().Contains(searchTrim)));
            }

                
            // ĐẾM TỔNG VÀ PHÂN TRANG
            
            int total = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)total / limit);

            var danhSach = await query
                .OrderBy(pk => pk.NgayKham)    // ASC theo đặc tả
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(pk => new
                {
                    maPhieu       = pk.MaPhieu,
                    maBN          = pk.MaBn,
                    hoTen         = pk.MaBnNavigation != null ? pk.MaBnNavigation.HoTen : null,
                    ngaySinh      = pk.MaBnNavigation != null && pk.MaBnNavigation.NgaySinh != null
                                        ? pk.MaBnNavigation.NgaySinh.Value.ToString("dd/MM/yyyy")
                                        : null,
                    gioiTinh      = pk.MaBnNavigation != null ? pk.MaBnNavigation.GioiTinh : null,
                    sdt           = pk.MaBnNavigation != null ? pk.MaBnNavigation.Sdt : null,
                    diaChi        = pk.MaBnNavigation != null ? pk.MaBnNavigation.DiaChi : null,
                    lyDoKham      = pk.LyDoKham,
                    ngayKham      = pk.NgayKham,
                    trangThaiKham = pk.TrangThaiKham,
                    maNV          = pk.MaNv,
                    tenNhanVien   = pk.MaNvNavigation != null ? pk.MaNvNavigation.HoTen : null
                })
                .ToListAsync();

            
            // TRẢ VỀ KẾT QUẢ
            
            return Ok(new
            {
                data = danhSach,
                pagination = new
                {
                    page,
                    limit,
                    total,
                    totalPages
                },
                filter = new
                {
                    ngayKham  = ngayLoc.ToString("yyyy-MM-dd"),
                    trangThai = trangThai ?? (object?)null,
                    maBacSi   = string.IsNullOrWhiteSpace(maBacSi) ? (object?)null : maBacSi.Trim(),
                    search    = string.IsNullOrWhiteSpace(search) ? (object?)null : search.Trim()
                }
            });
        }
        catch (Exception)
        {
            // Hệ thống không kết nối được API hoặc Database
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // GET api/TiepDon/{maPhieu}
    // Xem chi tiết hồ sơ bệnh nhân
    
    // Trả về toàn bộ thông tin của một lượt khám theo maPhieu, bao gồm:
    // thông tin cá nhân bệnh nhân, thông tin phiếu khám (sinh hiệu,
    // kết luận, lý do khám), danh sách ICD chẩn đoán, danh sách chỉ
    // định dịch vụ y tế / cận lâm sàng và đơn thuốc đã kê (nếu có).
    
    [HttpGet("{maPhieu}")]
    [Authorize(Roles = "LeTan,BacSi,Admin")]
    public async Task<IActionResult> XemChiTietHoSo(string maPhieu)
    {
        try
        {
            
            // KIỂM TRA maPhieu TỒN TẠI
            
            var phieuKham = await _context.PhieuKhams
                .Include(pk => pk.MaBnNavigation)                          // BenhNhan
                .Include(pk => pk.MaNvNavigation)                          // NhanVien (MaNV dùng chung)
                .Include(pk => pk.MaIcds)                                  // DanhMucICD (many-to-many)
                .Include(pk => pk.DichVuYtes)                              // DichVuYTe (chỉ định CLS)
                    .ThenInclude(dv => dv.MaDvNavigation)                  // ChiTietDichVuYTe (catalog)
                .Include(pk => pk.DonThuocs)                               // DonThuoc
                    .ThenInclude(dt => dt.ChiTietDonThuocs)                // ChiTietDonThuoc
                        .ThenInclude(ct => ct.MaThuocNavigation)           // DanhMucThuoc
                .FirstOrDefaultAsync(pk => pk.MaPhieu == maPhieu);

            //  maPhieu không tồn tại
            if (phieuKham == null)
                return NotFound(new { message = "Không tìm thấy hồ sơ bệnh án. Phiếu khám có thể đã bị xóa hoặc không tồn tại!" });

            // Lấy đơn thuốc đầu tiên (mỗi phiếu khám chỉ có tối đa 1 đơn)
            var donThuoc = phieuKham.DonThuocs.FirstOrDefault();

            var response = new
            {
                // --- Thông tin phiếu khám ---
                maPhieu       = phieuKham.MaPhieu,
                ngayKham      = phieuKham.NgayKham?.ToString("o"),    // ISO 8601
                trangThaiKham = phieuKham.TrangThaiKham,
                lyDoKham      = phieuKham.LyDoKham,

                // --- Thông tin bệnh nhân (từ bảng BenhNhan) ---
                maBN       = phieuKham.MaBn,
                hoTen      = phieuKham.MaBnNavigation?.HoTen,
                ngaySinh   = phieuKham.MaBnNavigation?.NgaySinh?.ToString("dd/MM/yyyy"),
                gioiTinh   = phieuKham.MaBnNavigation?.GioiTinh,
                sdt        = phieuKham.MaBnNavigation?.Sdt,
                diaChi     = phieuKham.MaBnNavigation?.DiaChi,
                tienSuBenh = phieuKham.MaBnNavigation?.TienSuBenh,

                // --- Nhân viên (từ NhanVien JOIN PhieuKham.MaNV) ---
                maNV         = phieuKham.MaNv,
                tenNhanVien  = phieuKham.MaNvNavigation?.HoTen,

                // --- Sinh hiệu (do bác sĩ cập nhật qua API khám bệnh, có thể null) ---
                mach     = phieuKham.Mach,
                nhietDo  = phieuKham.NhietDo,
                huyetAp  = phieuKham.HuyetAp,
                canNang  = phieuKham.CanNang,
                chieuCao = phieuKham.ChieuCao,

                // --- Chẩn đoán (do bác sĩ cập nhật, có thể null/rỗng) ---
                ketLuan = phieuKham.KetLuan,
                danhSachICD = phieuKham.MaIcds.Select(icd => new
                {
                    maICD   = icd.MaIcd,
                    tenBenh = icd.TenBenh
                }).ToList(),

                // --- Chỉ định dịch vụ / cận lâm sàng (từ bảng DichVuYTe) ---
                dichVuYTe = phieuKham.DichVuYtes.Select(dv => new
                {
                    maChiTiet       = dv.MaChiTiet,
                    maDV            = dv.MaDv,
                    tenDV           = dv.MaDvNavigation?.TenDv,
                    ketQua          = dv.KetQua,
                    trangThaiDichVu = dv.TrangThaiDichVu
                }).ToList(),

                // --- Đơn thuốc (từ bảng DonThuoc + ChiTietDonThuoc) ---
                donThuoc = donThuoc == null ? new
                {
                    maDonThuoc     = (string?)null,
                    ngayKeDon      = (string?)null,
                    loiDanDonThuoc = (string?)null,
                    chiTiet        = new List<object>()
                } : new
                {
                    maDonThuoc     = (string?)donThuoc.MaDonThuoc,
                    ngayKeDon      = (string?)donThuoc.NgayKeDon?.ToString("o"),   // ISO 8601
                    loiDanDonThuoc = (string?)donThuoc.LoiDan,
                    chiTiet        = donThuoc.ChiTietDonThuocs.Select(ct => (object)new
                    {
                        maThuoc   = ct.MaThuoc,
                        tenThuoc  = ct.MaThuocNavigation?.TenThuoc,
                        soLuong   = ct.SoLuong,
                        cachDung  = ct.CachDung,
                        donViTinh = ct.MaThuocNavigation?.DonViTinh
                    }).ToList()
                }
            };

            return Ok(response);
        }
        catch (Exception)
        {
            // Hệ thống không kết nối được API hoặc Database
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }
}
