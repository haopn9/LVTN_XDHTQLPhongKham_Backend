using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using System.Security.Claims;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Route("api/LichLamViec")]
[Authorize]
public class LichLamViecController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;

    public LichLamViecController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DTO
    // ──────────────────────────────────────────────────────────────────────────
    public class DangKyCaTrucRequest
    {
        public string NgayLamViec { get; set; } = string.Empty;  // yyyy-MM-dd
        public string CaLamViec   { get; set; } = string.Empty;  // "Sang" | "Chieu"
        public string? PhongKham  { get; set; }
        public string? GhiChu     { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST api/LichLamViec — Bác sĩ tự đăng ký ca trực
    // Phân quyền: BacSi
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "BacSi")]
    public async Task<IActionResult> DangKyCaTruc([FromBody] DangKyCaTrucRequest request)
    {
        // ── 1. Lấy maNV từ userID trong token ──────────────────────────────
        string? userIdClaim = User.FindFirstValue("userID");
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });

        var bacSi = await _context.NhanViens
            .AsNoTracking()
            .FirstOrDefaultAsync(nv => nv.UserId == userId);

        if (bacSi == null)
            return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" });

        // ── 2. Validate ngayLamViec ────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.NgayLamViec))
            return BadRequest(new { message = "Vui lòng nhập ngày làm việc!" });

        if (!DateOnly.TryParse(request.NgayLamViec, out DateOnly ngayLamViec))
            return BadRequest(new { message = "Ngày làm việc không hợp lệ. Định dạng: yyyy-MM-dd" });

        if (ngayLamViec < DateOnly.FromDateTime(DateTime.Now))
            return BadRequest(new { message = "Không thể đăng ký ca trực cho ngày trong quá khứ!" });

        // ── 3. Validate caLamViec ──────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.CaLamViec))
            return BadRequest(new { message = "Vui lòng chọn ca làm việc!" });

        string caLamViec = request.CaLamViec.Trim();
        if (caLamViec != "Sang" && caLamViec != "Chieu")
            return BadRequest(new { message = "Ca làm việc không hợp lệ. Chỉ chấp nhận: Sang, Chieu" });

        // ── 4. Kiểm tra MaKhoa của bác sĩ ─────────────────────────────────
        if (string.IsNullOrEmpty(bacSi.MaKhoa))
            return BadRequest(new { message = "Bác sĩ chưa được gán khoa. Vui lòng liên hệ Admin để được phân khoa trước khi đăng ký ca trực!" });

        string maNV    = bacSi.MaNv;
        string maKhoa  = bacSi.MaKhoa;

        // ── 5. Tính khoảng tuần dương lịch (Thứ 2 → Chủ Nhật) ────────────
        //      Dùng để check giới hạn 3 ca/tuần
        int     dow        = (int)ngayLamViec.DayOfWeek;          // 0=CN, 1=T2, ...6=T7
        int     offsetMon  = (dow == 0) ? -6 : (1 - dow);         // số ngày lùi về T2
        DateOnly dauTuan   = ngayLamViec.AddDays(offsetMon);
        DateOnly cuoiTuan  = dauTuan.AddDays(6);

        // ── 6. Transaction với Serializable để tránh race condition ────────
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);
            try
            {
                // Check trùng ca của chính bác sĩ
                bool trungCa = await _context.LichLamViecs
                    .AnyAsync(l => l.MaNv == maNV
                               && l.NgayLamViec == ngayLamViec
                               && l.CaLamViec   == caLamViec);

                if (trungCa)
                {
                    await transaction.RollbackAsync();
                    return Conflict(new { message = $"Bạn đã đăng ký ca {caLamViec} ngày {ngayLamViec:dd/MM/yyyy} rồi!" });
                }

                // Check giới hạn 3 ca/tuần
                int soCaTuanNay = await _context.LichLamViecs
                    .CountAsync(l => l.MaNv == maNV
                                  && l.NgayLamViec >= dauTuan
                                  && l.NgayLamViec <= cuoiTuan);

                if (soCaTuanNay >= 3)
                {
                    await transaction.RollbackAsync();
                    return Conflict(new
                    {
                        message = $"Bạn đã đăng ký đủ 3 ca trong tuần ({dauTuan:dd/MM} – {cuoiTuan:dd/MM/yyyy}). Không thể đăng ký thêm!"
                    });
                }

                // Check 1 khoa tối đa 1 bác sĩ/ca/ngày
                var bacSiCungKhoa = await _context.LichLamViecs
                    .Include(l => l.MaNvNavigation)
                    .Where(l => l.MaNv       != maNV
                             && l.NgayLamViec == ngayLamViec
                             && l.CaLamViec   == caLamViec
                             && l.MaNvNavigation.MaKhoa == maKhoa)
                    .Select(l => new { l.MaNvNavigation.HoTen })
                    .FirstOrDefaultAsync();

                if (bacSiCungKhoa != null)
                {
                    await transaction.RollbackAsync();
                    return Conflict(new
                    {
                        message = $"Ca {caLamViec} ngày {ngayLamViec:dd/MM/yyyy} trong khoa của bạn đã có {bacSiCungKhoa.HoTen} đăng ký trước. Mỗi khoa chỉ có 1 bác sĩ trực/ca/ngày!"
                    });
                }

                // Insert bản ghi mới
                var newLich = new LichLamViec
                {
                    MaNv        = maNV,
                    NgayLamViec = ngayLamViec,
                    CaLamViec   = caLamViec,
                    PhongKham   = string.IsNullOrWhiteSpace(request.PhongKham) ? null : request.PhongKham.Trim(),
                    GhiChu      = string.IsNullOrWhiteSpace(request.GhiChu)    ? null : request.GhiChu.Trim(),
                    NgayDangKy  = DateTime.Now
                };

                _context.LichLamViecs.Add(newLich);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return StatusCode(201, new
                {
                    maLich      = newLich.MaLich,
                    maNV        = newLich.MaNv,
                    ngayLamViec = newLich.NgayLamViec.ToString("yyyy-MM-dd"),
                    caLamViec   = newLich.CaLamViec,
                    phongKham   = newLich.PhongKham,
                    ghiChu      = newLich.GhiChu,
                    ngayDangKy  = newLich.NgayDangKy.ToString("o")
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET api/LichLamViec — Xem lịch trực (nội bộ)
    // Phân quyền: Mọi role đã đăng nhập
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> XemLichTruc(
        [FromQuery] string? tuNgay  = null,
        [FromQuery] string? denNgay = null,
        [FromQuery] string? maKhoa  = null,
        [FromQuery] string? maNV    = null)
    {
        try
        {
            // Mặc định: đầu tuần → cuối tuần hiện tại
            DateOnly ngayBatDau, ngayKetThuc;

            if (!string.IsNullOrWhiteSpace(tuNgay))
            {
                if (!DateOnly.TryParse(tuNgay, out ngayBatDau))
                    return BadRequest(new { message = "tuNgay không hợp lệ. Định dạng: yyyy-MM-dd" });
            }
            else
            {
                var today   = DateOnly.FromDateTime(DateTime.Now);
                int dow     = (int)today.DayOfWeek;
                int offset  = (dow == 0) ? -6 : (1 - dow);
                ngayBatDau  = today.AddDays(offset);
            }

            if (!string.IsNullOrWhiteSpace(denNgay))
            {
                if (!DateOnly.TryParse(denNgay, out ngayKetThuc))
                    return BadRequest(new { message = "denNgay không hợp lệ. Định dạng: yyyy-MM-dd" });
            }
            else
            {
                ngayKetThuc = ngayBatDau.AddDays(6);
            }

            if (ngayBatDau > ngayKetThuc)
                return BadRequest(new { message = "tuNgay không được lớn hơn denNgay!" });

            // Query LichLamViec JOIN NhanVien JOIN DanhMucKhoa
            var query = _context.LichLamViecs
                .AsNoTracking()
                .Include(l => l.MaNvNavigation)
                    .ThenInclude(nv => nv.MaKhoaNavigation)
                .Where(l => l.NgayLamViec >= ngayBatDau && l.NgayLamViec <= ngayKetThuc)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(maKhoa))
                query = query.Where(l => l.MaNvNavigation.MaKhoa == maKhoa.Trim());

            if (!string.IsNullOrWhiteSpace(maNV))
                query = query.Where(l => l.MaNv == maNV.Trim());

            var result = await query
                .OrderBy(l => l.NgayLamViec)
                .ThenBy(l => l.CaLamViec)
                .Select(l => new
                {
                    maLich      = l.MaLich,
                    maNV        = l.MaNv,
                    tenBacSi    = l.MaNvNavigation.HoTen,
                    maKhoa      = l.MaNvNavigation.MaKhoa,
                    tenKhoa     = l.MaNvNavigation.MaKhoaNavigation != null
                                    ? l.MaNvNavigation.MaKhoaNavigation.TenKhoa
                                    : null,
                    ngayLamViec = l.NgayLamViec.ToString("yyyy-MM-dd"),
                    caLamViec   = l.CaLamViec,
                    phongKham   = l.PhongKham,
                    ghiChu      = l.GhiChu
                })
                .ToListAsync();

            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DELETE api/LichLamViec/{maLich} — Admin xóa ca trực
    // Phân quyền: Admin
    // ──────────────────────────────────────────────────────────────────────────
    [HttpDelete("{maLich}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> XoaCaTruc(int maLich)
    {
        try
        {
            var lich = await _context.LichLamViecs
                .FirstOrDefaultAsync(l => l.MaLich == maLich);

            if (lich == null)
                return NotFound(new { message = $"Không tìm thấy lịch trực với mã {maLich}!" });

            // Chỉ xóa ca chưa diễn ra (giữ lại lịch sử)
            if (lich.NgayLamViec < DateOnly.FromDateTime(DateTime.Now))
                return BadRequest(new
                {
                    message = "Không thể xóa ca trực đã diễn ra. Dữ liệu lịch sử được giữ lại để tra cứu."
                });

            _context.LichLamViecs.Remove(lich);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }
}
