using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class BenhNhan
{
    public string MaBn { get; set; } = null!;

    public string HoTen { get; set; } = null!;

    public DateOnly? NgaySinh { get; set; }

    public string? GioiTinh { get; set; }

    public string? Sdt { get; set; }

    public string? DiaChi { get; set; }

    public string? TienSuBenh { get; set; }

    public virtual ICollection<PhieuKham> PhieuKhams { get; set; } = new List<PhieuKham>();
}
