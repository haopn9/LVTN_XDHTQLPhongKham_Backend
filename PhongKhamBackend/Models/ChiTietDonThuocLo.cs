using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class ChiTietDonThuocLo
{
    public string MaDonThuoc { get; set; } = null!;

    public string MaThuoc { get; set; } = null!;

    public string MaLo { get; set; } = null!;

    public int SoLuongTru { get; set; }

    public virtual ChiTietDonThuoc ChiTietDonThuoc { get; set; } = null!;

    public virtual LoThuoc MaLoNavigation { get; set; } = null!;
}
