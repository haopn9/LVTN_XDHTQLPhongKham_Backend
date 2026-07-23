using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class ChiTietVatTuLo
{
    public string MaPhieu { get; set; } = null!;

    public string MaVatTu { get; set; } = null!;

    public string MaLo { get; set; } = null!;

    public int SoLuongTru { get; set; }

    public virtual ChiTietVatTuPhieuKham ChiTietVatTuPhieuKham { get; set; } = null!;

    public virtual LoVatTu MaLoNavigation { get; set; } = null!;
}
