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
}
