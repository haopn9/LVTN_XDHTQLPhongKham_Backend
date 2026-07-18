using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Route("api/DanhSach")]
[Authorize]
public class DanhSachController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;

    public DanhSachController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }


    // ───────────────────────────────────────────────────────────────────────
    // GET api/DanhSach/bac-si
    //   — Lấy danh sách bác sĩ (phục vụ chọn/lọc bác sĩ)
    // Phân quyền: LeTan, BacSi, Admin
    //
    // Dùng để:
    //   - Lễ tân chọn "Bác sĩ chỉ định" khi tạo phiếu tiếp đón
    //   - Admin lọc theo bác sĩ trong danh sách chờ khám
    //   - Chỉ trả thông tin tối thiểu (maNV, hoTen, maKhoa, chuyenMon)
    //     không trả các trường quản trị khác của NhanVien
    // ───────────────────────────────────────────────────────────────────────
    [HttpGet("bac-si")]
    [Authorize(Roles = "LeTan,BacSi,Admin")]
    public async Task<IActionResult> LayDanhSachBacSi()
    {
        try
        {
            // SELECT NhanVien JOIN Users
            //     WHERE Users.RoleID = 2 (BacSi) AND Users.IsActive = 1
            //     ORDER BY HoTen ASC
            var danhSachBacSi = await _context.NhanViens
                .AsNoTracking()
                .Include(nv => nv.User)
                .Where(nv => nv.User != null
                             && nv.User.RoleId == 2
                             && nv.User.IsActive == true)
                .OrderBy(nv => nv.HoTen)
                .Select(nv => new
                {
                    maNV       = nv.MaNv,
                    hoTen      = nv.HoTen,
                    maKhoa     = nv.MaKhoa,       // có thể null
                    chuyenMon  = nv.ChuyenMon      // có thể null
                })
                .ToListAsync();

            // Trả HTTP 200 — danh sách có thể rỗng [] (không coi là lỗi)
            return Ok(new { data = danhSachBacSi });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    // GET api/DanhSach/bac-si-lich-trong
    //   — Tra cứu bác sĩ có ca trực theo ngày + khoa (Bước 1 đặt lịch)
    // Phân quyền: AllowAnonymous (public — phục vụ cổng đặt lịch của khách)
    //
    // Query params bắt buộc: ngayHen (yyyy-MM-dd), maKhoa
    // Query params tuỳ chọn: caHen ("Sang" | "Chieu") — nếu không truyền → trả cả 2 ca
    // ───────────────────────────────────────────────────────────────────────
    [HttpGet("bac-si-lich-trong")]
    [AllowAnonymous]
    public async Task<IActionResult> LayBacSiLichTrong(
        [FromQuery] string? ngayHen = null,
        [FromQuery] string? maKhoa  = null,
        [FromQuery] string? caHen   = null)
    {
        try
        {
            // Validate ngayHen — bắt buộc
            if (string.IsNullOrWhiteSpace(ngayHen))
                return BadRequest(new { message = "Vui lòng cung cấp ngày hẹn (ngayHen)!" });

            if (!DateOnly.TryParse(ngayHen.Trim(), out DateOnly ngay))
                return BadRequest(new { message = "ngayHen không hợp lệ. Định dạng: yyyy-MM-dd" });

            if (ngay < DateOnly.FromDateTime(DateTime.Now))
                return BadRequest(new { message = "Ngày hẹn không thể là ngày trong quá khứ!" });

            // Validate maKhoa — bắt buộc
            if (string.IsNullOrWhiteSpace(maKhoa))
                return BadRequest(new { message = "Vui lòng cung cấp mã khoa (maKhoa)!" });

            // Validate caHen — tuỳ chọn
            if (!string.IsNullOrWhiteSpace(caHen)
                && caHen.Trim() != "Sang"
                && caHen.Trim() != "Chieu")
            {
                return BadRequest(new { message = "caHen không hợp lệ. Chỉ chấp nhận: Sang, Chieu" });
            }

            // Query LichLamViec JOIN NhanVien theo maKhoa + ngayHen (+ caHen nếu có)
            var query = _context.LichLamViecs
                .AsNoTracking()
                .Include(l => l.MaNvNavigation)
                .Where(l => l.NgayLamViec == ngay
                         && l.MaNvNavigation.MaKhoa == maKhoa.Trim())
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(caHen))
                query = query.Where(l => l.CaLamViec == caHen.Trim());

            var result = await query
                .Select(l => new
                {
                    maNV       = l.MaNv,
                    hoTen      = l.MaNvNavigation.HoTen,
                    chuyenMon  = l.MaNvNavigation.ChuyenMon,
                    caLamViec  = l.CaLamViec,
                    phongKham  = l.PhongKham
                })
                .ToListAsync();

            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }
}
