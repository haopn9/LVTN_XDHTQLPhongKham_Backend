using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Route("api/KhamBenh")]
[Authorize]
public class KhamBenhController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;
    private readonly ILogger<KhamBenhController> _logger;

    public KhamBenhController(QuanLyPhongKhamDbContext context, ILogger<KhamBenhController> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // DTOs

    // Request body cho PUT api/KhamBenh/{maPhieu}
    // Tất cả nhóm field đều TUỲ CHỌN — gửi nhóm nào thì cập nhật nhóm đó.
    public class CapNhatKhamBenhRequest
    {
        // Nhóm 1: Khám cơ bản — sinh hiệu
        public int?    Mach     { get; set; }
        public double? NhietDo  { get; set; }
        public string? HuyetAp  { get; set; }
        public double? CanNang  { get; set; }
        public double? ChieuCao { get; set; }

        // Nhóm 1: Khám cơ bản — ICD (ghi đè toàn bộ ChiTietPhieuKhamICD)
        // icdList chỉ ở nhóm Khám cơ bản
        public List<string>? IcdList { get; set; }

        // Nhóm 2: Chỉ định CLS mới (chỉ thêm, không xoá chỉ định cũ)
        public List<string>? ChiDinhCLSMoi { get; set; }

        // Nhóm 3: Toggle trạng thái CLS 
        public List<TrangThaiClsItem>? TrangThaiCLS { get; set; }

        // Nhóm 4: Đơn thuốc (REPLACE các dòng chưa phát)
        public string?              LoiDan   { get; set; }
        public List<DonThuocItem>?  DonThuoc { get; set; }

        // Nhóm 4b: Kê vật tư (REPLACE toàn bộ danh sách — chỉ áp dụng cho phiếu có CLS)
        public List<VatTuItem>? VatTuList { get; set; }

        // Nhóm 5: Kết luận khám
        public string? KetLuan { get; set; }

        // Chuyển trạng thái phiếu khám (1=Đang khám | 2=Chờ CLS | 3=Hoàn thành)
        public int? TrangThaiKham { get; set; }
    }

    /// Item trong TrangThaiCLS (toggle trạng thái từng chỉ định CLS)
    public class TrangThaiClsItem
    {
        public int  MaChiTiet { get; set; }   // PK bảng DichVuYTe
        public bool DaLamCLS  { get; set; }   // true=Đã làm CLS(1) | false=Chưa thực hiện(0)
    }

    /// Item thuốc trong đơn thuốc
    public class DonThuocItem
    {
        public string MaThuoc  { get; set; } = string.Empty;
        public int?   SoLuong  { get; set; }
        public string CachDung { get; set; } = string.Empty;
    }

    /// Item vật tư trong kê vật tư (nhóm 4b)
    public class VatTuItem
    {
        public string MaVatTu { get; set; } = string.Empty;
        public int?   SoLuong { get; set; }
    }

    // Helper class

    private class ThongTinDangNhap
    {
        public bool    IsAdmin { get; set; }
        public bool    IsBacSi { get; set; }
        public string? MaNv    { get; set; }
    }


    // ───────────────────────────────────────────────────────────────────────
    // GET api/KhamBenh/danh-sach
    //   — Lấy danh sách bệnh nhân chờ khám
    // Phân quyền: BacSi, Admin
    //   - Route: LayDSBenhNhan → danh-sach
    //   - BacSi: chỉ xem phiếu của chính mình; Admin: xem tất cả (lọc maBacSi tuỳ chọn)
    //   - Mặc định: chỉ trả TrangThaiKham IN (0,1,2) — không trả phiếu Hoàn thành(3)
    // ───────────────────────────────────────────────────────────────────────
    [HttpGet("danh-sach")]
    [Authorize(Roles = "BacSi,Admin")]
    public async Task<IActionResult> LayDanhSachBenhNhanChoKham(
        [FromQuery] string? search   = null,
        [FromQuery] int?    trangThai = null,
        [FromQuery] string? maBacSi  = null,
        [FromQuery] string? ngayKham = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     limit    = 20)
    {
        try
        {
            // Lấy thông tin đăng nhập
            var ttdn = await LayThongTinDangNhapAsync();
            if (ttdn == null)
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });

            // Validate trangThai
            if (trangThai.HasValue && !new[] { 0, 1, 2 }.Contains(trangThai.Value))
                return BadRequest(new { message = "Giá trị trạng thái không hợp lệ. Chỉ chấp nhận: 0 | 1 | 2" });

            if (page <= 0 || limit <= 0)
                return BadRequest(new { message = "Giá trị phân trang không hợp lệ" });

            if (limit > 100) limit = 100;

            // Validate ngayKham — hỗ trợ DD-MM-YYYY theo đặc tả; tương thích yyyy-MM-dd
            DateTime ngayLoc;
            if (string.IsNullOrWhiteSpace(ngayKham))
            {
                ngayLoc = DateTime.Today;
            }
            else
            {
                string[] fmts = { "dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
                if (!DateTime.TryParseExact(ngayKham.Trim(), fmts,
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out ngayLoc))
                {
                    return BadRequest(new { message = "Định dạng ngày lọc không hợp lệ. Vui lòng nhập theo định dạng DD-MM-YYYY!" });
                }
            }

            DateTime tuNgay  = ngayLoc.Date;
            DateTime denNgay = tuNgay.AddDays(1);

            // Build query
            var query = _context.PhieuKhams
                .AsNoTracking()
                .Include(pk => pk.MaBnNavigation)
                .Include(pk => pk.MaNvNavigation)
                .Where(pk => pk.NgayKham >= tuNgay && pk.NgayKham < denNgay);

            // Mặc định lấy 0,1,2; nếu có lọc thì lấy theo trangThai yêu cầu
            if (trangThai.HasValue)
                query = query.Where(pk => pk.TrangThaiKham == trangThai.Value);
            else
                query = query.Where(pk => pk.TrangThaiKham == 0
                                       || pk.TrangThaiKham == 1
                                       || pk.TrangThaiKham == 2);

            // Phạm vi theo role
            if (ttdn.IsBacSi && !ttdn.IsAdmin)
                query = query.Where(pk => pk.MaNv == ttdn.MaNv);
            else if (ttdn.IsAdmin && !string.IsNullOrWhiteSpace(maBacSi))
                query = query.Where(pk => pk.MaNv == maBacSi.Trim());

            // Tìm kiếm theo tên / mã BN / mã phiếu
            if (!string.IsNullOrWhiteSpace(search))
            {
                string tuKhoa = search.Trim().ToLower();
                query = query.Where(pk =>
                    pk.MaPhieu.ToLower().Contains(tuKhoa) ||
                    (pk.MaBn != null && pk.MaBn.ToLower().Contains(tuKhoa)) ||
                    (pk.MaBnNavigation != null && pk.MaBnNavigation.HoTen.ToLower().Contains(tuKhoa)));
            }

            // Đếm tổng
            int total      = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)total / limit);

            // Sắp xếp DESC, phân trang
            var data = await query
                .OrderByDescending(pk => pk.NgayKham)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(pk => new
                {
                    maPhieu       = pk.MaPhieu,
                    maBN          = pk.MaBn,
                    hoTen         = pk.MaBnNavigation != null ? pk.MaBnNavigation.HoTen : null,
                    gioiTinh      = pk.MaBnNavigation != null ? pk.MaBnNavigation.GioiTinh : null,
                    sdt           = pk.MaBnNavigation != null ? pk.MaBnNavigation.Sdt : null,
                    lyDoKham      = pk.LyDoKham,
                    maBacSi       = pk.MaNv,
                    tenBacSi      = pk.MaNvNavigation != null ? pk.MaNvNavigation.HoTen : null,
                    ngayKhamRaw   = pk.NgayKham,
                    trangThaiKham = pk.TrangThaiKham
                })
                .ToListAsync();

            return Ok(new
            {
                data = data.Select(pk => new
                {
                    pk.maPhieu, pk.maBN, pk.hoTen, pk.gioiTinh, pk.sdt,
                    pk.lyDoKham, pk.maBacSi, pk.tenBacSi,
                    ngayKham      = pk.ngayKhamRaw?.ToString("o"),
                    pk.trangThaiKham
                }),
                pagination = new { page, limit, total, totalPages }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LayDanhSachBenhNhanChoKham] Lỗi khi lấy danh sách bệnh nhân");
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // ───────────────────────────────────────────────────────────────────────
    // GET api/KhamBenh/{maPhieu}
    //   — Xem chi tiết phiếu khám (màn hình khám bệnh)
    // Phân quyền: BacSi, Admin
    //   BacSi chỉ xem phiếu của chính mình; Admin xem tất cả.
    // ───────────────────────────────────────────────────────────────────────
    [HttpGet("{maPhieu}")]
    [Authorize(Roles = "BacSi,Admin")]
    public async Task<IActionResult> XemChiTietPhieuKham(string maPhieu)
    {
        try
        {
            var ttdn = await LayThongTinDangNhapAsync();
            if (ttdn == null)
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });

            var phieuKham = await LayPhieuKhamDayDuAsync(maPhieu);
            if (phieuKham == null)
                return NotFound(new { message = "Không tìm thấy phiếu khám cần xem" });

            // BacSi chỉ xem phiếu do chính mình phụ trách
            if (!CoQuyenTruyCapPhieu(phieuKham, ttdn))
                return StatusCode(403, new { message = "Bạn không có quyền xem phiếu khám này" });

            return Ok(new { data = TaoResponsePhieuKham(phieuKham) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[XemChiTietPhieuKham] Lỗi khi xem phiếu khám {MaPhieu}", maPhieu);
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // ───────────────────────────────────────────────────────────────────────
    // PUT api/KhamBenh/{maPhieu}
    //   — Cập nhật thông tin khám bệnh
    //     (sinh hiệu + ICD / chỉ định CLS / toggle trạng thái CLS / đơn thuốc / kết luận)
    // Phân quyền: BacSi, Admin
    //   - icdList: thuộc nhóm Khám cơ bản 
    //   - Ràng buộc ICD: chuyển TrangThaiKham 0→1 phải có ít nhất 1 ICD
    //   - Nhóm 3 : trangThaiCLS — toggle 2 chiều, gộp vào PUT này
    //   - Nhóm 4 (đơn thuốc):
    //       + Chặn nếu còn CLS TrangThaiDichVu = 0
    //       + Kiểm tra tồn kho theo FEFO; chặn cứng nếu không đủ
    //       + Trừ SoLuongTon theo FEFO trong cùng transaction
    // ───────────────────────────────────────────────────────────────────────
    [HttpPut("{maPhieu}")]
    [Authorize(Roles = "BacSi,Admin")]
    public async Task<IActionResult> CapNhatThongTinKhamBenh(
        string maPhieu,
        [FromBody] CapNhatKhamBenhRequest request)
    {
        try
        {
            // Lấy thông tin đăng nhập
            var ttdn = await LayThongTinDangNhapAsync();
            if (ttdn == null)
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });

            // Kiểm tra phiếu tồn tại
            var phieuKham = await _context.PhieuKhams
                .Include(pk => pk.MaIcds)
                .FirstOrDefaultAsync(pk => pk.MaPhieu == maPhieu);

            if (phieuKham == null)
                return NotFound(new { message = "Không tìm thấy phiếu khám cần cập nhật" });

            // BacSi chỉ được sửa phiếu của chính mình
            if (!CoQuyenTruyCapPhieu(phieuKham, ttdn))
                return StatusCode(403, new { message = "Bạn không có quyền cập nhật phiếu khám này" });

            // Phiếu đã Hoàn thành(3) — chỉ Admin được sửa
            if (phieuKham.TrangThaiKham == 3 && !ttdn.IsAdmin)
                return StatusCode(403, new { message = "Phiếu khám đã hoàn thành, không thể chỉnh sửa" });

            // Validate trangThaiKham sớm (trước khi mở transaction)
            if (request.TrangThaiKham.HasValue
                && !new[] { 1, 2, 3 }.Contains(request.TrangThaiKham.Value))
                return BadRequest(new { message = "Giá trị trạng thái không hợp lệ. Chỉ chấp nhận: 1 | 2 | 3" });

            // Validate sinh hiệu
            var loiSinhHieu = ValidateSinhHieu(request);
            if (loiSinhHieu != null)
                return BadRequest(new { message = loiSinhHieu });

            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Nhóm 1 — Sinh hiệu + ICD
            if (request.Mach.HasValue)     phieuKham.Mach    = request.Mach.Value;
            if (request.NhietDo.HasValue)  phieuKham.NhietDo = request.NhietDo.Value;
            if (!string.IsNullOrWhiteSpace(request.HuyetAp))
                phieuKham.HuyetAp = request.HuyetAp.Trim();
            if (request.CanNang.HasValue)  phieuKham.CanNang  = request.CanNang.Value;
            if (request.ChieuCao.HasValue) phieuKham.ChieuCao = request.ChieuCao.Value;

            if (request.IcdList != null)
            {
                var maIcdList = request.IcdList
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().ToUpper())
                    .Distinct()
                    .ToList();

                var icdEntities = await _context.DanhMucIcds
                    .Where(icd => maIcdList.Contains(icd.MaIcd))
                    .ToListAsync();

                if (icdEntities.Count != maIcdList.Count)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { message = "Mã bệnh ICD không tồn tại trong danh mục. Vui lòng kiểm tra lại" });
                }

                // Ghi đè toàn bộ ICD
                phieuKham.MaIcds.Clear();
                foreach (var icd in icdEntities)
                    phieuKham.MaIcds.Add(icd);
            }

            // Ràng buộc: chuyển TrangThaiKham 0→1 phải có ít nhất 1 ICD
            if (request.TrangThaiKham == 1 && phieuKham.TrangThaiKham == 0)
            {
                // ICD sau khi cập nhật (kể cả ICD mới vừa gửi lên)
                bool coICD = phieuKham.MaIcds.Any()
                             || (request.IcdList != null && request.IcdList.Any(s => !string.IsNullOrWhiteSpace(s)));
                if (!coICD)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { message = "Vui lòng nhập ít nhất 1 chẩn đoán ICD trước khi lưu khám cơ bản!" });
                }
            }

            // Nhóm 2 — Chỉ định CLS mới
            if (request.ChiDinhCLSMoi != null)
            {
                var maDvMoi = request.ChiDinhCLSMoi
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (string maDv in maDvMoi)
                {
                    bool dvHopLe = await _context.ChiTietDichVuYtes
                        .AnyAsync(dv => dv.MaDv == maDv && dv.TrangThai == true);
                    if (!dvHopLe)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = "Dịch vụ CLS không tồn tại hoặc đã ngừng cung cấp. Vui lòng kiểm tra lại" });
                    }

                    bool daChiDinh = await _context.DichVuYtes
                        .AnyAsync(dv => dv.MaPhieu == maPhieu && dv.MaDv == maDv);
                    if (daChiDinh)
                    {
                        continue;
                    }

                    _context.DichVuYtes.Add(new DichVuYte
                    {
                        MaPhieu         = maPhieu,
                        MaDv            = maDv,
                        TrangThaiDichVu = 0   // Chưa thực hiện
                    });
                }
            }

            // Nhóm 3 — Toggle trạng thái CLS
            if (request.TrangThaiCLS != null && request.TrangThaiCLS.Count > 0)
            {
                foreach (var clsItem in request.TrangThaiCLS)
                {
                    var dichVuYte = await _context.DichVuYtes
                        .FirstOrDefaultAsync(dv => dv.MaChiTiet == clsItem.MaChiTiet
                                                && dv.MaPhieu   == maPhieu);
                    if (dichVuYte == null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = "Chỉ định CLS không hợp lệ hoặc không thuộc phiếu khám này" });
                    }

                    // Toggle 2 chiều tự do (không giới hạn số lần đổi)
                    dichVuYte.TrangThaiDichVu = clsItem.DaLamCLS ? 1 : 0;
                    if (!clsItem.DaLamCLS)
                    {
                        // Toggle "Đã làm" → "Chưa làm": coi như hủy kết quả, làm lại từ đầu
                        dichVuYte.KetQua = null;
                    }
                }
            }

            // Lưu các thay đổi B5-B7 trước khi xử lý đơn thuốc (B8)
            await _context.SaveChangesAsync();

            // Nhóm 4 — Đơn thuốc (REPLACE semantics với hoàn/trừ kho chính xác theo lô)
            if (request.DonThuoc?.Count > 0 || request.LoiDan != null)
            {
                // Chặn kê đơn nếu còn CLS chưa thực hiện (TrangThaiDichVu = 0)
                bool conClsChuaLam = await _context.DichVuYtes
                    .AnyAsync(dv => dv.MaPhieu == maPhieu && dv.TrangThaiDichVu == 0);
                if (conClsChuaLam)
                {
                    await transaction.RollbackAsync();
                    return Conflict(new { message = "Còn dịch vụ cận lâm sàng chưa thực hiện xong. Vui lòng hoàn tất CLS trước khi kê thuốc!" });
                }

                // Lấy hoặc tạo mới DonThuoc
                var donThuoc = await _context.DonThuocs
                    .Include(dt => dt.ChiTietDonThuocs)
                        .ThenInclude(ct => ct.ChiTietDonThuocLos)
                    .OrderBy(dt => dt.NgayKeDon)
                    .FirstOrDefaultAsync(dt => dt.MaPhieu == maPhieu);

                if (donThuoc == null)
                {
                    // Tạo mới đơn thuốc (retry tối đa 3 lần tránh trùng mã)
                    DonThuoc? donThuocMoi = null;
                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        try
                        {
                            donThuocMoi = new DonThuoc
                            {
                                MaDonThuoc = await TaoMaDonThuocMoiAsync(),
                                MaPhieu    = maPhieu,
                                NgayKeDon  = DateTime.Now,
                                LoiDan     = ChuanHoaChuoi(request.LoiDan)
                            };
                            _context.DonThuocs.Add(donThuocMoi);
                            await _context.SaveChangesAsync();
                            break;
                        }
                        catch (DbUpdateException dbEx) when (attempt < 3)
                        {
                            _logger.LogWarning(dbEx,
                                "[TaoMaDonThuoc] Trùng mã lần {Attempt}, thử lại...", attempt);
                            _context.ChangeTracker.Clear();

                            // Reload lại phieuKham và ICD sau khi clear tracker
                            phieuKham = await _context.PhieuKhams
                                .Include(pk => pk.MaIcds)
                                .FirstAsync(pk => pk.MaPhieu == maPhieu);
                        }
                    }
                    donThuoc = donThuocMoi;
                }
                else if (request.LoiDan != null)
                {
                    donThuoc.LoiDan = ChuanHoaChuoi(request.LoiDan);
                }

                // Xử lý danh sách thuốc
                if (request.DonThuoc != null && donThuoc != null)
                {
                    // ── BƯỚC 1: Validate đầu vào (giữ nguyên logic hiện có) ──
                    var maThuocList = new List<string>();
                    foreach (var item in request.DonThuoc)
                    {
                        if (string.IsNullOrWhiteSpace(item.MaThuoc))
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { message = "Thuốc không tồn tại hoặc đã ngừng sử dụng. Vui lòng kiểm tra lại" });
                        }
                        if (!item.SoLuong.HasValue || item.SoLuong.Value <= 0)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { message = "Số lượng thuốc phải là số nguyên dương. Vui lòng nhập lại" });
                        }
                        if (string.IsNullOrWhiteSpace(item.CachDung))
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { message = "Vui lòng nhập cách dùng thuốc" });
                        }
                        maThuocList.Add(item.MaThuoc.Trim());
                    }

                    if (maThuocList.Count != maThuocList.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = "Danh sách thuốc không được chứa mã thuốc trùng nhau" });
                    }

                    // Chặn nếu request gửi lại mã thuốc đã phát
                    bool trungThuocDaPhat = donThuoc.ChiTietDonThuocs
                        .Any(ct => ct.TrangThaiPhatThuoc == true
                                && maThuocList.Contains(ct.MaThuoc, StringComparer.OrdinalIgnoreCase));
                    if (trungThuocDaPhat)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = "Không thể ghi đè thuốc đã phát trong đơn thuốc" });
                    }

                    // ── BƯỚC 2: Phân loại dòng chưa phát ──
                    var dongChuaPhat = donThuoc.ChiTietDonThuocs
                        .Where(ct => ct.TrangThaiPhatThuoc != true)
                        .ToList();

                    var requestDict = request.DonThuoc
                        .ToDictionary(i => i.MaThuoc.Trim(), i => i, StringComparer.OrdinalIgnoreCase);

                    // dongBiXoa: có trong DB (chưa phát) nhưng KHÔNG có trong request
                    var dongBiXoa = dongChuaPhat
                        .Where(ct => !requestDict.ContainsKey(ct.MaThuoc))
                        .ToList();

                    // dongGiuNguyen: có ở cả 2, SoLuong KHÔNG đổi
                    var dongGiuNguyen = dongChuaPhat
                        .Where(ct => requestDict.ContainsKey(ct.MaThuoc)
                                  && ct.SoLuong == requestDict[ct.MaThuoc].SoLuong!.Value)
                        .ToList();

                    // dongCanSua: có ở cả 2, SoLuong CÓ đổi
                    var dongCanSua = dongChuaPhat
                        .Where(ct => requestDict.ContainsKey(ct.MaThuoc)
                                  && ct.SoLuong != requestDict[ct.MaThuoc].SoLuong!.Value)
                        .ToList();

                    // dongMoi: có trong request nhưng KHÔNG có trong DB
                    var maThuocHienCo = dongChuaPhat.Select(ct => ct.MaThuoc).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    // Thuốc đã phát cũng coi là "đã tồn tại" — không kê lại
                    var maThuocDaPhat = donThuoc.ChiTietDonThuocs
                        .Where(ct => ct.TrangThaiPhatThuoc == true)
                        .Select(ct => ct.MaThuoc)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var dongMoi = request.DonThuoc
                        .Where(i => !maThuocHienCo.Contains(i.MaThuoc.Trim())
                                 && !maThuocDaPhat.Contains(i.MaThuoc.Trim()))
                        .ToList();

                    // ── BƯỚC 3: Hoàn kho cho (dongBiXoa + dongCanSua) theo breakdown cũ ──
                    var dongCanHoan = dongBiXoa.Concat(dongCanSua).ToList();
                    foreach (var ct in dongCanHoan)
                    {
                        var breakdownList = await _context.ChiTietDonThuocLos
                            .Where(b => b.MaDonThuoc == ct.MaDonThuoc && b.MaThuoc == ct.MaThuoc)
                            .ToListAsync();

                        if (breakdownList.Count == 0)
                        {
                            _logger.LogError(
                                "[CapNhatDonThuoc] Không tìm thấy breakdown cho thuốc {MaThuoc} đơn {MaDon}. Dữ liệu cũ trước khi có bảng breakdown.",
                                ct.MaThuoc, ct.MaDonThuoc);
                            await transaction.RollbackAsync();
                            return StatusCode(500, new { message = "Không thể xác định lô đã trừ cho dòng kê này. Vui lòng liên hệ quản trị viên để xử lý thủ công" });
                        }

                        // Hoàn SoLuongTru vào đúng lô tương ứng
                        foreach (var bd in breakdownList)
                        {
                            var loThuoc = await _context.LoThuocs
                                .FirstAsync(lo => lo.MaLo == bd.MaLo);
                            loThuoc.SoLuongTon = (loThuoc.SoLuongTon ?? 0) + bd.SoLuongTru;
                        }

                        // Xóa breakdown rows
                        _context.ChiTietDonThuocLos.RemoveRange(breakdownList);
                    }

                    // Xóa dòng ChiTietDonThuoc của (dongBiXoa + dongCanSua) khỏi DB
                    _context.ChiTietDonThuocs.RemoveRange(dongCanHoan);

                    // Flush để tồn kho đúng trước khi kiểm tra khả dụng
                    await _context.SaveChangesAsync();

                    // ── BƯỚC 4: Kiểm tra tồn kho cho (dongCanSua + dongMoi) ──
                    DateOnly homNay = DateOnly.FromDateTime(DateTime.Now);

                    var dongCanTruMoi = dongCanSua
                        .Select(ct => requestDict[ct.MaThuoc])
                        .Concat(dongMoi)
                        .ToList();

                    foreach (var item in dongCanTruMoi)
                    {
                        string maThuoc      = item.MaThuoc.Trim();
                        int    soLuongCanKe = item.SoLuong!.Value;

                        var thuoc = await _context.DanhMucThuocs
                            .FirstOrDefaultAsync(t => t.MaThuoc == maThuoc && t.IsActive);
                        if (thuoc == null)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { message = "Thuốc không tồn tại hoặc đã ngừng sử dụng. Vui lòng kiểm tra lại" });
                        }

                        int tonKhaDung = await _context.LoThuocs
                            .Where(lo => lo.MaThuoc == maThuoc
                                      && lo.HanSuDung >= homNay
                                      && lo.SoLuongTon > 0)
                            .SumAsync(lo => (int?)lo.SoLuongTon ?? 0);

                        if (tonKhaDung < soLuongCanKe)
                        {
                            await transaction.RollbackAsync();
                            return Conflict(new
                            {
                                message = $"Thuốc '{thuoc.TenThuoc}' không đủ tồn kho (còn {tonKhaDung}, yêu cầu {soLuongCanKe}). Vui lòng kiểm tra lại hoặc liên hệ kho thuốc!"
                            });
                        }
                    }

                    // ── BƯỚC 5: Trừ FEFO + ghi breakdown cho (dongCanSua + dongMoi) ──
                    foreach (var item in dongCanTruMoi)
                    {
                        string maThuoc      = item.MaThuoc.Trim();
                        int    soLuongCanKe = item.SoLuong!.Value;

                        var loThuocList = await _context.LoThuocs
                            .Where(lo => lo.MaThuoc == maThuoc
                                      && lo.HanSuDung >= homNay
                                      && lo.SoLuongTon > 0)
                            .OrderBy(lo => lo.HanSuDung)
                            .ToListAsync();

                        int conLai = soLuongCanKe;
                        foreach (var lo in loThuocList)
                        {
                            if (conLai <= 0) break;

                            int truLo = Math.Min(conLai, lo.SoLuongTon ?? 0);
                            lo.SoLuongTon -= truLo;
                            conLai        -= truLo;

                            // Ghi breakdown cho lô này
                            _context.ChiTietDonThuocLos.Add(new ChiTietDonThuocLo
                            {
                                MaDonThuoc = donThuoc.MaDonThuoc,
                                MaThuoc    = maThuoc,
                                MaLo       = lo.MaLo,
                                SoLuongTru = truLo
                            });
                        }

                        // INSERT dòng ChiTietDonThuoc mới
                        _context.ChiTietDonThuocs.Add(new ChiTietDonThuoc
                        {
                            MaDonThuoc         = donThuoc.MaDonThuoc,
                            MaThuoc            = maThuoc,
                            SoLuong            = soLuongCanKe,
                            CachDung           = item.CachDung.Trim(),
                            TrangThaiPhatThuoc = false
                        });
                    }

                    // ── BƯỚC 6: dongGiuNguyen — không làm gì (fix bug double-deduct) ──
                    // Cập nhật CachDung nếu thay đổi (SoLuong giữ nguyên, CachDung có thể sửa)
                    foreach (var ct in dongGiuNguyen)
                    {
                        if (requestDict.TryGetValue(ct.MaThuoc, out var reqItem))
                        {
                            ct.CachDung = reqItem.CachDung.Trim();
                        }
                    }

                    await _context.SaveChangesAsync();
                }
            }

            // Nhóm 4b — Kê vật tư (REPLACE toàn bộ danh sách theo FEFO)
            // VatTuList = null → không đụng vào vật tư
            // VatTuList = [] (rỗng) → xóa toàn bộ vật tư đã kê (hoàn kho hết)
            // VatTuList = [...] → REPLACE: phân loại + hoàn/trừ theo lô
            if (request.VatTuList != null)
            {
                // Chặn cứng nếu phiếu không có bất kỳ chỉ định CLS nào
                bool coChiDinhCLS = await _context.DichVuYtes
                    .AnyAsync(dv => dv.MaPhieu == maPhieu);
                if (!coChiDinhCLS)
                {
                    await transaction.RollbackAsync();
                    return Conflict(new { message = "Phiếu khám chưa có chỉ định CLS. Chỉ được kê vật tư cho phiếu có CLS!" });
                }

                // Validate danh sách (bỏ qua nếu rỗng — xóa hết)
                var maVatTuList = new List<string>();
                foreach (var item in request.VatTuList)
                {
                    if (string.IsNullOrWhiteSpace(item.MaVatTu))
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = "Vật tư không tồn tại hoặc đã ngừng sử dụng. Vui lòng kiểm tra lại" });
                    }
                    if (!item.SoLuong.HasValue || item.SoLuong.Value <= 0)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = "Số lượng vật tư phải là số nguyên dương. Vui lòng nhập lại" });
                    }
                    maVatTuList.Add(item.MaVatTu.Trim());
                }

                if (maVatTuList.Count != maVatTuList.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                {
                    await transaction.RollbackAsync();
                    return Conflict(new { message = "Danh sách vật tư không được chứa mã vật tư trùng nhau trong cùng một request" });
                }

                // Lấy toàn bộ vật tư hiện có của phiếu + breakdown
                var vatTuHienCo = await _context.ChiTietVatTuPhieuKhams
                    .Include(ct => ct.ChiTietVatTuLos)
                    .Where(ct => ct.MaPhieu == maPhieu)
                    .ToListAsync();

                // ── Phân loại ──
                var vtRequestDict = request.VatTuList
                    .ToDictionary(i => i.MaVatTu.Trim(), i => i, StringComparer.OrdinalIgnoreCase);

                var vtDongBiXoa = vatTuHienCo
                    .Where(ct => !vtRequestDict.ContainsKey(ct.MaVatTu))
                    .ToList();

                var vtDongGiuNguyen = vatTuHienCo
                    .Where(ct => vtRequestDict.ContainsKey(ct.MaVatTu)
                              && ct.SoLuong == vtRequestDict[ct.MaVatTu].SoLuong!.Value)
                    .ToList();

                var vtDongCanSua = vatTuHienCo
                    .Where(ct => vtRequestDict.ContainsKey(ct.MaVatTu)
                              && ct.SoLuong != vtRequestDict[ct.MaVatTu].SoLuong!.Value)
                    .ToList();

                var maVatTuHienCo = vatTuHienCo.Select(ct => ct.MaVatTu).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var vtDongMoi = request.VatTuList
                    .Where(i => !maVatTuHienCo.Contains(i.MaVatTu.Trim()))
                    .ToList();

                // ── Hoàn kho cho (dongBiXoa + dongCanSua) theo breakdown cũ ──
                var vtDongCanHoan = vtDongBiXoa.Concat(vtDongCanSua).ToList();
                foreach (var ct in vtDongCanHoan)
                {
                    var breakdownList = ct.ChiTietVatTuLos.ToList();

                    if (breakdownList.Count == 0)
                    {
                        _logger.LogError(
                            "[CapNhatVatTu] Không tìm thấy breakdown cho vật tư {MaVatTu} phiếu {MaPhieu}. Dữ liệu cũ trước khi có bảng breakdown.",
                            ct.MaVatTu, ct.MaPhieu);
                        await transaction.RollbackAsync();
                        return StatusCode(500, new { message = "Không thể xác định lô đã trừ cho dòng kê này. Vui lòng liên hệ quản trị viên để xử lý thủ công" });
                    }

                    foreach (var bd in breakdownList)
                    {
                        var loVatTu = await _context.LoVatTus
                            .FirstAsync(lo => lo.MaLo == bd.MaLo);
                        loVatTu.SoLuongTon += bd.SoLuongTru;
                    }

                    _context.ChiTietVatTuLos.RemoveRange(breakdownList);
                }

                _context.ChiTietVatTuPhieuKhams.RemoveRange(vtDongCanHoan);
                await _context.SaveChangesAsync();

                // ── Kiểm tra tồn kho + trừ FEFO + ghi breakdown cho (dongCanSua + dongMoi) ──
                DateOnly homNayVT = DateOnly.FromDateTime(DateTime.Now);

                var vtDongCanTruMoi = vtDongCanSua
                    .Select(ct => vtRequestDict[ct.MaVatTu])
                    .Concat(vtDongMoi)
                    .ToList();

                foreach (var item in vtDongCanTruMoi)
                {
                    string maVatTu      = item.MaVatTu.Trim();
                    int    soLuongCanKe = item.SoLuong!.Value;

                    var vatTu = await _context.DanhMucVatTus
                        .FirstOrDefaultAsync(v => v.MaVatTu == maVatTu && v.IsActive);
                    if (vatTu == null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = "Vật tư không tồn tại hoặc đã ngừng sử dụng. Vui lòng kiểm tra lại" });
                    }

                    int tonKhaDung = await _context.LoVatTus
                        .Where(lo => lo.MaVatTu == maVatTu
                                  && lo.HanSuDung >= homNayVT
                                  && lo.SoLuongTon > 0)
                        .SumAsync(lo => (int?)lo.SoLuongTon ?? 0);

                    if (tonKhaDung < soLuongCanKe)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new
                        {
                            message = $"Vật tư '{vatTu.TenVatTu}' không đủ tồn kho (còn {tonKhaDung}, yêu cầu {soLuongCanKe}). Vui lòng kiểm tra lại hoặc liên hệ kho!"
                        });
                    }

                    var loVatTuList = await _context.LoVatTus
                        .Where(lo => lo.MaVatTu == maVatTu
                                  && lo.HanSuDung >= homNayVT
                                  && lo.SoLuongTon > 0)
                        .OrderBy(lo => lo.HanSuDung)
                        .ToListAsync();

                    decimal giaBanLoDauTien = loVatTuList.First().GiaBan;
                    int     conLai          = soLuongCanKe;
                    foreach (var lo in loVatTuList)
                    {
                        if (conLai <= 0) break;
                        int truLo  = Math.Min(conLai, lo.SoLuongTon);
                        lo.SoLuongTon -= truLo;
                        conLai        -= truLo;

                        // Ghi breakdown cho lô này
                        _context.ChiTietVatTuLos.Add(new ChiTietVatTuLo
                        {
                            MaPhieu    = maPhieu,
                            MaVatTu    = maVatTu,
                            MaLo       = lo.MaLo,
                            SoLuongTru = truLo
                        });
                    }

                    // INSERT dòng ChiTietVatTuPhieuKham mới
                    _context.ChiTietVatTuPhieuKhams.Add(new ChiTietVatTuPhieuKham
                    {
                        MaPhieu = maPhieu,
                        MaVatTu = maVatTu,
                        SoLuong = soLuongCanKe,
                        DonGia  = giaBanLoDauTien
                    });
                }

                // dongGiuNguyen: không làm gì — giữ nguyên dòng cũ + breakdown cũ

                await _context.SaveChangesAsync();
            }

            // Nhóm 5 — Kết luận khám
            if (request.KetLuan != null)
                phieuKham.KetLuan = ChuanHoaChuoi(request.KetLuan);

            // Chuyển trạng thái phiếu khám
            if (request.TrangThaiKham.HasValue)
            {
                // Muốn Hoàn thành(3): bắt buộc KetLuan không rỗng
                if (request.TrangThaiKham == 3)
                {
                    string? ketLuanSauCapNhat = ChuanHoaChuoi(request.KetLuan) ?? phieuKham.KetLuan;
                    if (string.IsNullOrWhiteSpace(ketLuanSauCapNhat))
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = "Vui lòng nhập Kết luận khám trước khi hoàn thành" });
                    }
                }
                phieuKham.TrangThaiKham = request.TrangThaiKham.Value;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Trả HTTP 200 kèm toàn bộ phiếu sau cập nhật
            var phieuSauCapNhat = await LayPhieuKhamDayDuAsync(maPhieu);
            return Ok(new
            {
                message = "Cập nhật thông tin khám bệnh thành công",
                data    = TaoResponsePhieuKham(phieuSauCapNhat!)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[CapNhatThongTinKhamBenh] Lỗi khi cập nhật phiếu khám {MaPhieu}", maPhieu);
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


    // ───────────────────────────────────────────────────────────────────────
    // DELETE api/KhamBenh/{maPhieu}/chi-dinh-cls/{maChiTiet}
    //   — Xóa một chỉ định CLS chưa thực hiện khỏi phiếu khám
    // Phân quyền: BacSi, Admin
    //   - BacSi: chỉ xóa trên phiếu của mình
    //   - Phiếu Hoàn thành(3): chỉ Admin được xóa
    //   - Điều kiện: TrangThaiDichVu == 0 VÀ KetQua null/rỗng
    // ───────────────────────────────────────────────────────────────────────
    [HttpDelete("{maPhieu}/chi-dinh-cls/{maChiTiet}")]
    [Authorize(Roles = "BacSi,Admin")]
    public async Task<IActionResult> XoaChiDinhCLS(string maPhieu, int maChiTiet)
    {
        try
        {
            // 1. Xác thực đăng nhập
            var ttdn = await LayThongTinDangNhapAsync();
            if (ttdn == null)
                return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });

            // 2. Kiểm tra phiếu khám tồn tại
            var phieuKham = await _context.PhieuKhams
                .FirstOrDefaultAsync(pk => pk.MaPhieu == maPhieu);
            if (phieuKham == null)
                return NotFound(new { message = "Không tìm thấy phiếu khám cần cập nhật" });

            // 3. Kiểm tra quyền truy cập phiếu
            if (!CoQuyenTruyCapPhieu(phieuKham, ttdn))
                return StatusCode(403, new { message = "Bạn không có quyền cập nhật phiếu khám này" });

            // 4. Phiếu Hoàn thành(3) — chỉ Admin được sửa
            if (phieuKham.TrangThaiKham == 3 && !ttdn.IsAdmin)
                return StatusCode(403, new { message = "Phiếu khám đã hoàn thành, không thể chỉnh sửa" });

            // 5. Tìm bản ghi DichVuYte theo (MaChiTiet, MaPhieu)
            var dichVuYte = await _context.DichVuYtes
                .FirstOrDefaultAsync(dv => dv.MaChiTiet == maChiTiet && dv.MaPhieu == maPhieu);
            if (dichVuYte == null)
                return NotFound(new { message = "Chỉ định CLS không hợp lệ hoặc không thuộc phiếu khám này" });

            // 6. Kiểm tra điều kiện xóa: TrangThaiDichVu == 0 VÀ KetQua null/rỗng
            if (dichVuYte.TrangThaiDichVu != 0 || !string.IsNullOrWhiteSpace(dichVuYte.KetQua))
                return Conflict(new { message = "Không thể xóa chỉ định CLS đã thực hiện hoặc đã có kết quả" });

            // 7. Xóa bản ghi DichVuYte
            _context.DichVuYtes.Remove(dichVuYte);

            // 8. SaveChangesAsync
            await _context.SaveChangesAsync();

            // 9. Trả về 200 kèm phiếu khám sau cập nhật
            var phieuSauCapNhat = await LayPhieuKhamDayDuAsync(maPhieu);
            return Ok(new
            {
                message = "Xóa chỉ định CLS thành công",
                data    = TaoResponsePhieuKham(phieuSauCapNhat!)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[XoaChiDinhCLS] Lỗi khi xóa chỉ định CLS {MaChiTiet} phiếu {MaPhieu}", maChiTiet, maPhieu);
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    private async Task<ThongTinDangNhap?> LayThongTinDangNhapAsync()
    {
        string? userIdClaim = User.FindFirstValue("userID");
        if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return null;

        bool isAdmin = User.IsInRole("Admin");
        bool isBacSi = User.IsInRole("BacSi");

        var nhanVien = await _context.NhanViens
            .AsNoTracking()
            .FirstOrDefaultAsync(nv => nv.UserId == userId);

        // BacSi bắt buộc phải có bản ghi NhanVien
        if (isBacSi && nhanVien == null)
            return null;

        return new ThongTinDangNhap
        {
            IsAdmin = isAdmin,
            IsBacSi = isBacSi,
            MaNv    = nhanVien?.MaNv
        };
    }

    private static bool CoQuyenTruyCapPhieu(PhieuKham phieuKham, ThongTinDangNhap ttdn)
    {
        if (ttdn.IsAdmin) return true;
        return ttdn.IsBacSi && phieuKham.MaNv == ttdn.MaNv;
    }

    private async Task<PhieuKham?> LayPhieuKhamDayDuAsync(string maPhieu)
    {
        return await _context.PhieuKhams
            .AsNoTracking()
            .Include(pk => pk.MaBnNavigation)
            .Include(pk => pk.MaNvNavigation)
            .Include(pk => pk.MaIcds)
            .Include(pk => pk.DichVuYtes)
                .ThenInclude(dv => dv.MaDvNavigation)
            .Include(pk => pk.DonThuocs)
                .ThenInclude(dt => dt.ChiTietDonThuocs)
                    .ThenInclude(ct => ct.MaThuocNavigation)
            .Include(pk => pk.ChiTietVatTuPhieuKhams)
                .ThenInclude(ct => ct.MaVatTuNavigation)
            .FirstOrDefaultAsync(pk => pk.MaPhieu == maPhieu);
    }

    private static object TaoResponsePhieuKham(PhieuKham phieuKham)
    {
        var donThuoc = phieuKham.DonThuocs
            .OrderByDescending(dt => dt.NgayKeDon)
            .FirstOrDefault();

        return new
        {
            maPhieu       = phieuKham.MaPhieu,
            trangThaiKham = phieuKham.TrangThaiKham,
            ngayKham      = phieuKham.NgayKham?.ToString("o"),
            lyDoKham      = phieuKham.LyDoKham,
            ketLuan       = phieuKham.KetLuan,
            maBacSi       = phieuKham.MaNv,
            tenBacSi      = phieuKham.MaNvNavigation?.HoTen,

            benhNhan = new
            {
                maBN       = phieuKham.MaBnNavigation?.MaBn ?? phieuKham.MaBn,
                hoTen      = phieuKham.MaBnNavigation?.HoTen,
                ngaySinh   = phieuKham.MaBnNavigation?.NgaySinh?.ToString("dd-MM-yyyy"),
                gioiTinh   = phieuKham.MaBnNavigation?.GioiTinh,
                sdt        = phieuKham.MaBnNavigation?.Sdt,
                diaChi     = phieuKham.MaBnNavigation?.DiaChi,
                tienSuBenh = phieuKham.MaBnNavigation?.TienSuBenh
            },

            sinhHieu = new
            {
                mach     = phieuKham.Mach,
                nhietDo  = phieuKham.NhietDo,
                huyetAp  = phieuKham.HuyetAp,
                canNang  = phieuKham.CanNang,
                chieuCao = phieuKham.ChieuCao
            },
            //ICD
            icdList = phieuKham.MaIcds
                .OrderBy(icd => icd.MaIcd)
                .Select(icd => new { maICD = icd.MaIcd, tenBenh = icd.TenBenh })
                .ToList(),

            // trangThaiCLS: 0=Chưa thực hiện | 1=Đã làm CLS
            chiDinhCLS = phieuKham.DichVuYtes
                .OrderBy(dv => dv.MaChiTiet)
                .Select(dv => new
                {
                    maChiTiet    = dv.MaChiTiet,
                    maDV         = dv.MaDv,
                    tenDV        = dv.MaDvNavigation?.TenDv,
                    giaTien      = dv.MaDvNavigation?.GiaTien,
                    ketQua       = dv.KetQua,
                    trangThaiCLS = dv.TrangThaiDichVu   // field name mới theo spec v2
                })
                .ToList(),

            donThuoc = donThuoc == null
                ? new
                {
                    maDonThuoc = (string?)null,
                    loiDan     = (string?)null,
                    chiTiet    = new List<object>()
                }
                : new
                {
                    maDonThuoc = (string?)donThuoc.MaDonThuoc,
                    loiDan     = (string?)donThuoc.LoiDan,
                    chiTiet    = donThuoc.ChiTietDonThuocs
                        .OrderBy(ct => ct.MaThuoc)
                        .Select(ct => (object)new
                        {
                            maThuoc            = ct.MaThuoc,
                            tenThuoc           = ct.MaThuocNavigation?.TenThuoc,
                            soLuong            = ct.SoLuong,
                            cachDung           = ct.CachDung,
                            trangThaiPhatThuoc = ct.TrangThaiPhatThuoc ?? false
                        })
                        .ToList()
                },

            // Danh sách vật tư đã kê
            // Chỉ có dữ liệu khi phiếu có chỉ định CLS; phiếu không có CLS → mảng rỗng
            vatTu = phieuKham.ChiTietVatTuPhieuKhams
                .OrderBy(ct => ct.MaVatTu)
                .Select(ct => new
                {
                    maVatTu    = ct.MaVatTu,
                    tenVatTu   = ct.MaVatTuNavigation?.TenVatTu,
                    donViTinh  = ct.MaVatTuNavigation?.DonViTinh,
                    soLuong    = ct.SoLuong,
                    donGia     = ct.DonGia
                })
                .ToList()
        };
    }

    private static string? ValidateSinhHieu(CapNhatKhamBenhRequest req)
    {
        if (req.Mach.HasValue    && req.Mach.Value    <= 0) return "Giá trị sinh hiệu không hợp lệ. Vui lòng nhập lại";
        if (req.NhietDo.HasValue && req.NhietDo.Value <= 0) return "Giá trị sinh hiệu không hợp lệ. Vui lòng nhập lại";
        if (req.CanNang.HasValue && req.CanNang.Value  <= 0) return "Giá trị sinh hiệu không hợp lệ. Vui lòng nhập lại";
        if (req.ChieuCao.HasValue && req.ChieuCao.Value <= 0) return "Giá trị sinh hiệu không hợp lệ. Vui lòng nhập lại";

        if (!string.IsNullOrWhiteSpace(req.HuyetAp)
            && !Regex.IsMatch(req.HuyetAp.Trim(), @"^\d{2,3}/\d{2,3}$"))
            return "Huyết áp không đúng định dạng (VD: 120/80)";

        return null;
    }

    private async Task<string> TaoMaDonThuocMoiAsync()
    {
        string today  = DateTime.Now.ToString("yyMMdd");
        string prefix = $"DT{today}";

        var maCuoiCung = await _context.DonThuocs
            .Where(dt => dt.MaDonThuoc.StartsWith(prefix))
            .OrderByDescending(dt => dt.MaDonThuoc)
            .Select(dt => dt.MaDonThuoc)
            .FirstOrDefaultAsync();

        int soTiepTheo = 1;
        if (maCuoiCung != null
            && maCuoiCung.Length > prefix.Length
            && int.TryParse(maCuoiCung[prefix.Length..], out int soHienTai))
        {
            soTiepTheo = soHienTai + 1;
        }

        return $"{prefix}{soTiepTheo:D3}";
    }

    private static string? ChuanHoaChuoi(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
