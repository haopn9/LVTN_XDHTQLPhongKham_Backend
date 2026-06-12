using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class NhanVien
{
    public string MaNv { get; set; } = null!;

    public int? UserId { get; set; }

    public string HoTen { get; set; } = null!;

    public string? ChuyenMon { get; set; }

    public string? Sdt { get; set; }

    public string? Email { get; set; }

    public virtual ICollection<HoaDon> HoaDons { get; set; } = new List<HoaDon>();

    public virtual ICollection<PhieuKham> PhieuKhamMaBacSiNavigations { get; set; } = new List<PhieuKham>();

    public virtual ICollection<PhieuKham> PhieuKhamMaNvTiepDonNavigations { get; set; } = new List<PhieuKham>();

    public virtual User? User { get; set; }
}
