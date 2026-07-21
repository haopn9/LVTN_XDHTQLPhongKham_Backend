using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Security.Claims;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Authorize]
public class ThanhToanController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;

    public ThanhToanController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }

    // ================================================================
    // DTO DEFINITIONS
    // ================================================================

    public class ThemVatTuPhieuRequest
    {
        public string MaVatTu { get; set; } = string.Empty;
        public int SoLuong { get; set; }
    }

    public class XacNhanThanhToanRequest
    {
        public string MaPhieu { get; set; } = string.Empty;
        public string PhuongThucTT { get; set; } = string.Empty;
    }

    // ================================================================
    // SHARED SERVICE: GiaThuocVatTu
    // ================================================================

    /// <summary>
    /// Tính đơn giá bình quân FEFO (First-Expired-First-Out) cho thuốc.
    /// Trả về (donGia, canhBao). canhBao = true khi phải dùng fallback.
    /// Không trừ kho.
    /// </summary>
    private async Task<(decimal donGia, bool canhBao)> GetGiaThuocFEFO(string maThuoc, int soLuongCan)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Lô còn hạn, còn tồn, sắp hết hạn trước
        var loHopLe = await _context.LoThuocs
            .AsNoTracking()
            .Where(l => l.MaThuoc == maThuoc
                     && l.SoLuongTon > 0
                     && l.HanSuDung.HasValue
                     && l.HanSuDung.Value >= today)
            .OrderBy(l => l.HanSuDung)
            .ToListAsync();

        decimal tongTien = 0;
        int tonLay = 0;

        foreach (var lo in loHopLe)
        {
            if (tonLay >= soLuongCan) break;
            int layTuLo = Math.Min(lo.SoLuongTon ?? 0, soLuongCan - tonLay);
            tongTien += layTuLo * (lo.GiaBan ?? 0);
            tonLay += layTuLo;
        }

        bool canhBao = false;

        if (tonLay < soLuongCan)
        {
            // Fallback: lô mới nhập nhất (bất kể hạn)
            canhBao = true;
            var loFallback = await _context.LoThuocs
                .AsNoTracking()
                .Where(l => l.MaThuoc == maThuoc)
                .OrderByDescending(l => l.NgaySanXuat)
                .FirstOrDefaultAsync();

            int soLuongThieu = soLuongCan - tonLay;
            decimal giaFallback = loFallback?.GiaBan ?? 0;
            tongTien += soLuongThieu * giaFallback;
            tonLay += soLuongThieu;
        }

        decimal donGia = soLuongCan > 0 ? tongTien / soLuongCan : 0;
        return (donGia, canhBao);
    }

    /// <summary>
    /// Lấy đơn giá tham khảo FEFO cho vật tư (không trừ kho).
    /// Trả về (donGia, canhBao).
    /// </summary>
    private async Task<(decimal donGia, bool canhBao)> GetGiaVatTuFEFO(string maVatTu)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var loHopLe = await _context.LoVatTus
            .AsNoTracking()
            .Where(l => l.MaVatTu == maVatTu
                     && l.SoLuongTon > 0
                     && l.HanSuDung >= today)
            .OrderBy(l => l.HanSuDung)
            .FirstOrDefaultAsync();

        if (loHopLe != null)
            return (loHopLe.GiaBan, false);

        // Fallback: lô có NgaySanXuat gần nhất bất kể hạn
        var loFallback = await _context.LoVatTus
            .AsNoTracking()
            .Where(l => l.MaVatTu == maVatTu)
            .OrderByDescending(l => l.NgaySanXuat)
            .FirstOrDefaultAsync();

        return (loFallback?.GiaBan ?? 0, true);
    }

    /// <summary>
    /// Parse CachDung để tính số lượng quy đổi (viên/ml/...).
    /// Format CachDung được kỳ vọng: "X viên x Y lần/ngày x Z ngày"
    /// Fallback về SoLuong trong ChiTietDonThuoc nếu không parse được.
    /// </summary>
    private static int TinhSoLuongQuyDoi(ChiTietDonThuoc chiTiet)
    {
        // Thử parse theo format: "X viên x Y lần/ngày x Z ngày"
        // hoặc các biến thể: số × số × số
        if (!string.IsNullOrWhiteSpace(chiTiet.CachDung))
        {
            try
            {
                // Tách các số trong chuỗi CachDung
                var numbers = System.Text.RegularExpressions.Regex
                    .Matches(chiTiet.CachDung, @"\d+(?:[.,]\d+)?")
                    .Select(m => decimal.TryParse(m.Value.Replace(',', '.'),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var v) ? v : 0)
                    .Where(v => v > 0)
                    .ToList();

                // Mẫu phổ biến: "soLuongMoiLan x soLanNgay x soNgay"
                if (numbers.Count >= 3)
                {
                    int quyDoi = (int)Math.Ceiling(numbers[0] * numbers[1] * numbers[2]);
                    if (quyDoi > 0) return quyDoi;
                }
                // Mẫu 2 số: "soLuongMoiLan x soLanNgay" – dùng SoLuong làm số ngày
                if (numbers.Count == 2)
                {
                    int quyDoi = (int)Math.Ceiling(numbers[0] * numbers[1] * (chiTiet.SoLuong ?? 1));
                    if (quyDoi > 0) return quyDoi;
                }
            }
            catch { /* ignore, fallback */ }
        }

        // Fallback: SoLuong đã là tổng số lượng
        return chiTiet.SoLuong ?? 1;
    }

    // ================================================================
    // 4.1. GET api/ThanhToan/danh-sach
    // Danh sách phiếu khám (trangThai = All/Unpaid/Paid), tìm kiếm, phân trang
    // Phân quyền: Admin, ThuNgan
    // ================================================================

    [HttpGet("api/ThanhToan/danh-sach")]
    [Authorize(Roles = "Admin,ThuNgan")]
    public async Task<IActionResult> LayDanhSachPhieu(
        [FromQuery] string? search = null,
        [FromQuery] string? trangThai = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            if (page < 1)
                return BadRequest(new { message = "Giá trị page phải >= 1" });

            if (pageSize > 100)
                return BadRequest(new { message = "pageSize tối đa là 100" });

            if (pageSize < 1) pageSize = 20;

            // Chuẩn hoá trangThai
            var ttNorm = (trangThai ?? "All").Trim();
            if (ttNorm != "All" && ttNorm != "Unpaid" && ttNorm != "Paid")
                return BadRequest(new { message = "trangThai chỉ chấp nhận: All | Unpaid | Paid" });

            // Base query: PhieuKham đã hoàn thành khám (TrangThaiKham = 3)
            var query = _context.PhieuKhams
                .AsNoTracking()
                .Where(pk => pk.TrangThaiKham == 3)
                .Include(pk => pk.MaBnNavigation)
                .Include(pk => pk.MaNvNavigation)
                .Include(pk => pk.HoaDons)
                .AsQueryable();

            // Áp bộ lọc search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(pk =>
                    (pk.MaBnNavigation != null && pk.MaBnNavigation.HoTen.ToLower().Contains(s)) ||
                    (pk.MaBn != null && pk.MaBn.ToLower().Contains(s)) ||
                    pk.MaPhieu.ToLower().Contains(s));
            }

            // Áp bộ lọc trạng thái thanh toán
            if (ttNorm == "Paid")
                query = query.Where(pk => pk.HoaDons.Any(hd => hd.TrangThaiThanhToan == true));
            else if (ttNorm == "Unpaid")
                query = query.Where(pk => !pk.HoaDons.Any(hd => hd.TrangThaiThanhToan == true));

            // Đếm tổng trước khi phân trang
            int totalCount = await query.CountAsync();

            // Sắp xếp và phân trang
            var items = await query
                .OrderByDescending(pk => pk.NgayKham)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(pk => new
                {
                    maPhieu   = pk.MaPhieu,
                    maBN      = pk.MaBn,
                    hoTen     = pk.MaBnNavigation != null ? pk.MaBnNavigation.HoTen : "",
                    ngaySinh  = pk.MaBnNavigation != null ? pk.MaBnNavigation.NgaySinh : (DateOnly?)null,
                    gioiTinh  = pk.MaBnNavigation != null ? pk.MaBnNavigation.GioiTinh : null,
                    tenBacSi  = pk.MaNvNavigation != null ? pk.MaNvNavigation.HoTen : "",
                    ngayKham  = pk.NgayKham,
                    daThanhToan = pk.HoaDons.Any(hd => hd.TrangThaiThanhToan == true)
                })
                .ToListAsync();

            return Ok(new
            {
                data       = items,
                totalCount,
                page,
                pageSize
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi hệ thống", detail = ex.Message });
        }
    }

    // ================================================================
    // 4.2. GET api/ThanhToan/{maPhieu}/chi-tiet
    // Chi tiết chi phí của 1 phiếu khám (CLS, thuốc, vật tư)
    // Phân quyền: Admin, ThuNgan
    // ================================================================

    [HttpGet("api/ThanhToan/{maPhieu}/chi-tiet")]
    [Authorize(Roles = "Admin,ThuNgan")]
    public async Task<IActionResult> LayChiTietPhieu(string maPhieu)
    {
        try
        {
            // Kiểm tra phiếu khám tồn tại
            var phieu = await _context.PhieuKhams
                .AsNoTracking()
                .Include(pk => pk.MaBnNavigation)
                .Include(pk => pk.MaNvNavigation)
                .Include(pk => pk.MaIcds)                       // Chẩn đoán ICD
                .Include(pk => pk.HoaDons)
                    .ThenInclude(hd => hd.MaNvNavigation)
                .Include(pk => pk.DichVuYtes)
                    .ThenInclude(dv => dv.MaDvNavigation)
                .Include(pk => pk.DonThuocs)
                    .ThenInclude(dt => dt.ChiTietDonThuocs)
                        .ThenInclude(ctdt => ctdt.MaThuocNavigation)
                .Include(pk => pk.ChiTietVatTuPhieuKhams)
                    .ThenInclude(vt => vt.MaVatTuNavigation)
                .FirstOrDefaultAsync(pk => pk.MaPhieu == maPhieu);

            if (phieu == null)
                return NotFound(new { message = $"Không tìm thấy phiếu khám '{maPhieu}'" });

            if (phieu.TrangThaiKham != 3)
                return Conflict(new { message = "Phiếu khám chưa hoàn thành khám lâm sàng, chưa đủ điều kiện lập hóa đơn" });

            // -- CLS --
            var clsItems = phieu.DichVuYtes
                .Where(dv => dv.MaDvNavigation != null)
                .Select(dv => new
                {
                    maDV       = dv.MaDv,
                    tenDV      = dv.MaDvNavigation!.TenDv,
                    soLuong    = 1,
                    donGia     = dv.MaDvNavigation.GiaTien,
                    thanhTien  = dv.MaDvNavigation.GiaTien
                })
                .ToList();

            decimal tongTienDichVu = clsItems.Sum(c => c.thanhTien);

            // -- Thuốc (tính FEFO không gây N+1 vì đã load trong memory) --
            var thuocItems = new List<object>();
            decimal tongTienThuoc = 0;

            foreach (var donThuoc in phieu.DonThuocs)
            {
                foreach (var ct in donThuoc.ChiTietDonThuocs)
                {
                    int soLuongQuyDoi = TinhSoLuongQuyDoi(ct);
                    var (donGia, canhBao) = await GetGiaThuocFEFO(ct.MaThuoc, soLuongQuyDoi);
                    decimal thanhTien = soLuongQuyDoi * donGia;
                    tongTienThuoc += thanhTien;

                    thuocItems.Add(new
                    {
                        maThuoc        = ct.MaThuoc,
                        tenThuoc       = ct.MaThuocNavigation?.TenThuoc ?? "",
                        cachDung       = ct.CachDung,
                        soLuongQuyDoi,
                        donGia         = Math.Round(donGia, 2),
                        thanhTien      = Math.Round(thanhTien, 2),
                        canhBao
                    });
                }
            }

            // -- Vật tư --
            var vatTuItems = phieu.ChiTietVatTuPhieuKhams
                .Select(vt => new
                {
                    maVatTu   = vt.MaVatTu,
                    tenVatTu  = vt.MaVatTuNavigation?.TenVatTu ?? "",
                    soLuong   = vt.SoLuong,
                    donGia    = vt.DonGia,
                    thanhTien = vt.SoLuong * vt.DonGia
                })
                .ToList();

            decimal tongTienVatTu    = vatTuItems.Sum(v => v.thanhTien);
            decimal tongTienThanhToan = tongTienDichVu + tongTienThuoc + tongTienVatTu;

            // Thông tin hóa đơn nếu đã thanh toán
            var hoaDon = phieu.HoaDons.FirstOrDefault(hd => hd.TrangThaiThanhToan == true);

            return Ok(new
            {
                maPhieu,
                benhNhan = phieu.MaBnNavigation == null ? null : new
                {
                    maBN     = phieu.MaBnNavigation.MaBn,
                    hoTen    = phieu.MaBnNavigation.HoTen,
                    ngaySinh = phieu.MaBnNavigation.NgaySinh
                },
                bacSi = phieu.MaNvNavigation == null ? null : new
                {
                    maNV  = phieu.MaNvNavigation.MaNv,
                    hoTen = phieu.MaNvNavigation.HoTen
                },
                icdList = phieu.MaIcds
                    .OrderBy(icd => icd.MaIcd)
                    .Select(icd => new { maICD = icd.MaIcd, tenBenh = icd.TenBenh })
                    .ToList(),
                cls         = clsItems,
                thuoc       = thuocItems,
                vatTu       = vatTuItems,
                tongTienDichVu    = Math.Round(tongTienDichVu, 2),
                tongTienThuoc     = Math.Round(tongTienThuoc, 2),
                tongTienVatTu     = Math.Round(tongTienVatTu, 2),
                tongTienThanhToan = Math.Round(tongTienThanhToan, 2),
                daThanhToan       = hoaDon != null,
                hoaDon = hoaDon == null ? null : new
                {
                    maHoaDon     = hoaDon.MaHoaDon,
                    phuongThucTT = hoaDon.PhuongThucTt,
                    ngayThanhToan = hoaDon.NgayThanhToan,
                    tenThuNgan   = hoaDon.MaNvNavigation?.HoTen
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi hệ thống", detail = ex.Message });
        }
    }

    // ================================================================
    // 4.3. POST api/ThanhToan/{maPhieu}/vat-tu
    // Kê thêm vật tư phát sinh vào phiếu (chưa thanh toán)
    // Phân quyền: Admin, ThuNgan
    // ================================================================

    [HttpPost("api/ThanhToan/{maPhieu}/vat-tu")]
    [Authorize(Roles = "Admin,ThuNgan")]
    public async Task<IActionResult> ThemVatTu(string maPhieu, [FromBody] ThemVatTuPhieuRequest request)
    {
        try
        {
            // Kiểm tra soLuong
            if (request.SoLuong <= 0)
                return BadRequest(new { message = "Số lượng phải lớn hơn 0" });

            if (string.IsNullOrWhiteSpace(request.MaVatTu))
                return BadRequest(new { message = "MaVatTu không được để trống" });

            // Kiểm tra phiếu khám
            var phieu = await _context.PhieuKhams
                .Include(pk => pk.HoaDons)
                .FirstOrDefaultAsync(pk => pk.MaPhieu == maPhieu);

            if (phieu == null)
                return NotFound(new { message = $"Không tìm thấy phiếu khám '{maPhieu}'" });

            if (phieu.TrangThaiKham != 3)
                return Conflict(new { message = "Phiếu khám chưa hoàn thành khám, không thể kê vật tư" });

            if (phieu.HoaDons.Any(hd => hd.TrangThaiThanhToan == true))
                return Conflict(new { message = "Phiếu đã thanh toán, không thể kê thêm vật tư" });

            // Kiểm tra vật tư
            var vatTu = await _context.DanhMucVatTus
                .AsNoTracking()
                .FirstOrDefaultAsync(vt => vt.MaVatTu == request.MaVatTu);

            if (vatTu == null)
                return NotFound(new { message = $"Không tìm thấy vật tư '{request.MaVatTu}'" });

            if (!vatTu.IsActive)
                return BadRequest(new { message = "Vật tư không còn hoạt động" });

            // Lấy giá FEFO tham khảo (snapshot tại thời điểm kê)
            var (donGia, _) = await GetGiaVatTuFEFO(request.MaVatTu);

            // Kiểm tra đã có dòng vật tư này trong phiếu chưa (composite PK)
            var existing = await _context.ChiTietVatTuPhieuKhams
                .FirstOrDefaultAsync(ct => ct.MaPhieu == maPhieu && ct.MaVatTu == request.MaVatTu);

            if (existing != null)
            {
                // Cập nhật số lượng thay vì thêm trùng (PK là composite MaPhieu + MaVatTu)
                existing.SoLuong += request.SoLuong;
                // Cập nhật đơn giá theo thời điểm kê mới nhất
                existing.DonGia = donGia;
            }
            else
            {
                var chiTiet = new ChiTietVatTuPhieuKham
                {
                    MaPhieu = maPhieu,
                    MaVatTu = request.MaVatTu,
                    SoLuong = request.SoLuong,
                    DonGia  = donGia
                };
                _context.ChiTietVatTuPhieuKhams.Add(chiTiet);
            }

            await _context.SaveChangesAsync();

            var soLuongMoi = existing?.SoLuong ?? request.SoLuong;

            return StatusCode(201, new
            {
                maPhieu,
                maVatTu   = request.MaVatTu,
                tenVatTu  = vatTu.TenVatTu,
                soLuong   = soLuongMoi,
                donGia    = Math.Round(donGia, 2),
                thanhTien = Math.Round(soLuongMoi * donGia, 2)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi hệ thống", detail = ex.Message });
        }
    }

    // ================================================================
    // 4.4. DELETE api/ThanhToan/{maPhieu}/vat-tu/{maVatTu}
    // Xóa vật tư khỏi phiếu (chưa thanh toán)
    // Phân quyền: Admin, ThuNgan
    // ================================================================

    [HttpDelete("api/ThanhToan/{maPhieu}/vat-tu/{maVatTu}")]
    [Authorize(Roles = "Admin,ThuNgan")]
    public async Task<IActionResult> XoaVatTu(string maPhieu, string maVatTu)
    {
        try
        {
            // Kiểm tra phiếu đã thanh toán chưa
            var daThanhToan = await _context.HoaDons
                .AsNoTracking()
                .AnyAsync(hd => hd.MaPhieu == maPhieu && hd.TrangThaiThanhToan == true);

            if (daThanhToan)
                return Conflict(new { message = "Phiếu đã thanh toán, không thể xóa vật tư" });

            // Kiểm tra dòng vật tư tồn tại
            var chiTiet = await _context.ChiTietVatTuPhieuKhams
                .FirstOrDefaultAsync(ct => ct.MaPhieu == maPhieu && ct.MaVatTu == maVatTu);

            if (chiTiet == null)
                return NotFound(new { message = "Không tìm thấy dòng vật tư tương ứng trên phiếu" });

            // Hard delete
            _context.ChiTietVatTuPhieuKhams.Remove(chiTiet);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi hệ thống", detail = ex.Message });
        }
    }

    // ================================================================
    // 4.5. POST api/ThanhToan/xac-nhan
    // Xác nhận thanh toán, tạo hóa đơn (F12)
    // Phân quyền: Admin, ThuNgan
    // MaNV lấy từ JWT claim, không từ body
    // ================================================================

    [HttpPost("api/ThanhToan/xac-nhan")]
    [Authorize(Roles = "Admin,ThuNgan")]
    public async Task<IActionResult> XacNhanThanhToan([FromBody] XacNhanThanhToanRequest request)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.MaPhieu))
            return BadRequest(new { message = "maPhieu không được để trống" });

        var ptHopLe = new[] { "Tiền mặt", "Chuyển khoản" };
        if (!ptHopLe.Contains(request.PhuongThucTT))
            return BadRequest(new { message = "phuongThucTT chỉ chấp nhận: 'Tiền mặt' hoặc 'Chuyển khoản'" });

        // Lấy MaNV từ JWT
        string? userIdClaim = User.FindFirstValue("userID");
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });

        var nhanVienThuNgan = await _context.NhanViens
            .AsNoTracking()
            .FirstOrDefaultAsync(nv => nv.UserId == userId);

        if (nhanVienThuNgan == null)
            return Unauthorized(new { message = "Không tìm thấy thông tin nhân viên, vui lòng đăng nhập lại" });

        // Sử dụng transaction để đảm bảo tính toàn vẹn
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Bước 1: Khóa & kiểm tra phiếu (EF Core với Serializable isolation)
            var phieu = await _context.PhieuKhams
                .Include(pk => pk.HoaDons)
                .Include(pk => pk.DichVuYtes)
                    .ThenInclude(dv => dv.MaDvNavigation)
                .Include(pk => pk.DonThuocs)
                    .ThenInclude(dt => dt.ChiTietDonThuocs)
                .Include(pk => pk.ChiTietVatTuPhieuKhams)
                .FirstOrDefaultAsync(pk => pk.MaPhieu == request.MaPhieu);

            if (phieu == null)
                return NotFound(new { message = $"Không tìm thấy phiếu khám '{request.MaPhieu}'" });

            if (phieu.TrangThaiKham != 3)
                return Conflict(new { message = "Phiếu khám chưa hoàn thành, không thể lập hóa đơn" });

            // Chặn double-submit
            if (phieu.HoaDons.Any(hd => hd.TrangThaiThanhToan == true))
                return Conflict(new { message = "Phiếu này đã được thanh toán rồi" });

            // Bước 2: Tính lại tổng tiền ở backend (không tin FE)
            decimal tongTienDichVu = phieu.DichVuYtes
                .Where(dv => dv.MaDvNavigation != null)
                .Sum(dv => dv.MaDvNavigation!.GiaTien);

            decimal tongTienThuoc = 0;
            foreach (var donThuoc in phieu.DonThuocs)
            {
                foreach (var ct in donThuoc.ChiTietDonThuocs)
                {
                    int soLuongQuyDoi = TinhSoLuongQuyDoi(ct);
                    var (donGia, _) = await GetGiaThuocFEFO(ct.MaThuoc, soLuongQuyDoi);
                    tongTienThuoc += soLuongQuyDoi * donGia;
                }
            }

            decimal tongTienVatTu = phieu.ChiTietVatTuPhieuKhams
                .Sum(vt => vt.SoLuong * vt.DonGia);

            decimal thanhTien = tongTienDichVu + tongTienThuoc + tongTienVatTu;

            // Bước 3: Sinh MaHoaDon với retry (tối đa 5 lần)
            string maHoaDon = string.Empty;
            bool inserted = false;
            int retryCount = 0;
            var ngayHienTai = DateTime.Now;

            while (!inserted && retryCount < 5)
            {
                string suffix = Random.Shared.Next(100, 999).ToString();
                maHoaDon = $"HD{ngayHienTai:yyMMdd}{suffix}";

                var exists = await _context.HoaDons.AnyAsync(hd => hd.MaHoaDon == maHoaDon);
                if (!exists)
                {
                    inserted = true;
                }
                retryCount++;
            }

            if (!inserted)
                return StatusCode(500, new { message = "Không thể sinh mã hóa đơn sau 5 lần thử, vui lòng thực hiện lại" });

            // Bước 4: Insert HoaDon
            var hoaDon = new HoaDon
            {
                MaHoaDon          = maHoaDon,
                MaPhieu           = request.MaPhieu,
                MaNv              = nhanVienThuNgan.MaNv,
                NgayThanhToan     = ngayHienTai,
                TongTienDichVu    = Math.Round(tongTienDichVu, 2),
                TongTienThuoc     = Math.Round(tongTienThuoc, 2),
                TongTienVatTu     = Math.Round(tongTienVatTu, 2),
                ThanhTien         = Math.Round(thanhTien, 2),
                TrangThaiThanhToan = true,
                PhuongThucTt      = request.PhuongThucTT
            };

            _context.HoaDons.Add(hoaDon);
            await _context.SaveChangesAsync();

            // Bước 5: Commit
            await transaction.CommitAsync();

            return StatusCode(201, new
            {
                maHoaDon,
                maPhieu           = request.MaPhieu,
                maNV              = nhanVienThuNgan.MaNv,
                tenThuNgan        = nhanVienThuNgan.HoTen,
                ngayThanhToan     = ngayHienTai,
                phuongThucTT      = request.PhuongThucTT,
                tongTienDichVu    = Math.Round(tongTienDichVu, 2),
                tongTienThuoc     = Math.Round(tongTienThuoc, 2),
                tongTienVatTu     = Math.Round(tongTienVatTu, 2),
                thanhTien         = Math.Round(thanhTien, 2)
            });
        }
        catch (DbUpdateException dbEx)
        {
            await transaction.RollbackAsync();
            // Trường hợp PK trùng do race condition cực kỳ hiếm gặp
            if (dbEx.InnerException?.Message.Contains("duplicate") == true ||
                dbEx.InnerException?.Message.Contains("PRIMARY KEY") == true)
                return StatusCode(500, new { message = "Sinh mã hóa đơn bị trùng (race condition), vui lòng thực hiện lại" });

            return StatusCode(500, new { message = "Lỗi lưu dữ liệu", detail = dbEx.Message });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Lỗi hệ thống", detail = ex.Message });
        }
    }

    // ================================================================
    // 4.6. GET api/ThanhToan/{maHoaDon}/pdf
    // Xuất file PDF hóa đơn (QuestPDF)
    // Phân quyền: Admin, ThuNgan
    // ================================================================

    [HttpGet("api/ThanhToan/{maHoaDon}/pdf")]
    [Authorize(Roles = "Admin,ThuNgan")]
    public async Task<IActionResult> XuatPdfHoaDon(string maHoaDon)
    {
        try
        {
            // Truy vấn hóa đơn + đầy đủ thông tin liên kết
            var hoaDon = await _context.HoaDons
                .AsNoTracking()
                .Include(hd => hd.MaNvNavigation)         // Thu ngân
                .Include(hd => hd.MaPhieuNavigation)
                    .ThenInclude(pk => pk!.MaBnNavigation) // Bệnh nhân
                .Include(hd => hd.MaPhieuNavigation)
                    .ThenInclude(pk => pk!.MaNvNavigation) // Bác sĩ
                .Include(hd => hd.MaPhieuNavigation)
                    .ThenInclude(pk => pk!.MaIcds)         // Chẩn đoán ICD
                .Include(hd => hd.MaPhieuNavigation)
                    .ThenInclude(pk => pk!.DichVuYtes)
                        .ThenInclude(dv => dv.MaDvNavigation)
                .Include(hd => hd.MaPhieuNavigation)
                    .ThenInclude(pk => pk!.DonThuocs)
                        .ThenInclude(dt => dt.ChiTietDonThuocs)
                            .ThenInclude(ctdt => ctdt.MaThuocNavigation)
                .Include(hd => hd.MaPhieuNavigation)
                    .ThenInclude(pk => pk!.ChiTietVatTuPhieuKhams)
                        .ThenInclude(vt => vt.MaVatTuNavigation)
                .FirstOrDefaultAsync(hd => hd.MaHoaDon == maHoaDon);

            if (hoaDon == null)
                return NotFound(new { message = $"Không tìm thấy hóa đơn '{maHoaDon}'" });

            var phieu = hoaDon.MaPhieuNavigation;

            // Lấy danh sách chi tiết để hiển thị trên PDF
            var clsItems = phieu?.DichVuYtes
                .Where(dv => dv.MaDvNavigation != null)
                .Select(dv => new
                {
                    Ten       = dv.MaDvNavigation!.TenDv,
                    SoLuong   = 1,
                    DonGia    = dv.MaDvNavigation.GiaTien,
                    ThanhTien = dv.MaDvNavigation.GiaTien
                })
                .ToList() ?? new();

            // Tính lại thuốc (FEFO) cho mục đích hiển thị trên PDF
            var thuocItems = new List<(string Ten, int SoLuong, decimal DonGia, decimal ThanhTien)>();
            if (phieu != null)
            {
                foreach (var donThuoc in phieu.DonThuocs)
                {
                    foreach (var ct in donThuoc.ChiTietDonThuocs)
                    {
                        int soLuongQuyDoi = TinhSoLuongQuyDoi(ct);
                        var (donGia, _) = await GetGiaThuocFEFO(ct.MaThuoc, soLuongQuyDoi);
                        decimal thanhTien = soLuongQuyDoi * donGia;
                        thuocItems.Add((
                            ct.MaThuocNavigation?.TenThuoc ?? ct.MaThuoc,
                            soLuongQuyDoi,
                            Math.Round(donGia, 2),
                            Math.Round(thanhTien, 2)
                        ));
                    }
                }
            }

            var vatTuItems = phieu?.ChiTietVatTuPhieuKhams
                .Select(vt => new
                {
                    Ten       = vt.MaVatTuNavigation?.TenVatTu ?? vt.MaVatTu,
                    SoLuong   = vt.SoLuong,
                    DonGia    = vt.DonGia,
                    ThanhTien = vt.SoLuong * vt.DonGia
                })
                .ToList() ?? new();

            // ICD chẩn đoán — hiển thị trên PDF
            var icdItems = phieu?.MaIcds
                .OrderBy(icd => icd.MaIcd)
                .Select(icd => $"{icd.MaIcd} – {icd.TenBenh}")
                .ToList() ?? new();

            // === Build PDF bằng QuestPDF ===
            var benhNhan  = phieu?.MaBnNavigation;
            var bacSi     = phieu?.MaNvNavigation;
            var thuNgan   = hoaDon.MaNvNavigation;

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(ts => ts.FontFamily("Roboto").FontSize(10));

                    page.Content().Column(col =>
                    {
                        // === HEADER ===
                        col.Item().AlignCenter().Text("PHÒNG KHÁM ĐA KHOA NHẬT TẢO")
                            .FontSize(14).Bold().FontColor(Colors.Blue.Darken3);

                        col.Item().AlignCenter().Text("Địa chỉ: 123 Đường Nhật Tảo, Phường 7, Quận 10, TP. HCM")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);

                        col.Item().AlignCenter().Text("Điện thoại: (028) 3864 xxxx")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);

                        col.Item().Height(8);

                        col.Item().AlignCenter().Text("HÓA ĐƠN VIỆN PHÍ")
                            .FontSize(16).Bold().FontColor(Colors.Black);

                        col.Item().AlignCenter().Text($"Số hóa đơn: {maHoaDon}")
                            .FontSize(10).Bold();

                        col.Item().Height(10);

                        // === THÔNG TIN BỆNH NHÂN ===
                        col.Item().Background(Colors.Blue.Lighten5).Padding(8).Column(info =>
                        {
                            info.Item().Row(row =>
                            {
                                row.RelativeItem().Text(t =>
                                {
                                    t.Span("Họ tên BN: ").Bold();
                                    t.Span(benhNhan?.HoTen ?? "—");
                                });
                                row.RelativeItem().Text(t =>
                                {
                                    t.Span("Mã BN: ").Bold();
                                    t.Span(benhNhan?.MaBn ?? "—");
                                });
                            });

                            info.Item().Row(row =>
                            {
                                row.RelativeItem().Text(t =>
                                {
                                    t.Span("Ngày sinh: ").Bold();
                                    t.Span(benhNhan?.NgaySinh?.ToString("dd/MM/yyyy") ?? "—");
                                });
                                row.RelativeItem().Text(t =>
                                {
                                    t.Span("Giới tính: ").Bold();
                                    t.Span(benhNhan?.GioiTinh ?? "—");
                                });
                            });

                            info.Item().Row(row =>
                            {
                                row.RelativeItem().Text(t =>
                                {
                                    t.Span("Bác sĩ điều trị: ").Bold();
                                    t.Span(bacSi?.HoTen ?? "—");
                                });
                                row.RelativeItem().Text(t =>
                                {
                                    t.Span("Ngày khám: ").Bold();
                                    t.Span(phieu?.NgayKham?.ToString("dd/MM/yyyy HH:mm") ?? "—");
                                });
                            });
                        });

                        col.Item().Height(10);

                        // === CHẨN ĐOÁN ICD ===
                        if (icdItems.Count > 0)
                        {
                            col.Item().Background(Colors.Green.Lighten5).Padding(6).Row(row =>
                            {
                                row.AutoItem().Text("Chẩn đoán: ").Bold().FontSize(10);
                                row.RelativeItem().Text(string.Join("  |  ", icdItems)).FontSize(10);
                            });
                            col.Item().Height(8);
                        }

                        // === BẢNG CHI TIẾT CHI PHÍ ===

                        // Helper: tạo header bảng
                        static IContainer HeaderStyle(IContainer c) =>
                            c.Background(Colors.Blue.Darken3).Padding(5);

                        static IContainer CellStyle(IContainer c) =>
                            c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4);

                        // -- Dịch vụ CLS --
                        if (clsItems.Count > 0)
                        {
                            col.Item().Text("I. Dịch vụ cận lâm sàng (CLS)").Bold().FontSize(11);
                            col.Item().Height(4);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(30);
                                    cols.RelativeColumn(4);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).AlignCenter()
                                        .Text("#").FontColor(Colors.White).Bold();
                                    header.Cell().Element(HeaderStyle)
                                        .Text("Tên dịch vụ").FontColor(Colors.White).Bold();
                                    header.Cell().Element(HeaderStyle).AlignCenter()
                                        .Text("SL").FontColor(Colors.White).Bold();
                                    header.Cell().Element(HeaderStyle).AlignRight()
                                        .Text("Đơn giá").FontColor(Colors.White).Bold();
                                    header.Cell().Element(HeaderStyle).AlignRight()
                                        .Text("Thành tiền").FontColor(Colors.White).Bold();
                                });

                                int stt = 1;
                                foreach (var item in clsItems)
                                {
                                    table.Cell().Element(CellStyle).AlignCenter().Text(stt++.ToString());
                                    table.Cell().Element(CellStyle).Text(item.Ten);
                                    table.Cell().Element(CellStyle).AlignCenter().Text(item.SoLuong.ToString());
                                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.DonGia:N0} đ");
                                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.ThanhTien:N0} đ");
                                }
                            });

                            col.Item().AlignRight().Text(t =>
                            {
                                t.Span("Tổng dịch vụ: ").Bold();
                                t.Span($"{hoaDon.TongTienDichVu:N0} đ");
                            });
                            col.Item().Height(8);
                        }

                        // -- Thuốc --
                        if (thuocItems.Count > 0)
                        {
                            col.Item().Text("II. Thuốc").Bold().FontSize(11);
                            col.Item().Height(4);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(30);
                                    cols.RelativeColumn(4);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).AlignCenter()
                                        .Text("#").FontColor(Colors.White).Bold();
                                    header.Cell().Element(HeaderStyle)
                                        .Text("Tên thuốc").FontColor(Colors.White).Bold();
                                    header.Cell().Element(HeaderStyle).AlignCenter()
                                        .Text("SL").FontColor(Colors.White).Bold();
                                    header.Cell().Element(HeaderStyle).AlignRight()
                                        .Text("Đơn giá").FontColor(Colors.White).Bold();
                                    header.Cell().Element(HeaderStyle).AlignRight()
                                        .Text("Thành tiền").FontColor(Colors.White).Bold();
                                });

                                int stt = 1;
                                foreach (var (Ten, SoLuong, DonGia, ThanhTien) in thuocItems)
                                {
                                    table.Cell().Element(CellStyle).AlignCenter().Text(stt++.ToString());
                                    table.Cell().Element(CellStyle).Text(Ten);
                                    table.Cell().Element(CellStyle).AlignCenter().Text(SoLuong.ToString());
                                    table.Cell().Element(CellStyle).AlignRight().Text($"{DonGia:N0} đ");
                                    table.Cell().Element(CellStyle).AlignRight().Text($"{ThanhTien:N0} đ");
                                }
                            });

                            col.Item().AlignRight().Text(t =>
                            {
                                t.Span("Tổng thuốc: ").Bold();
                                t.Span($"{hoaDon.TongTienThuoc:N0} đ");
                            });
                            col.Item().Height(8);
                        }

                        // -- Vật tư --
                        if (vatTuItems.Count > 0)
                        {
                            col.Item().Text("III. Vật tư tiêu hao").Bold().FontSize(11);
                            col.Item().Height(4);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(30);
                                    cols.RelativeColumn(4);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).AlignCenter()
                                        .Text("#").FontColor(Colors.White).Bold();
                                    header.Cell().Element(HeaderStyle)
                                        .Text("Tên vật tư").FontColor(Colors.White).Bold();
                                    header.Cell().Element(HeaderStyle).AlignCenter()
                                        .Text("SL").FontColor(Colors.White).Bold();
                                    header.Cell().Element(HeaderStyle).AlignRight()
                                        .Text("Đơn giá").FontColor(Colors.White).Bold();
                                    header.Cell().Element(HeaderStyle).AlignRight()
                                        .Text("Thành tiền").FontColor(Colors.White).Bold();
                                });

                                int stt = 1;
                                foreach (var item in vatTuItems)
                                {
                                    table.Cell().Element(CellStyle).AlignCenter().Text(stt++.ToString());
                                    table.Cell().Element(CellStyle).Text(item.Ten);
                                    table.Cell().Element(CellStyle).AlignCenter().Text(item.SoLuong.ToString());
                                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.DonGia:N0} đ");
                                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.ThanhTien:N0} đ");
                                }
                            });

                            col.Item().AlignRight().Text(t =>
                            {
                                t.Span("Tổng vật tư: ").Bold();
                                t.Span($"{hoaDon.TongTienVatTu:N0} đ");
                            });
                            col.Item().Height(8);
                        }

                        // === TỔNG CỘNG ===
                        col.Item().LineHorizontal(1).LineColor(Colors.Blue.Darken3);
                        col.Item().Height(6);

                        col.Item().Background(Colors.Blue.Lighten5).Padding(8).Column(sum =>
                        {
                            sum.Item().Row(row =>
                            {
                                row.RelativeItem().Text(t =>
                                {
                                    t.Span("Phương thức thanh toán: ").Bold();
                                    t.Span(hoaDon.PhuongThucTt ?? "—");
                                });
                                row.ConstantItem(200).AlignRight().Text(t =>
                                {
                                    t.Span("TỔNG THANH TOÁN: ").Bold().FontSize(13);
                                    t.Span($"{hoaDon.ThanhTien:N0} đ").Bold().FontSize(13)
                                        .FontColor(Colors.Red.Darken2);
                                });
                            });
                        });

                        col.Item().Height(20);

                        // === CHỮ KÝ ===
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(sig =>
                            {
                                sig.Item().AlignCenter().Text("Bệnh nhân").Bold();
                                sig.Item().AlignCenter().Text("(Ký, ghi rõ họ tên)").FontSize(8).FontColor(Colors.Grey.Darken1);
                                sig.Item().Height(40);
                                sig.Item().AlignCenter().Text(benhNhan?.HoTen ?? "").FontSize(9);
                            });

                            row.RelativeItem().Column(sig =>
                            {
                                sig.Item().AlignCenter()
                                    .Text($"Ngày {hoaDon.NgayThanhToan?.Day ?? DateTime.Now.Day:D2} " +
                                          $"tháng {hoaDon.NgayThanhToan?.Month ?? DateTime.Now.Month:D2} " +
                                          $"năm {hoaDon.NgayThanhToan?.Year ?? DateTime.Now.Year}")
                                    .FontSize(9).FontColor(Colors.Grey.Darken1);
                                sig.Item().AlignCenter().Text("Thu ngân").Bold();
                                sig.Item().AlignCenter().Text("(Ký, ghi rõ họ tên)").FontSize(8).FontColor(Colors.Grey.Darken1);
                                sig.Item().Height(40);
                                sig.Item().AlignCenter().Text(thuNgan?.HoTen ?? "").FontSize(9);
                            });
                        });

                        // Footer nhỏ
                        col.Item().Height(10);
                        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                        col.Item().AlignCenter()
                            .Text($"In lúc: {DateTime.Now:dd/MM/yyyy HH:mm:ss} | Mã HĐ: {maHoaDon}")
                            .FontSize(7).FontColor(Colors.Grey.Medium);
                    });
                });
            }).GeneratePdf();

            return File(pdfBytes, "application/pdf", $"{maHoaDon}.pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi xuất PDF", detail = ex.Message });
        }
    }
}
