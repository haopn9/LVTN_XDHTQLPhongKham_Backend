using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class NhaCungCap
{
    public int MaNcc { get; set; }

    public string TenNcc { get; set; } = null!;

    public string? Sdt { get; set; }

    public string? DiaChi { get; set; }

    public virtual ICollection<LoThuoc> LoThuocs { get; set; } = new List<LoThuoc>();

    public virtual ICollection<LoVatTu> LoVatTus { get; set; } = new List<LoVatTu>();
}
