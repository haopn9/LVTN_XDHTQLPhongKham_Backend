using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class DonThuoc
{
    public string MaDonThuoc { get; set; } = null!;

    public string? MaPhieu { get; set; }

    public DateTime? NgayKeDon { get; set; }

    public string? LoiDan { get; set; }

    public virtual ICollection<ChiTietDonThuoc> ChiTietDonThuocs { get; set; } = new List<ChiTietDonThuoc>();

    public virtual PhieuKham? MaPhieuNavigation { get; set; }
}
