using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKhamBackend.Models;
using System.Text.RegularExpressions;

namespace PhongKhamBackend.Controllers;

[ApiController]
[Authorize]   // Tất cả endpoint trong controller này đều yêu cầu đăng nhập
public class BenhLyICDController : ControllerBase
{
    private readonly QuanLyPhongKhamDbContext _context;

    public BenhLyICDController(QuanLyPhongKhamDbContext context)
    {
        _context = context;
    }


    // DTO

    public class ThemBenhLyRequest
    {
        public string MaICD   { get; set; } = string.Empty;
        public string TenBenh { get; set; } = string.Empty;
    }

    public class CapNhatBenhLyRequest
    {
        public string TenBenh { get; set; } = string.Empty;
    }


   
    // LẤY DANH SÁCH BỆNH LÝ ICD
    // GET api/BenhLyICD
    // Lấy danh sách danh mục bệnh lý ICD có hỗ trợ lọc theo mã/tên
    // và phân trang.
    // Phân quyền: Admin, BacSi, LeTan

    [HttpGet("api/BenhLyICD")]
    [Authorize(Roles = "Admin,BacSi,LeTan")]
    public async Task<IActionResult> LayDanhSachBenhLy(
        [FromQuery] string? maICD = null,
        [FromQuery] string? tenBenh = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            // Kiểm tra page và pageSize
            if (page <= 0 || pageSize <= 0)
            {
                return BadRequest(new { message = "Giá trị phân trang không hợp lệ" });
            }

            // Giới hạn tối đa 100 bản ghi mỗi trang
            if (pageSize > 100) pageSize = 100;

            // Xây dựng query
            var query = _context.DanhMucIcds.AsQueryable();

            // Lọc theo mã ICD (tìm kiếm gần đúng, không phân biệt hoa thường)
            if (!string.IsNullOrWhiteSpace(maICD))
            {
                string maICDTrim = maICD.Trim().ToLower();
                query = query.Where(icd => icd.MaIcd.ToLower().Contains(maICDTrim));
            }

            // Lọc theo tên bệnh (tìm kiếm gần đúng, không phân biệt hoa thường)
            if (!string.IsNullOrWhiteSpace(tenBenh))
            {
                string tenBenhTrim = tenBenh.Trim().ToLower();
                query = query.Where(icd => icd.TenBenh.ToLower().Contains(tenBenhTrim));
            }

            // Đếm tổng số bản ghi sau khi lọc
            int total = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)total / pageSize);

            // Phân trang và lấy dữ liệu
            var danhSach = await query
                .OrderBy(icd => icd.MaIcd)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(icd => new
                {
                    MaICD   = icd.MaIcd,
                    TenBenh = icd.TenBenh
                })
                .ToListAsync();

            // Thêm số thứ tự (tính theo trang hiện tại)
            var data = danhSach.Select((item, index) => new
            {
                stt     = (page - 1) * pageSize + index + 1,
                item.MaICD,
                item.TenBenh
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


 
    // THÊM DANH MỤC BỆNH LÝ
    // POST api/BenhLyICD
    // Admin thêm mới một mã bệnh lý ICD vào danh mục hệ thống.
    // Phân quyền: Chỉ Admin

    [HttpPost("api/BenhLyICD")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ThemBenhLy([FromBody] ThemBenhLyRequest request)
    {
        try
        {
           
            // VALIDATE CÁC TRƯỜNG BẮT BUỘC
            // Không nhập mã bệnh lý ICD
            if (string.IsNullOrWhiteSpace(request.MaICD))
                return BadRequest(new { message = "Vui lòng nhập mã bệnh lý (ICD)" });

            // Không nhập tên bệnh lý
            if (string.IsNullOrWhiteSpace(request.TenBenh))
                return BadRequest(new { message = "Vui lòng nhập tên phân loại chẩn đoán bệnh" });

            // VALIDATE ĐỊNH DẠNG & RÀNG BUỘC
            // Mã bệnh lý ICD chứa khoảng trắng
            if (request.MaICD.Contains(' '))
                return BadRequest(new { message = "Mã bệnh lý ICD không được chứa khoảng trắng. Vui lòng nhập lại" });

            // Mã bệnh lý ICD vượt quá 10 ký tự
            if (request.MaICD.Trim().Length > 10)
                return BadRequest(new { message = "Mã bệnh lý ICD không được vượt quá 10 ký tự. Vui lòng nhập lại" });

            // Mã bệnh lý ICD chứa ký tự đặc biệt (chỉ cho phép chữ cái, chữ số và dấu chấm)
            if (!Regex.IsMatch(request.MaICD.Trim(), @"^[A-Za-z0-9.]+$"))
                return BadRequest(new { message = "Mã bệnh lý ICD không được chứa ký tự đặc biệt. Vui lòng nhập lại" });

            // Tên bệnh lý vượt quá 100 ký tự
            if (request.TenBenh.Trim().Length > 100)
                return BadRequest(new { message = "Tên bệnh lý không được vượt quá 100 ký tự. Vui lòng nhập lại" });

            // Server tự động chuyển mã ICD về chữ HOA trước khi lưu
            string maICDUpper = request.MaICD.Trim().ToUpper();

            // Mã bệnh lý ICD đã tồn tại trong hệ thống
            bool maICDExists = await _context.DanhMucIcds.AnyAsync(icd => icd.MaIcd == maICDUpper);
            if (maICDExists)
                return Conflict(new { message = "Mã bệnh lý ICD đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại mã khác" });

            // Tên bệnh lý đã tồn tại trong hệ thống
            string tenBenhTrim = request.TenBenh.Trim();
            bool tenBenhExists = await _context.DanhMucIcds
                .AnyAsync(icd => icd.TenBenh.ToLower() == tenBenhTrim.ToLower());
            if (tenBenhExists)
                return Conflict(new { message = "Tên bệnh lý đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại tên khác" });

           
            // TẠO BẢN GHI MỚI TRONG DATABASE
            var newICD = new DanhMucIcd
            {
                MaIcd   = maICDUpper,
                TenBenh = tenBenhTrim
            };

            _context.DanhMucIcds.Add(newICD);
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                message = "Thêm bệnh lý thành công",
                data = new
                {
                    maICD   = newICD.MaIcd,
                    tenBenh = newICD.TenBenh
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


 
    // SỬA DANH MỤC BỆNH LÝ
    // PUT api/BenhLyICD/{maICD}
    // Admin cập nhật tên bệnh lý của một mã ICD đã tồn tại trong hệ thống.
    // Lưu ý: Không cho phép thay đổi maICD vì đây là khóa chính (PRIMARY KEY)
    // và được tham chiếu bởi bảng PhieuKham.
    // Phân quyền: Chỉ Admin

    [HttpPut("api/BenhLyICD/{maICD}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CapNhatBenhLy(string maICD, [FromBody] CapNhatBenhLyRequest request)
    {
        try
        {
           
            // KIỂM TRA BỆNH LÝ TỒN TẠI
            // maICD trên URL không tồn tại trong hệ thống
            var benhLy = await _context.DanhMucIcds.FindAsync(maICD);

            if (benhLy == null)
                return NotFound(new { message = "Không tìm thấy bệnh lý cần cập nhật" });

            // VALIDATE CÁC TRƯỜNG BẮT BUỘC
            // Không nhập tên bệnh lý
            if (string.IsNullOrWhiteSpace(request.TenBenh))
                return BadRequest(new { message = "Vui lòng nhập tên phân loại chẩn đoán bệnh" });

            // VALIDATE RÀNG BUỘC
            string tenBenhTrim = request.TenBenh.Trim();

            // Tên bệnh lý vượt quá 100 ký tự
            if (tenBenhTrim.Length > 100)
                return BadRequest(new { message = "Tên bệnh lý không được vượt quá 100 ký tự. Vui lòng nhập lại" });

            // Tên bệnh lý đã tồn tại trong hệ thống (ngoại trừ chính bản ghi đang cập nhật)
            bool tenBenhDuplicate = await _context.DanhMucIcds
                .AnyAsync(icd => icd.TenBenh.ToLower() == tenBenhTrim.ToLower() && icd.MaIcd != maICD);
            if (tenBenhDuplicate)
                return Conflict(new { message = "Tên bệnh lý đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại tên khác" });

            // CẬP NHẬT DỮ LIỆU
            benhLy.TenBenh = tenBenhTrim;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật thông tin bệnh lý thành công",
                data = new
                {
                    maICD   = benhLy.MaIcd,
                    tenBenh = benhLy.TenBenh
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }


 
    // XÓA DANH MỤC BỆNH LÝ
    // DELETE api/BenhLyICD/{maICD}
    // Admin xóa một mã bệnh lý ICD khỏi danh mục hệ thống.
    // Không cho phép xóa nếu mã ICD đang được tham chiếu bởi bảng PhieuKham
    // (tức là đã có phiếu khám ghi nhận chẩn đoán theo mã này).
    // Phân quyền: Chỉ Admin

    [HttpDelete("api/BenhLyICD/{maICD}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> XoaBenhLy(string maICD)
    {
        try
        {
            // KIỂM TRA BỆNH LÝ TỒN TẠI

            // maICD trên URL không tồn tại trong hệ thống
            var benhLy = await _context.DanhMucIcds.FindAsync(maICD);

            if (benhLy == null)
                return NotFound(new { message = "Không tìm thấy bệnh lý cần xóa" });

            // KIỂM TRA RÀNG BUỘC KHÓA NGOẠI

            // Mã ICD đang được sử dụng trong phiếu khám
            bool hasPhieuKham = await _context.PhieuKhams
                .AnyAsync(pk => pk.MaIcds.Any(icd => icd.MaIcd == maICD));

            if (hasPhieuKham)
            {
                return Conflict(new
                {
                    message = "Không thể xóa bệnh lý này vì đã có phiếu khám sử dụng mã chẩn đoán này"
                });
            }

            // XÓA BẢN GHI
            _context.DanhMucIcds.Remove(benhLy);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Xóa bệnh lý thành công",
                data = new
                {
                    maICD   = benhLy.MaIcd,
                    tenBenh = benhLy.TenBenh
                }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể xóa dữ liệu từ máy chủ. Xin hãy thử lại" });
        }
    }
}
