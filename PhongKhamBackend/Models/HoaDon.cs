using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class HoaDon
{
    public string MaHoaDon { get; set; } = null!;

    public string? MaPhieu { get; set; }

    public string? MaNvThuNgan { get; set; }

    public DateTime? NgayThanhToan { get; set; }

    public decimal? TongTienDichVu { get; set; }

    public decimal? TongTienThuoc { get; set; }

    public decimal ThanhTien { get; set; }

    public bool? TrangThaiThanhToan { get; set; }

    public virtual NhanVien? MaNvThuNganNavigation { get; set; }

    public virtual PhieuKham? MaPhieuNavigation { get; set; }
}
