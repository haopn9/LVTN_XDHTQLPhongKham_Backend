using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class HoaDon
{
    public string MaHoaDon { get; set; } = null!;

    public string? MaPhieu { get; set; }

    public string? MaNv { get; set; }

    public DateTime? NgayThanhToan { get; set; }

    public decimal? TongTienDichVu { get; set; }

    public decimal? TongTienThuoc { get; set; }

    public decimal ThanhTien { get; set; }

    public bool? TrangThaiThanhToan { get; set; }

    public virtual NhanVien? MaNvNavigation { get; set; }

    public virtual PhieuKham? MaPhieuNavigation { get; set; }
}
