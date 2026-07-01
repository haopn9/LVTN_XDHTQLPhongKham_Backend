using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class ChiTietVatTuPhieuKham
{
    public string MaPhieu { get; set; } = null!;

    public string MaVatTu { get; set; } = null!;

    public int SoLuong { get; set; }

    public decimal DonGia { get; set; }

    public virtual PhieuKham MaPhieuNavigation { get; set; } = null!;

    public virtual DanhMucVatTu MaVatTuNavigation { get; set; } = null!;
}
