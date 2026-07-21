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

    // ───────────────────────────────────────────────────────────────────────
    // GET api/DanhSach/vat-tu
    //   — Lấy danh sách vật tư CÒN TỒN KHO (phục vụ chọn khi kê/thêm vật tư)
    // Phân quyền: BacSi, ThuNgan, Admin, QuanLyKhoThuoc
    //
    // Lấy từ LoVatTu (không phải DanhMucVatTu) vì cần biết tổng tồn thực tế
    // trước khi cho chọn — tránh việc chọn xong mới báo lỗi hết hàng.
    // Không trả giá bán ở đây vì giá được tính FEFO tại thời điểm kê (server-side),
    // tránh lệch giá hiển thị vs giá thực tính lúc submit.
    // ───────────────────────────────────────────────────────────────────────
    [HttpGet("vat-tu")]
    [Authorize(Roles = "BacSi,ThuNgan,Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> LayDanhSachVatTuChon()
    {
        try
        {
            // Chỉ tính tồn kho từ các lô CÒN HẠN SỬ DỤNG — khớp với logic kê vật tư
            DateOnly today = DateOnly.FromDateTime(DateTime.Today);

            var danhSach = await _context.LoVatTus
                .AsNoTracking()
                .Where(lo => lo.MaVatTuNavigation != null && lo.MaVatTuNavigation.IsActive)
                .GroupBy(lo => new
                {
                    lo.MaVatTu,
                    TenVatTu  = lo.MaVatTuNavigation!.TenVatTu,
                    DonViTinh = lo.MaVatTuNavigation.DonViTinh
                })
                .Select(g => new
                {
                    maVatTu   = g.Key.MaVatTu,
                    tenVatTu  = g.Key.TenVatTu,
                    donViTinh = g.Key.DonViTinh,
                    // Chỉ cộng lô còn hạn sử dụng (tránh tính lô hết hạn vào tonKho)
                    tonKho    = g.Where(lo => lo.HanSuDung >= today)
                                 .Sum(lo => (int?)lo.SoLuongTon) ?? 0
                })
                .Where(vt => vt.tonKho > 0)   // chỉ hiện vật tư thực sự còn hàng khả dụng
                .OrderBy(vt => vt.tenVatTu)
                .ToListAsync();

            return Ok(new { data = danhSach });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }
}
