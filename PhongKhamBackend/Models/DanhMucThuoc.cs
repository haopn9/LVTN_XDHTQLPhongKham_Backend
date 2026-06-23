using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class DanhMucThuoc
{
    public string MaThuoc { get; set; } = null!;

    public string TenThuoc { get; set; } = null!;

    public string? HoatChat { get; set; }

    public string? DonViTinh { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<ChiTietDonThuoc> ChiTietDonThuocs { get; set; } = new List<ChiTietDonThuoc>();

    public virtual ICollection<LoThuoc> LoThuocs { get; set; } = new List<LoThuoc>();
}
