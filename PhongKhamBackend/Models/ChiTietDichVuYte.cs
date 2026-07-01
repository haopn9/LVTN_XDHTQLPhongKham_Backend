using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class ChiTietDichVuYte
{
    public string MaDv { get; set; } = null!;

    public int? MaLoaiDv { get; set; }

    public string TenDv { get; set; } = null!;

    public decimal GiaTien { get; set; }

    public bool? TrangThai { get; set; }

    public virtual ICollection<DichVuYte> DichVuYtes { get; set; } = new List<DichVuYte>();

    public virtual LoaiDichVu? MaLoaiDvNavigation { get; set; }
}
