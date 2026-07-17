using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Route("api/TiepDon")]
[Authorize]   // Tất cả endpoint đều yêu cầu đăng nhập
public class TiepDonController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;

    public TiepDonController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }

    // DTO cho API Tiếp nhận bệnh nhân (tạo phiếu khám)
    public class TiepNhanBenhNhanRequest
    {
        // -- Thông tin bệnh nhân (bảng BenhNhan) --
        public string? MaBN       { get; set; }                    // Mã BN cũ — bỏ trống/null nếu là BN mới
        public string  HoTen      { get; set; } = string.Empty;   // Họ và tên (CHỮ HOA) — BẮT BUỘC
        public string  NgaySinh   { get; set; } = string.Empty;   // DD-MM-YYYY — BẮT BUỘC
        public string  GioiTinh   { get; set; } = string.Empty;   // "Nam" | "Nữ" | "Khác" — BẮT BUỘC
        public string  Sdt        { get; set; } = string.Empty;   // 10 chữ số — BẮT BUỘC
        public string? DiaChi     { get; set; }                    // Địa chỉ — Tuỳ chọn
        public string? TienSuBenh { get; set; }                    // Tiền sử bệnh — Tuỳ chọn

        // -- Thông tin phiếu khám (bảng PhieuKham) --
        public string  MaNVBacSi  { get; set; } = string.Empty;   // Mã NV bác sĩ chỉ định — BẮT BUỘC
        public string  LyDoKham   { get; set; } = string.Empty;   // Lý do đến khám — BẮT BUỘC
       
    }


    // ───────────────────────────────────────────────────────────────────────
    // GET api/TiepDon/tra-cuu?sdt={soDienThoai}
    // — Tra cứu bệnh nhân cũ theo SĐT
    // Phân quyền: LeTan, Admin
    // ───────────────────────────────────────────────────────────────────────
    [HttpGet("tra-cuu")]
    [Authorize(Roles = "LeTan,Admin")]
    public async Task<IActionResult> TraCuuBenhNhan([FromQuery] string? sdt)
    {
        try
        {
            // Kiểm tra tham số sdt không rỗng
            if (string.IsNullOrWhiteSpace(sdt))
                return BadRequest(new { message = "Vui lòng nhập số điện thoại để tra cứu!" });

            // Validate định dạng SĐT (10 chữ số, bắt đầu bằng 0)
            if (!Regex.IsMatch(sdt.Trim(), @"^0\d{9}$"))
                return BadRequest(new { message = "Số điện thoại không đúng định dạng (phải gồm đúng 10 chữ số và bắt đầu bằng số 0)!" });

            // SELECT từ bảng BenhNhan WHERE SDT = @sdt
            var benhNhan = await _context.BenhNhans
                .AsNoTracking()
                .FirstOrDefaultAsync(bn => bn.Sdt == sdt.Trim());

            // Không tìm thấy → HTTP 200, found = false (luồng bình thường)
            if (benhNhan == null)
            {
                return Ok(new
                {
                    found   = false,
                    data    = (object?)null,
                    message = "Không tìm thấy hồ sơ bệnh nhân với số điện thoại này. Đây là bệnh nhân mới."
                });
            }

            // Tìm thấy → HTTP 200, found = true kèm dữ liệu BN
            return Ok(new
            {
                found = true,
                data  = new
                {
                    maBN       = benhNhan.MaBn,
                    hoTen      = benhNhan.HoTen,
                    ngaySinh   = benhNhan.NgaySinh?.ToString("dd-MM-yyyy"),   // DD-MM-YYYY
                    gioiTinh   = benhNhan.GioiTinh,
                    sdt        = benhNhan.Sdt,
                    diaChi     = benhNhan.DiaChi,
                    tienSuBenh = benhNhan.TienSuBenh
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // ───────────────────────────────────────────────────────────────────────
    // POST api/TiepDon
    // Tiếp nhận bệnh nhân (Tạo phiếu khám)
    // Phân quyền: LeTan, Admin
    // ───────────────────────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "LeTan,Admin")]
    public async Task<IActionResult> TiepNhanBenhNhan([FromBody] TiepNhanBenhNhanRequest request)
    {
        try
        {
            // Xác thực token (middleware đã xử lý; lấy thêm để verify claim)
            string? userIdClaim = User.FindFirstValue("userID");
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out _))
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });

            // Validate các trường bắt buộc ──────────────────────────

            if (string.IsNullOrWhiteSpace(request.HoTen))
                return BadRequest(new { message = "Vui lòng nhập Họ và tên bệnh nhân!" });

            if (string.IsNullOrWhiteSpace(request.Sdt))
                return BadRequest(new { message = "Vui lòng nhập Số điện thoại!" });

            if (string.IsNullOrWhiteSpace(request.NgaySinh))
                return BadRequest(new { message = "Vui lòng nhập Ngày tháng năm sinh!" });

            if (string.IsNullOrWhiteSpace(request.LyDoKham))
                return BadRequest(new { message = "Vui lòng nhập Lý do đến khám!" });

            if (string.IsNullOrWhiteSpace(request.GioiTinh))
                return BadRequest(new { message = "Giới tính không hợp lệ. Chỉ chấp nhận: Nam, Nữ, Khác!" });

            // maNVBacSi BẮT BUỘC
            if (string.IsNullOrWhiteSpace(request.MaNVBacSi))
                return BadRequest(new { message = "Vui lòng chỉ định bác sĩ khám trước khi lưu tiếp đón!" });

            // Validate định dạng SĐT ───────────────────────────────
            if (!Regex.IsMatch(request.Sdt.Trim(), @"^0\d{9}$"))
                return BadRequest(new { message = "Số điện thoại không đúng định dạng (phải gồm đúng 10 chữ số và bắt đầu bằng số 0, không chứa khoảng trắng)!" });

            // Validate ngaySinh (DD-MM-YYYY; hỗ trợ thêm dd/MM/yyyy) ─
            string[] dateFormats = { "dd-MM-yyyy", "dd/MM/yyyy" };
            if (!DateOnly.TryParseExact(request.NgaySinh.Trim(), dateFormats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateOnly ngaySinh))
            {
                return BadRequest(new { message = "Ngày sinh không hợp lệ. Vui lòng kiểm tra lại!" });
            }

            if (ngaySinh.Year < 1900)
                return BadRequest(new { message = "Ngày sinh không hợp lệ. Vui lòng kiểm tra lại!" });

            if (ngaySinh > DateOnly.FromDateTime(DateTime.Now))
                return BadRequest(new { message = "Ngày sinh không thể là ngày trong tương lai!" });

            // Validate gioiTinh ────────────────────────────────────
            string[] gioiTinhHopLe = { "Nam", "Nữ", "Khác" };
            if (!gioiTinhHopLe.Contains(request.GioiTinh.Trim()))
                return BadRequest(new { message = "Giới tính không hợp lệ. Chỉ chấp nhận: Nam, Nữ, Khác!" });

            // Validate maNVBacSi (bắt buộc, phải là RoleID=2) ──
            string maNVBacSi = request.MaNVBacSi.Trim();
            var bacSi = await _context.NhanViens
                .AsNoTracking()
                .Include(nv => nv.User)
                .FirstOrDefaultAsync(nv => nv.MaNv == maNVBacSi);

            if (bacSi == null || bacSi.User == null || bacSi.User.RoleId != 2)
                return BadRequest(new { message = "Bác sĩ chỉ định không hợp lệ hoặc không tồn tại trong hệ thống!" });

            // Chạy trong transaction ───────────────────────────
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                string maBN;
                bool   isNewPatient;

                // BN mới hay BN cũ không truyền MaBN
                if (string.IsNullOrWhiteSpace(request.MaBN))
                {
                    // Kiểm tra SĐT đã tồn tại trong hệ thống chưa
                    var bnTheoSdt = await _context.BenhNhans
                        .FirstOrDefaultAsync(bn => bn.Sdt == request.Sdt.Trim());

                    if (bnTheoSdt != null)
                    {
                        // ── BN cũ: lễ tân nhập SĐT nhưng không truyền MaBN (tái khám)
                        //    → tự động dùng hồ sơ bệnh nhân đã có theo SĐT
                        isNewPatient = false;
                        maBN = bnTheoSdt.MaBn;

                        // Cập nhật diaChi / tienSuBenh nếu có thay đổi
                        bool changed = false;
                        if (!string.IsNullOrWhiteSpace(request.DiaChi) && bnTheoSdt.DiaChi != request.DiaChi.Trim())
                        {
                            bnTheoSdt.DiaChi = request.DiaChi.Trim();
                            changed = true;
                        }
                        if (!string.IsNullOrWhiteSpace(request.TienSuBenh) && bnTheoSdt.TienSuBenh != request.TienSuBenh.Trim())
                        {
                            bnTheoSdt.TienSuBenh = request.TienSuBenh.Trim();
                            changed = true;
                        }
                        if (changed) await _context.SaveChangesAsync();
                    }
                    else
                    {
                        // ── BN mới: SĐT chưa có trong hệ thống → tạo hồ sơ mới
                        isNewPatient = true;

                        // Sinh maBN: "BN" + yyMMdd + stt 3 chữ số
                        string todayBN  = DateTime.Now.ToString("yyMMdd");
                        string prefixBN = $"BN{todayBN}";

                        // Dùng MAX thay COUNT để tránh lỗi khi có bản ghi bị xoá
                        var maBNCuoiCung = await _context.BenhNhans
                            .Where(bn => bn.MaBn.StartsWith(prefixBN))
                            .OrderByDescending(bn => bn.MaBn)
                            .Select(bn => bn.MaBn)
                            .FirstOrDefaultAsync();

                        int sttBN = 1;
                        if (maBNCuoiCung != null
                            && maBNCuoiCung.Length > prefixBN.Length
                            && int.TryParse(maBNCuoiCung[prefixBN.Length..], out int soHienTaiBN))
                        {
                            sttBN = soHienTaiBN + 1;
                        }

                        maBN = $"{prefixBN}{sttBN:D3}";  // VD: BN260713001

                        _context.BenhNhans.Add(new BenhNhan
                        {
                            MaBn       = maBN,
                            HoTen      = request.HoTen.Trim(),
                            NgaySinh   = ngaySinh,
                            GioiTinh   = request.GioiTinh.Trim(),
                            Sdt        = request.Sdt.Trim(),
                            DiaChi     = string.IsNullOrWhiteSpace(request.DiaChi)     ? null : request.DiaChi.Trim(),
                            TienSuBenh = string.IsNullOrWhiteSpace(request.TienSuBenh) ? null : request.TienSuBenh.Trim()
                        });
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    // BN cũ — lễ tân truyền MaBN tường minh ──────────────────────
                    isNewPatient = false;
                    maBN = request.MaBN.Trim();

                    var existingBN = await _context.BenhNhans
                        .FirstOrDefaultAsync(bn => bn.MaBn == maBN);

                    if (existingBN == null)
                    {
                        await transaction.RollbackAsync();
                        return NotFound(new { message = "Không tìm thấy hồ sơ bệnh nhân với mã BN đã cung cấp!" });
                    }

                    // Cập nhật diaChi / tienSuBenh nếu có thay đổi
                    bool changed = false;
                    if (!string.IsNullOrWhiteSpace(request.DiaChi) && existingBN.DiaChi != request.DiaChi.Trim())
                    {
                        existingBN.DiaChi = request.DiaChi.Trim();
                        changed = true;
                    }
                    if (!string.IsNullOrWhiteSpace(request.TienSuBenh) && existingBN.TienSuBenh != request.TienSuBenh.Trim())
                    {
                        existingBN.TienSuBenh = request.TienSuBenh.Trim();
                        changed = true;
                    }
                    if (changed) await _context.SaveChangesAsync();
                }

                // Kiểm tra BN không có phiếu khám đang active ──
                var phieuDangActive = await _context.PhieuKhams
                    .Where(pk => pk.MaBn == maBN &&
                                 (pk.TrangThaiKham == 0 ||
                                  pk.TrangThaiKham == 1 ||
                                  pk.TrangThaiKham == 2))
                    .Select(pk => pk.MaPhieu)
                    .FirstOrDefaultAsync();

                if (phieuDangActive != null)
                {
                    await transaction.RollbackAsync();
                    return Conflict(new
                    {
                        message = $"Bệnh nhân này đang có 1 phiếu khám chưa hoàn tất (Mã phiếu: {phieuDangActive}). Vui lòng xử lý xong phiếu cũ trước khi tạo phiếu mới!"
                    });
                }

                // Kiểm tra BN không có phiếu khám đã hoàn tất nhưng chưa thanh toán ──
                var phieuChuaThanhToan = await _context.PhieuKhams
                    .Where(pk => pk.MaBn == maBN
                              && pk.TrangThaiKham == 3
                              && !pk.HoaDons.Any(hd => hd.TrangThaiThanhToan == true))
                    .Select(pk => pk.MaPhieu)
                    .FirstOrDefaultAsync();

                if (phieuChuaThanhToan != null)
                {
                    await transaction.RollbackAsync();
                    return Conflict(new
                    {
                        message = $"Bệnh nhân này còn phiếu khám chưa thanh toán (Mã phiếu: {phieuChuaThanhToan}). Vui lòng thanh toán phiếu cũ trước khi tạo phiếu khám mới!"
                    });
                }

                // Sinh mã phiếu khám
                // Format: "PK_" + yyMMdd + "_" + stt 3 chữ số
                string todayPK  = DateTime.Now.ToString("yyMMdd");
                string prefixPK = $"PK_{todayPK}_";

                var maPKCuoiCung = await _context.PhieuKhams
                    .Where(pk => pk.MaPhieu.StartsWith(prefixPK))
                    .OrderByDescending(pk => pk.MaPhieu)
                    .Select(pk => pk.MaPhieu)
                    .FirstOrDefaultAsync();

                int sttPK = 1;
                if (maPKCuoiCung != null
                    && maPKCuoiCung.Length > prefixPK.Length
                    && int.TryParse(maPKCuoiCung[prefixPK.Length..], out int soHienTaiPK))
                {
                    sttPK = soHienTaiPK + 1;
                }

                string maPhieu = $"{prefixPK}{sttPK:D3}";  // VD: PK_260713_001

                // INSERT PhieuKham
                // MaNV = maNVBacSi
                var newPhieuKham = new PhieuKham
                {
                    MaPhieu       = maPhieu,
                    MaBn          = maBN,
                    MaNv          = maNVBacSi,
                    NgayKham      = DateTime.Now,
                    LyDoKham      = request.LyDoKham.Trim(),
                    TrangThaiKham = 0                // Chờ khám
                };

                _context.PhieuKhams.Add(newPhieuKham);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // Trả HTTP 201
                // Output: maBacSi / tenBacSi
                return StatusCode(201, new
                {
                    message = "Tiếp nhận bệnh nhân thành công",
                    data    = new
                    {
                        maPhieu       = maPhieu,
                        maBN          = maBN,
                        hoTen         = request.HoTen.Trim(),
                        ngayKham      = newPhieuKham.NgayKham?.ToString("o"),   // ISO 8601
                        lyDoKham      = request.LyDoKham.Trim(),
                        maBacSi       = maNVBacSi,                             // [CẬP NHẬT v2]
                        tenBacSi      = bacSi.HoTen,                           // [CẬP NHẬT v2]
                        trangThaiKham = 0,
                        isNewPatient  = isNewPatient
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
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // ───────────────────────────────────────────────────────────────────────
    // GET api/TiepDon/danh-sach
    //   — Lấy danh sách bệnh nhân đã tiếp đón
    // Phân quyền: LeTan, BacSi, Admin
    //
    // Phạm vi theo role:
    //   - Admin / LeTan: xem TẤT CẢ; có thể lọc thêm bằng query maBacSi
    //   - BacSi: CHỈ xem phiếu của chính mình (MaNV = maNV từ token);
    //             tham số maBacSi bị BỎ QUA kể cả khi truyền lên
    // ───────────────────────────────────────────────────────────────────────
    [HttpGet("danh-sach")]
    [Authorize(Roles = "LeTan,BacSi,Admin")]
    public async Task<IActionResult> LayDanhSachTiepDon(
        [FromQuery] string? search    = null,
        [FromQuery] string? maBacSi   = null,
        [FromQuery] int?    trangThai = null,
        [FromQuery] string? ngayKham  = null,
        [FromQuery] int     page      = 1,
        [FromQuery] int     limit     = 100)
    {
        try
        {
            // Lấy role + MaNV từ token
            bool isBacSi = User.IsInRole("BacSi");
            string? maNVTuToken = null;

            if (isBacSi)
            {
                // Bác sĩ cần MaNV để tự giới hạn phạm vi
                string? userIdClaim = User.FindFirstValue("userID");
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });

                var nhanVien = await _context.NhanViens
                    .AsNoTracking()
                    .FirstOrDefaultAsync(nv => nv.UserId == userId);

                if (nhanVien == null)
                    return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });

                maNVTuToken = nhanVien.MaNv;
            }

            // Validate trangThai
            if (trangThai.HasValue && !new[] { 0, 1, 2, 3 }.Contains(trangThai.Value))
                return BadRequest(new { message = "Giá trị trạng thái không hợp lệ. Chỉ chấp nhận: 0 | 1 | 2 | 3" });

            // Validate ngayKham — mặc định hôm nay; hỗ trợ DD-MM-YYYY theo đặc tả
            DateOnly ngayLoc;
            if (!string.IsNullOrWhiteSpace(ngayKham))
            {
                string[] dateFormats = { "dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
                if (!DateOnly.TryParseExact(ngayKham.Trim(), dateFormats,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out ngayLoc))
                {
                    return BadRequest(new { message = "Định dạng ngày lọc không hợp lệ. Vui lòng nhập theo định dạng DD-MM-YYYY!" });
                }
            }
            else
            {
                ngayLoc = DateOnly.FromDateTime(DateTime.Now);
            }

            // Validate phân trang
            if (page <= 0 || limit <= 0)
                return BadRequest(new { message = "Giá trị phân trang không hợp lệ" });

            if (limit > 200) limit = 200;

            // Build query
            DateTime ngayBatDau  = ngayLoc.ToDateTime(TimeOnly.MinValue);
            DateTime ngayKetThuc = ngayLoc.ToDateTime(TimeOnly.MaxValue);

            var query = _context.PhieuKhams
                .AsNoTracking()
                .Include(pk => pk.MaBnNavigation)
                .Include(pk => pk.MaNvNavigation)
                .Where(pk => pk.NgayKham >= ngayBatDau && pk.NgayKham <= ngayKetThuc);

            // Lọc theo role
            if (isBacSi)
            {
                // BacSi: chỉ xem phiếu của chính mình
                query = query.Where(pk => pk.MaNv == maNVTuToken);
            }
            else if (!string.IsNullOrWhiteSpace(maBacSi))
            {
                // Admin / LeTan: lọc theo maBacSi nếu có
                query = query.Where(pk => pk.MaNv == maBacSi.Trim());
            }

            // Lọc theo trạng thái
            if (trangThai.HasValue)
                query = query.Where(pk => pk.TrangThaiKham == trangThai.Value);

            // Tìm kiếm
            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchTrim = search.Trim().ToLower();
                query = query.Where(pk =>
                    (pk.MaBnNavigation != null && pk.MaBnNavigation.HoTen.ToLower().Contains(searchTrim)) ||
                    (pk.MaBnNavigation != null && pk.MaBnNavigation.Sdt != null && pk.MaBnNavigation.Sdt.Contains(searchTrim)) ||
                    (pk.MaBn != null && pk.MaBn.ToLower().Contains(searchTrim)));
            }

            // B5: Đếm tổng
            int total      = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)total / limit);

            // B6: Phân trang
            var danhSach = await query
                .OrderBy(pk => pk.NgayKham)   // ASC theo đặc tả
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(pk => new
                {
                    maPhieu       = pk.MaPhieu,
                    maBN          = pk.MaBn,
                    hoTen         = pk.MaBnNavigation != null ? pk.MaBnNavigation.HoTen : null,
                    ngaySinh      = pk.MaBnNavigation != null && pk.MaBnNavigation.NgaySinh != null
                                        ? pk.MaBnNavigation.NgaySinh.Value.ToString("dd-MM-yyyy")
                                        : null,
                    gioiTinh      = pk.MaBnNavigation != null ? pk.MaBnNavigation.GioiTinh : null,
                    sdt           = pk.MaBnNavigation != null ? pk.MaBnNavigation.Sdt : null,
                    diaChi        = pk.MaBnNavigation != null ? pk.MaBnNavigation.DiaChi : null,
                    lyDoKham      = pk.LyDoKham,
                    ngayKhamRaw   = pk.NgayKham,
                    trangThaiKham = pk.TrangThaiKham,
                    // tên field: maBacSi / tenBacSi
                    maBacSi       = pk.MaNv,
                    tenBacSi      = pk.MaNvNavigation != null ? pk.MaNvNavigation.HoTen : null
                })
                .ToListAsync();

            // B7: Trả HTTP 200
            return Ok(new
            {
                data = danhSach.Select(pk => new
                {
                    pk.maPhieu,
                    pk.maBN,
                    pk.hoTen,
                    pk.ngaySinh,
                    pk.gioiTinh,
                    pk.sdt,
                    pk.diaChi,
                    pk.lyDoKham,
                    ngayKham      = pk.ngayKhamRaw?.ToString("o"),  // ISO 8601
                    pk.trangThaiKham,
                    pk.maBacSi,
                    pk.tenBacSi
                }),
                pagination = new { page, limit, total, totalPages },
                filter = new
                {
                    ngayKham  = ngayLoc.ToString("dd-MM-yyyy"),
                    trangThai = trangThai ?? (object?)null,
                    // BacSi: luôn trả maBacSi = chính mình; Admin/LeTan: trả tham số đã lọc
                    maBacSi   = isBacSi
                                    ? maNVTuToken
                                    : (string.IsNullOrWhiteSpace(maBacSi) ? (object?)null : maBacSi.Trim()),
                    search    = string.IsNullOrWhiteSpace(search) ? (object?)null : search.Trim()
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // ───────────────────────────────────────────────────────────────────────
    // GET api/TiepDon/{maPhieu}
    //   — Xem chi tiết hồ sơ bệnh nhân (theo phiếu khám)
    // Phân quyền: LeTan, BacSi, Admin
    //
    // Output field:
    //   maBacSi / tenBacSi
    // ───────────────────────────────────────────────────────────────────────
    [HttpGet("{maPhieu}")]
    [Authorize(Roles = "LeTan,BacSi,Admin")]
    public async Task<IActionResult> XemChiTietHoSo(string maPhieu)
    {
        try
        {
            // B2: Kiểm tra maPhieu tồn tại
            var phieuKham = await _context.PhieuKhams
                .AsNoTracking()
                .Include(pk => pk.MaBnNavigation)                          // BenhNhan
                .Include(pk => pk.MaNvNavigation)                          // NhanVien (= bác sĩ chỉ định)
                .Include(pk => pk.MaIcds)                                  // DanhMucICD (many-to-many)
                .Include(pk => pk.DichVuYtes)                              // DichVuYTe (chỉ định CLS)
                    .ThenInclude(dv => dv.MaDvNavigation)                  // ChiTietDichVuYTe (danh mục)
                .Include(pk => pk.DonThuocs)                               // DonThuoc
                    .ThenInclude(dt => dt.ChiTietDonThuocs)                // ChiTietDonThuoc
                        .ThenInclude(ct => ct.MaThuocNavigation)           // DanhMucThuoc
                .FirstOrDefaultAsync(pk => pk.MaPhieu == maPhieu);

            if (phieuKham == null)
                return NotFound(new { message = "Không tìm thấy hồ sơ bệnh án. Phiếu khám có thể đã bị xóa hoặc không tồn tại!" });

            // Lấy đơn thuốc (mỗi phiếu tối đa 1 đơn)
            var donThuoc = phieuKham.DonThuocs
                .OrderByDescending(dt => dt.NgayKeDon)
                .FirstOrDefault();

            // B4: Tổng hợp và trả HTTP 200
            return Ok(new
            {
                // --- Thông tin phiếu khám ---
                maPhieu       = phieuKham.MaPhieu,
                ngayKham      = phieuKham.NgayKham?.ToString("o"),
                trangThaiKham = phieuKham.TrangThaiKham,
                lyDoKham      = phieuKham.LyDoKham,

                // --- Thông tin bệnh nhân ---
                maBN       = phieuKham.MaBn,
                hoTen      = phieuKham.MaBnNavigation?.HoTen,
                ngaySinh   = phieuKham.MaBnNavigation?.NgaySinh?.ToString("dd-MM-yyyy"),
                gioiTinh   = phieuKham.MaBnNavigation?.GioiTinh,
                sdt        = phieuKham.MaBnNavigation?.Sdt,
                diaChi     = phieuKham.MaBnNavigation?.DiaChi,
                tienSuBenh = phieuKham.MaBnNavigation?.TienSuBenh,

                // --- [CẬP NHẬT v2] Bác sĩ chỉ định: maBacSi / tenBacSi ---
                maBacSi  = phieuKham.MaNv,
                tenBacSi = phieuKham.MaNvNavigation?.HoTen,

                // --- Sinh hiệu (bác sĩ cập nhật ở bước Khám cơ bản, có thể null) ---
                mach     = phieuKham.Mach,
                nhietDo  = phieuKham.NhietDo,
                huyetAp  = phieuKham.HuyetAp,
                canNang  = phieuKham.CanNang,
                chieuCao = phieuKham.ChieuCao,

                // --- Kết luận ---
                ketLuan = phieuKham.KetLuan,

                // --- ICD (do bác sĩ nhập ở bước Khám cơ bản) ---
                danhSachICD = phieuKham.MaIcds
                    .OrderBy(icd => icd.MaIcd)
                    .Select(icd => new
                    {
                        maICD   = icd.MaIcd,
                        tenBenh = icd.TenBenh
                    }).ToList(),

                // --- Chỉ định CLS (2 mức: 0=Chưa thực hiện, 1=Đã làm CLS) ---
                dichVuYTe = phieuKham.DichVuYtes
                    .OrderBy(dv => dv.MaChiTiet)
                    .Select(dv => new
                    {
                        maChiTiet       = dv.MaChiTiet,
                        maDV            = dv.MaDv,
                        tenDV           = dv.MaDvNavigation?.TenDv,
                        ketQua          = dv.KetQua,
                        trangThaiDichVu = dv.TrangThaiDichVu   // 0=Chưa thực hiện | 1=Đã làm CLS
                    }).ToList(),

                // --- Đơn thuốc ---
                donThuoc = donThuoc == null
                    ? new
                    {
                        maDonThuoc     = (string?)null,
                        ngayKeDon      = (string?)null,
                        loiDanDonThuoc = (string?)null,
                        chiTiet        = new List<object>()
                    }
                    : new
                    {
                        maDonThuoc     = (string?)donThuoc.MaDonThuoc,
                        ngayKeDon      = (string?)donThuoc.NgayKeDon?.ToString("o"),
                        loiDanDonThuoc = (string?)donThuoc.LoiDan,
                        chiTiet        = donThuoc.ChiTietDonThuocs
                            .OrderBy(ct => ct.MaThuoc)
                            .Select(ct => (object)new
                            {
                                maThuoc   = ct.MaThuoc,
                                tenThuoc  = ct.MaThuocNavigation?.TenThuoc,
                                soLuong   = ct.SoLuong,
                                cachDung  = ct.CachDung,
                                donViTinh = ct.MaThuocNavigation?.DonViTinh
                            }).ToList()
                    }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }
}
