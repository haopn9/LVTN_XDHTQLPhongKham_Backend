using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Authorize]
public class NhaCungCapController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;

    public NhaCungCapController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }

    public class NhaCungCapRequest
    {
        public string TenNCC { get; set; } = string.Empty;
        public string? SDT { get; set; }
        public string? DiaChi { get; set; }
    }

    // ================================================================
    // LAY DANH SACH NHA CUNG CAP
    // GET api/NhaCungCap
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpGet("api/NhaCungCap")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> LayDanhSachNhaCungCap(
        [FromQuery] string? tenNCC = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            if (page <= 0 || pageSize <= 0)
                return BadRequest(new { message = "Giá trị phân trang không hợp lệ" });

            if (pageSize > 100) pageSize = 100;

            var query = _context.NhaCungCaps.AsQueryable();

            if (!string.IsNullOrWhiteSpace(tenNCC))
            {
                string tenNCCTrim = tenNCC.Trim().ToLower();
                query = query.Where(ncc => ncc.TenNcc.ToLower().Contains(tenNCCTrim));
            }

            int total = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)total / pageSize);

            var danhSach = await query
                .OrderBy(ncc => ncc.MaNcc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ncc => new
                {
                    ncc.MaNcc,
                    ncc.TenNcc,
                    ncc.Sdt,
                    ncc.DiaChi
                })
                .ToListAsync();

            var data = danhSach.Select((item, index) => new
            {
                stt = (page - 1) * pageSize + index + 1,
                maNCC = item.MaNcc,
                tenNCC = item.TenNcc,
                sDT = item.Sdt,
                diaChi = item.DiaChi
            });

            return Ok(new
            {
                data,
                total,
                page,
                pageSize,
                totalPages
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ================================================================
    // THEM NHA CUNG CAP
    // POST api/NhaCungCap
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpPost("api/NhaCungCap")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> ThemNhaCungCap([FromBody] NhaCungCapRequest request)
    {
        try
        {
            var validationResult = ValidateNhaCungCapRequest(request);
            if (validationResult != null) return validationResult;

            string tenNCCTrim = request.TenNCC.Trim();
            string? sdtTrim = string.IsNullOrWhiteSpace(request.SDT) ? null : request.SDT.Trim();
            string? diaChiTrim = string.IsNullOrWhiteSpace(request.DiaChi) ? null : request.DiaChi.Trim();

            bool tenNCCExists = await _context.NhaCungCaps
                .AnyAsync(ncc => ncc.TenNcc.ToLower() == tenNCCTrim.ToLower());
            if (tenNCCExists)
                return Conflict(new { message = "Tên nhà cung cấp đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại tên khác" });

            var nhaCungCap = new NhaCungCap
            {
                TenNcc = tenNCCTrim,
                Sdt = sdtTrim,
                DiaChi = diaChiTrim
            };

            _context.NhaCungCaps.Add(nhaCungCap);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Thêm mới nhà cung cấp thành công",
                data = ToNhaCungCapResponse(nhaCungCap)
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ================================================================
    // SUA THONG TIN NHA CUNG CAP
    // PUT api/NhaCungCap/{maNCC}
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpPut("api/NhaCungCap/{maNCC:int}")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> CapNhatNhaCungCap(int maNCC, [FromBody] NhaCungCapRequest request)
    {
        try
        {
            var nhaCungCap = await _context.NhaCungCaps.FindAsync(maNCC);
            if (nhaCungCap == null)
                return NotFound(new { message = "Không tìm thấy nhà cung cấp cần cập nhật" });

            var validationResult = ValidateNhaCungCapRequest(request);
            if (validationResult != null) return validationResult;

            string tenNCCTrim = request.TenNCC.Trim();
            string? sdtTrim = string.IsNullOrWhiteSpace(request.SDT) ? null : request.SDT.Trim();
            string? diaChiTrim = string.IsNullOrWhiteSpace(request.DiaChi) ? null : request.DiaChi.Trim();

            bool tenNCCDuplicate = await _context.NhaCungCaps
                .AnyAsync(ncc => ncc.TenNcc.ToLower() == tenNCCTrim.ToLower() && ncc.MaNcc != maNCC);
            if (tenNCCDuplicate)
                return Conflict(new { message = "Tên nhà cung cấp đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại tên khác" });

            nhaCungCap.TenNcc = tenNCCTrim;
            nhaCungCap.Sdt = sdtTrim;
            nhaCungCap.DiaChi = diaChiTrim;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật thông tin nhà cung cấp thành công",
                data = ToNhaCungCapResponse(nhaCungCap)
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }

    // ================================================================
    // XOA NHA CUNG CAP (HARD DELETE)
    // DELETE api/NhaCungCap/{maNCC}
    // Phan quyen: Admin, QuanLyKhoThuoc
    // ================================================================

    [HttpDelete("api/NhaCungCap/{maNCC:int}")]
    [Authorize(Roles = "Admin,QuanLyKhoThuoc")]
    public async Task<IActionResult> XoaNhaCungCap(int maNCC)
    {
        try
        {
            var nhaCungCap = await _context.NhaCungCaps.FindAsync(maNCC);
            if (nhaCungCap == null)
                return NotFound(new { message = "Không tìm thấy nhà cung cấp cần xóa" });

            bool coLoThuocLienKet = await _context.LoThuocs.AnyAsync(lo => lo.MaNcc == maNCC);
            if (coLoThuocLienKet)
                return Conflict(new { message = "Không thể xóa nhà cung cấp này vì đang có lô thuốc liên kết. Vui lòng kiểm tra lại" });

            var data = ToNhaCungCapResponse(nhaCungCap);

            _context.NhaCungCaps.Remove(nhaCungCap);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Xóa nhà cung cấp thành công",
                data
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể thực hiện thao tác. Xin hãy thử lại" });
        }
    }

    private IActionResult? ValidateNhaCungCapRequest(NhaCungCapRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Người dùng bắt buộc nhập đủ các dữ liệu cần thiết" });

        if (string.IsNullOrWhiteSpace(request.TenNCC))
            return BadRequest(new { message = "Vui lòng nhập tên nhà cung cấp" });

        if (request.TenNCC.Trim().Length > 100)
            return BadRequest(new { message = "Tên nhà cung cấp không được vượt quá 100 ký tự. Vui lòng nhập lại" });

        if (!string.IsNullOrWhiteSpace(request.SDT))
        {
            string sdtTrim = request.SDT.Trim();

            if (sdtTrim.Length > 15)
                return BadRequest(new { message = "Số điện thoại nhà cung cấp không được vượt quá 15 ký tự. Vui lòng nhập lại" });

            if (!Regex.IsMatch(sdtTrim, @"^[0-9]+$"))
                return BadRequest(new { message = "Số điện thoại nhà cung cấp phải là kiểu số nguyên dương. Vui lòng nhập lại" });
        }

        if (!string.IsNullOrWhiteSpace(request.DiaChi) && request.DiaChi.Trim().Length > 255)
            return BadRequest(new { message = "Địa chỉ nhà cung cấp không được vượt quá 255 ký tự. Vui lòng nhập lại" });

        return null;
    }

    private static object ToNhaCungCapResponse(NhaCungCap nhaCungCap)
    {
        return new
        {
            maNCC = nhaCungCap.MaNcc,
            tenNCC = nhaCungCap.TenNcc,
            sDT = nhaCungCap.Sdt,
            diaChi = nhaCungCap.DiaChi
        };
    }
}
