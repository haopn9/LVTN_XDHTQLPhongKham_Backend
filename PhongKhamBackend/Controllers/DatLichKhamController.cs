using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Route("api/DatLichKham")]
public class DatLichKhamController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;

    public DatLichKhamController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }


    // DTOs
    public class DatLichRequest
    {
        public string  HoTenKhach { get; set; } = string.Empty;
        public string  Sdt        { get; set; } = string.Empty;
        public string  NgayHen    { get; set; } = string.Empty;  // yyyy-MM-dd
        public string  CaHen      { get; set; } = string.Empty;  // "Sáng" | "Chiều"
        public string? YeuCauKham { get; set; }
        public string? MaNV       { get; set; }                  // optional — khách không nhất thiết chọn bác sĩ
    }

    public class TiepNhanRequest
    {
        public string?           MaBacSiChiDinh { get; set; }   // bắt buộc nếu DatLichKham.MaNV đang NULL
        public ThongTinBenhNhan? BenhNhan       { get; set; }
    }

    public class ThongTinBenhNhan
    {
        public string? MaBN       { get; set; }   // null → hệ thống tự tìm/tạo theo SDT
        public string? GioiTinh   { get; set; }
        public string? NgaySinh   { get; set; }   // yyyy-MM-dd hoặc dd-MM-yyyy
        public string? DiaChi     { get; set; }
        public string? TienSuBenh { get; set; }
    }

    // Kiểm tra BN có phiếu khám đang active / chưa thanh toán
    private async Task<(bool coLoi, string? message)> KiemTraPhieuKhamConDang(string maBN)
    {
        // Phiếu khám đang active (TrangThaiKham: 0=Chờ, 1=Đang khám, 2=Hoàn thành CLS)
        var phieuActive = await _context.PhieuKhams
            .Where(pk => pk.MaBn == maBN
                      && (pk.TrangThaiKham == 0
                       || pk.TrangThaiKham == 1
                       || pk.TrangThaiKham == 2))
            .Select(pk => pk.MaPhieu)
            .FirstOrDefaultAsync();

        if (phieuActive != null)
            return (true, $"Bệnh nhân đang có phiếu khám chưa hoàn tất (Mã phiếu: {phieuActive}). Vui lòng xử lý xong phiếu cũ trước khi tiếp nhận mới!");

        // Phiếu đã hoàn tất nhưng chưa thanh toán (TrangThaiKham=3, không có HoaDon.TrangThaiThanhToan=true)
        var phieuChuaTT = await _context.PhieuKhams
            .Where(pk => pk.MaBn == maBN
                      && pk.TrangThaiKham == 3
                      && !pk.HoaDons.Any(hd => hd.TrangThaiThanhToan == true))
            .Select(pk => pk.MaPhieu)
            .FirstOrDefaultAsync();

        if (phieuChuaTT != null)
            return (true, $"Bệnh nhân còn phiếu khám chưa thanh toán (Mã phiếu: {phieuChuaTT}). Vui lòng thanh toán trước khi tạo phiếu mới!");

        return (false, null);
    }

    // POST api/DatLichKham — Khách tạo lịch hẹn (public)
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> DatLich([FromBody] DatLichRequest request)
    {
        try
        {
            // Validate bắt buộc 
            if (string.IsNullOrWhiteSpace(request.HoTenKhach))
                return BadRequest(new { message = "Vui lòng nhập họ tên khách!" });

            if (string.IsNullOrWhiteSpace(request.Sdt))
                return BadRequest(new { message = "Vui lòng nhập số điện thoại!" });

            if (!Regex.IsMatch(request.Sdt.Trim(), @"^(0|\+84)\d{9}$"))
                return BadRequest(new { message = "Số điện thoại không hợp lệ (10 chữ số, bắt đầu bằng 0 hoặc +84)!" });

            if (string.IsNullOrWhiteSpace(request.NgayHen))
                return BadRequest(new { message = "Vui lòng nhập ngày hẹn!" });

            if (!DateOnly.TryParse(request.NgayHen.Trim(), out DateOnly ngayHen))
                return BadRequest(new { message = "ngayHen không hợp lệ. Định dạng: yyyy-MM-dd" });

            if (ngayHen < DateOnly.FromDateTime(DateTime.Now))
                return BadRequest(new { message = "Ngày hẹn không thể là ngày trong quá khứ!" });

            if (string.IsNullOrWhiteSpace(request.CaHen))
                return BadRequest(new { message = "Vui lòng chọn ca hẹn!" });

            string caHen = request.CaHen.Trim();
            if (caHen != "Sang" && caHen != "Chieu")
                return BadRequest(new { message = "Ca hẹn không hợp lệ. Chỉ chấp nhận: Sang, Chieu" });

            // Kiểm tra bác sĩ (nếu khách chọn cụ thể) 
            string? maNV = string.IsNullOrWhiteSpace(request.MaNV) ? null : request.MaNV.Trim();

            if (maNV != null)
            {
                // Kiểm tra bác sĩ tồn tại
                var bacSi = await _context.NhanViens
                    .AsNoTracking()
                    .Include(nv => nv.User)
                    .FirstOrDefaultAsync(nv => nv.MaNv == maNV);

                if (bacSi == null || bacSi.User == null || bacSi.User.RoleId != 2)
                    return BadRequest(new { message = "Bác sĩ được chọn không tồn tại trong hệ thống!" });

                // Kiểm tra bác sĩ có ca trực đúng ngayHen + caHen
                bool coLichTruc = await _context.LichLamViecs
                    .AsNoTracking()
                    .AnyAsync(l => l.MaNv == maNV
                               && l.NgayLamViec == ngayHen
                               && l.CaLamViec   == caHen);

                if (!coLichTruc)
                    return Conflict(new
                    {
                        message = $"Bác sĩ {bacSi.HoTen} không có lịch trực vào {caHen} ngày {ngayHen:dd/MM/yyyy}. Vui lòng chọn thời gian khác!"
                    });
            }

            // Insert DatLichKham
            var newLich = new DatLichKham
            {
                HoTenKhach = request.HoTenKhach.Trim(),
                Sdt        = request.Sdt.Trim(),
                NgayHen    = ngayHen,
                CaHen      = caHen,
                YeuCauKham = string.IsNullOrWhiteSpace(request.YeuCauKham) ? null : request.YeuCauKham.Trim(),
                MaNv       = maNV,
                TrangThai  = "ChoXacNhan"
            };

            _context.DatLichKhams.Add(newLich);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                maDatLich = newLich.MaDatLich,
                trangThai = newLich.TrangThai,
                ngayHen   = newLich.NgayHen?.ToString("yyyy-MM-dd"),
                caHen     = newLich.CaHen
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // GET api/DatLichKham — Danh sách lịch hẹn (LeTan, Admin)
    // Phân trang bắt buộc; tìm theo tên/SDT; ưu tiên ChoXacNhan lên đầu.
    [HttpGet]
    [Authorize(Roles = "LeTan,Admin")]
    public async Task<IActionResult> DanhSachLichHen(
        [FromQuery] string? trangThai = null,
        [FromQuery] string? ngayHen   = null,
        [FromQuery] string? search    = null,
        [FromQuery] int     page      = 1,
        [FromQuery] int     pageSize  = 20)
    {
        try
        {
            if (page < 1)      page     = 1;
            if (pageSize < 1)  pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            // Các giá trị TrangThai hợp lệ
            string[] trangThaiHopLe = { "ChoXacNhan", "DaXacNhan", "DaTiepNhan", "DaHuy" };
            if (!string.IsNullOrWhiteSpace(trangThai) && !trangThaiHopLe.Contains(trangThai.Trim()))
                return BadRequest(new { message = "trangThai không hợp lệ. Chỉ chấp nhận: ChoXacNhan, DaXacNhan, DaTiepNhan, DaHuy" });

            var query = _context.DatLichKhams
                .AsNoTracking()
                .Include(d => d.MaNvNavigation)
                    .ThenInclude(nv => nv!.MaKhoaNavigation)
                .AsQueryable();

            // Filter theo trangThai
            if (!string.IsNullOrWhiteSpace(trangThai))
                query = query.Where(d => d.TrangThai == trangThai.Trim());

            // Filter theo ngayHen
            if (!string.IsNullOrWhiteSpace(ngayHen))
            {
                if (!DateOnly.TryParse(ngayHen.Trim(), out DateOnly ngayLoc))
                    return BadRequest(new { message = "ngayHen không hợp lệ. Định dạng: yyyy-MM-dd" });
                query = query.Where(d => d.NgayHen == ngayLoc);
            }

            // Filter theo search (tên hoặc SDT) — tránh non-sargable: dùng StartsWith / Contains trực tiếp
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(d => d.HoTenKhach.Contains(s) || d.Sdt.StartsWith(s));
            }

            // Sắp xếp: ChoXacNhan lên đầu, sau đó NgayHen ASC
            query = query
                .OrderBy(d => d.TrangThai == "ChoXacNhan" ? 0 : 1)
                .ThenBy(d => d.NgayHen);

            int total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    maDatLich  = d.MaDatLich,
                    hoTenKhach = d.HoTenKhach,
                    sdt        = d.Sdt,
                    ngayHen    = d.NgayHen.HasValue ? d.NgayHen.Value.ToString("yyyy-MM-dd") : null,
                    caHen      = d.CaHen,
                    yeuCauKham = d.YeuCauKham,
                    trangThai  = d.TrangThai,
                    maNV       = d.MaNv,
                    tenBacSi   = d.MaNvNavigation != null ? d.MaNvNavigation.HoTen : null,
                    maKhoa     = d.MaNvNavigation != null ? d.MaNvNavigation.MaKhoa : null,
                    tenKhoa    = d.MaNvNavigation != null && d.MaNvNavigation.MaKhoaNavigation != null
                                     ? d.MaNvNavigation.MaKhoaNavigation.TenKhoa
                                     : null
                })
                .ToListAsync();

            return Ok(new
            {
                total,
                page,
                pageSize,
                data = items
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // PUT api/DatLichKham/{maDatLich}/xac-nhan — Lễ tân xác nhận lịch hẹn
    [HttpPut("{maDatLich}/xac-nhan")]
    [Authorize(Roles = "LeTan,Admin")]
    public async Task<IActionResult> XacNhanLich(int maDatLich)
    {
        try
        {
            var lichHen = await _context.DatLichKhams
                .Include(d => d.MaNvNavigation)
                    .ThenInclude(nv => nv!.MaKhoaNavigation)
                .FirstOrDefaultAsync(d => d.MaDatLich == maDatLich);

            if (lichHen == null)
                return NotFound(new { message = $"Không tìm thấy lịch hẹn với mã {maDatLich}!" });

            if (lichHen.TrangThai != "ChoXacNhan")
                return Conflict(new
                {
                    message = $"Không thể xác nhận! Lịch hẹn đang ở trạng thái '{lichHen.TrangThai}', chỉ xác nhận được lịch ở trạng thái 'ChoXacNhan'."
                });

            lichHen.TrangThai = "DaXacNhan";
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message    = "Xác nhận lịch hẹn thành công",
                maDatLich  = lichHen.MaDatLich,
                hoTenKhach = lichHen.HoTenKhach,
                sdt        = lichHen.Sdt,
                ngayHen    = lichHen.NgayHen?.ToString("yyyy-MM-dd"),
                caHen      = lichHen.CaHen,
                trangThai  = lichHen.TrangThai,
                maNV       = lichHen.MaNv,
                tenBacSi   = lichHen.MaNvNavigation?.HoTen,
                tenKhoa    = lichHen.MaNvNavigation?.MaKhoaNavigation?.TenKhoa
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // PUT api/DatLichKham/{maDatLich}/huy — Hủy lịch hẹn
    [HttpPut("{maDatLich}/huy")]
    [Authorize(Roles = "LeTan,Admin")]
    public async Task<IActionResult> HuyLich(int maDatLich)
    {
        try
        {
            var lichHen = await _context.DatLichKhams
                .FirstOrDefaultAsync(d => d.MaDatLich == maDatLich);

            if (lichHen == null)
                return NotFound(new { message = $"Không tìm thấy lịch hẹn với mã {maDatLich}!" });

            // Chỉ hủy được khi ở ChoXacNhan hoặc DaXacNhan
            if (lichHen.TrangThai == "DaTiepNhan" || lichHen.TrangThai == "DaHuy")
                return Conflict(new
                {
                    message = $"Không thể hủy! Lịch hẹn đang ở trạng thái '{lichHen.TrangThai}'. Chỉ hủy được lịch ở trạng thái 'ChoXacNhan' hoặc 'DaXacNhan'."
                });

            lichHen.TrangThai = "DaHuy";
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message   = "Hủy lịch hẹn thành công",
                maDatLich = lichHen.MaDatLich,
                trangThai = lichHen.TrangThai
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // POST api/DatLichKham/{maDatLich}/tiep-nhan — Tiếp nhận bệnh nhân (Bước 5)
    //   Toàn bộ thao tác nằm trong 1 transaction:
    //     1. Xác định bác sĩ khám
    //     2. Tìm / tạo BenhNhan
    //     3. Kiểm tra phiếu active / nợ tiền
    //     4. Sinh PhieuKham
    //     5. Cập nhật DatLichKham.TrangThai = 'DaTiepNhan'
    [HttpPost("{maDatLich}/tiep-nhan")]
    [Authorize(Roles = "LeTan,Admin")]
    public async Task<IActionResult> TiepNhan(int maDatLich, [FromBody] TiepNhanRequest request)
    {
        // ── Lấy lịch hẹn ──────────────────────────────────────────────────
        var lichHen = await _context.DatLichKhams
            .FirstOrDefaultAsync(d => d.MaDatLich == maDatLich);

        if (lichHen == null)
            return NotFound(new { message = $"Không tìm thấy lịch hẹn với mã {maDatLich}!" });

        if (lichHen.TrangThai != "DaXacNhan")
            return Conflict(new
            {
                message = $"Không thể tiếp nhận! Lịch hẹn đang ở trạng thái '{lichHen.TrangThai}'. Chỉ tiếp nhận được lịch đã xác nhận ('DaXacNhan')."
            });

        // ── Xác định bác sĩ khám ──────────────────────────────────────────
        // Ưu tiên maBacSiChiDinh (lễ tân đổi bác sĩ), fallback về MaNV đã lưu trong lịch hẹn
        string? maBacSi = !string.IsNullOrWhiteSpace(request?.MaBacSiChiDinh)
            ? request.MaBacSiChiDinh.Trim()
            : lichHen.MaNv;

        if (string.IsNullOrEmpty(maBacSi))
            return BadRequest(new { message = "Chưa chỉ định bác sĩ khám. Vui lòng chọn bác sĩ trước khi tiếp nhận!" });

        // Kiểm tra bác sĩ hợp lệ
        var bacSi = await _context.NhanViens
            .AsNoTracking()
            .Include(nv => nv.User)
            .FirstOrDefaultAsync(nv => nv.MaNv == maBacSi);

        if (bacSi == null || bacSi.User == null || bacSi.User.RoleId != 2)
            return BadRequest(new { message = "Bác sĩ chỉ định không hợp lệ hoặc không tồn tại trong hệ thống!" });

        // ── Bắt đầu transaction ───────────────────────────────────────────
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                string maBN;

                // ── Xác định BenhNhan ──────────────────────────────────────
                var bnInput = request?.BenhNhan;

                if (!string.IsNullOrWhiteSpace(bnInput?.MaBN))
                {
                    // Lễ tân tự chọn MaBN tường minh
                    maBN = bnInput.MaBN.Trim();
                    bool exists = await _context.BenhNhans.AnyAsync(bn => bn.MaBn == maBN);
                    if (!exists)
                    {
                        await transaction.RollbackAsync();
                        return NotFound(new { message = $"Không tìm thấy hồ sơ bệnh nhân với mã {maBN}!" });
                    }
                }
                else
                {
                    // Tự tìm theo SDT của lịch hẹn
                    var danhSachBN = await _context.BenhNhans
                        .Where(bn => bn.Sdt == lichHen.Sdt)
                        .ToListAsync();

                    if (danhSachBN.Count > 1)
                    {
                        // Nhiều hơn 1 hồ sơ trùng SDT → yêu cầu lễ tân chọn thủ công
                        await transaction.RollbackAsync();
                        return Conflict(new
                        {
                            message = $"Tìm thấy {danhSachBN.Count} hồ sơ bệnh nhân với SĐT {lichHen.Sdt}. Vui lòng tra cứu và chọn đúng hồ sơ (truyền maBN) trước khi tiếp nhận!",
                            danhSachTrungSdt = danhSachBN.Select(bn => new
                            {
                                maBN  = bn.MaBn,
                                hoTen = bn.HoTen,
                                sdt   = bn.Sdt
                            })
                        });
                    }
                    else if (danhSachBN.Count == 1)
                    {
                        // Tìm thấy đúng 1 → dùng luôn
                        maBN = danhSachBN[0].MaBn;
                    }
                    else
                    {
                        // Không tìm thấy → tạo mới BenhNhan
                        // Kiểm tra các trường bắt buộc khi tạo mới
                        if (string.IsNullOrWhiteSpace(bnInput?.GioiTinh))
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { message = "Không tìm thấy hồ sơ bệnh nhân. Vui lòng cung cấp thông tin để tạo hồ sơ mới (gioiTinh, ngaySinh)!" });
                        }

                        // Validate giới tính
                        string[] gioiTinhHopLe = { "Nam", "Nữ", "Khác" };
                        if (!gioiTinhHopLe.Contains(bnInput.GioiTinh.Trim()))
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { message = "Giới tính không hợp lệ. Chỉ chấp nhận: Nam, Nữ, Khác" });
                        }

                        // Validate ngaySinh
                        DateOnly ngaySinh = DateOnly.MinValue;
                        if (!string.IsNullOrWhiteSpace(bnInput.NgaySinh))
                        {
                            string[] formats = { "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy" };
                            if (!DateOnly.TryParseExact(bnInput.NgaySinh.Trim(), formats,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.None, out ngaySinh))
                            {
                                await transaction.RollbackAsync();
                                return BadRequest(new { message = "Ngày sinh không hợp lệ. Định dạng: yyyy-MM-dd hoặc dd-MM-yyyy" });
                            }
                        }

                        // Sinh maBN: "BN" + yyMMdd + stt 3 chữ số 
                        string today   = DateTime.Now.ToString("yyMMdd");
                        string prefix  = $"BN{today}";

                        var maBNCuoi = await _context.BenhNhans
                            .Where(bn => bn.MaBn.StartsWith(prefix))
                            .OrderByDescending(bn => bn.MaBn)
                            .Select(bn => bn.MaBn)
                            .FirstOrDefaultAsync();

                        int stt = 1;
                        if (maBNCuoi != null
                            && maBNCuoi.Length > prefix.Length
                            && int.TryParse(maBNCuoi[prefix.Length..], out int soHienTai))
                        {
                            stt = soHienTai + 1;
                        }

                        maBN = $"{prefix}{stt:D3}";

                        _context.BenhNhans.Add(new BenhNhan
                        {
                            MaBn       = maBN,
                            HoTen      = lichHen.HoTenKhach,
                            Sdt        = lichHen.Sdt,
                            GioiTinh   = bnInput.GioiTinh.Trim(),
                            NgaySinh   = ngaySinh == DateOnly.MinValue ? null : ngaySinh,
                            DiaChi     = string.IsNullOrWhiteSpace(bnInput.DiaChi)     ? null : bnInput.DiaChi.Trim(),
                            TienSuBenh = string.IsNullOrWhiteSpace(bnInput.TienSuBenh) ? null : bnInput.TienSuBenh.Trim()
                        });
                        await _context.SaveChangesAsync();
                    }
                }

                // Kiểm tra phiếu active / chưa thanh toán 
                var (coLoi, errMsg) = await KiemTraPhieuKhamConDang(maBN);
                if (coLoi)
                {
                    await transaction.RollbackAsync();
                    return Conflict(new { message = errMsg });
                }

                //  Sinh mã phiếu khám  
                string todayPK  = DateTime.Now.ToString("yyMMdd");
                string prefixPK = $"PK_{todayPK}_";

                var maPKCuoi = await _context.PhieuKhams
                    .Where(pk => pk.MaPhieu.StartsWith(prefixPK))
                    .OrderByDescending(pk => pk.MaPhieu)
                    .Select(pk => pk.MaPhieu)
                    .FirstOrDefaultAsync();

                int sttPK = 1;
                if (maPKCuoi != null
                    && maPKCuoi.Length > prefixPK.Length
                    && int.TryParse(maPKCuoi[prefixPK.Length..], out int soHienTaiPK))
                {
                    sttPK = soHienTaiPK + 1;
                }

                string maPhieu = $"{prefixPK}{sttPK:D3}";

                //  INSERT PhieuKham 
                var newPhieu = new PhieuKham
                {
                    MaPhieu       = maPhieu,
                    MaBn          = maBN,
                    MaNv          = maBacSi,
                    NgayKham      = DateTime.Now,
                    LyDoKham      = lichHen.YeuCauKham,
                    TrangThaiKham = 0  // Chờ khám
                };

                _context.PhieuKhams.Add(newPhieu);

                //  Cập nhật DatLichKham 
                lichHen.TrangThai = "DaTiepNhan";
                lichHen.MaNv      = maBacSi;  // đồng bộ lại bác sĩ nếu lễ tân đổi

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return StatusCode(201, new
                {
                    maDatLich = lichHen.MaDatLich,
                    maBN      = maBN,
                    maPhieu   = maPhieu,
                    maNV      = maBacSi,
                    trangThai = "DaTiepNhan"
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
}
