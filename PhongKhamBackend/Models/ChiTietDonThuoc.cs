using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class ChiTietDonThuoc
{
    public string MaDonThuoc { get; set; } = null!;

    public string MaThuoc { get; set; } = null!;

    public int? SoLuong { get; set; }

    public string? CachDung { get; set; }

    public bool? TrangThaiPhatThuoc { get; set; }

    public virtual DonThuoc MaDonThuocNavigation { get; set; } = null!;

    public virtual DanhMucThuoc MaThuocNavigation { get; set; } = null!;
}
