using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class LoThuoc
{
    public string MaLo { get; set; } = null!;

    public string? MaThuoc { get; set; }

    public int? MaNcc { get; set; }

    public int? SoLuongNhap { get; set; }

    public int? SoLuongTon { get; set; }

    public decimal? GiaNhap { get; set; }

    public decimal? GiaBan { get; set; }

    public DateOnly? NgaySanXuat { get; set; }

    public DateOnly? HanSuDung { get; set; }

    public virtual ICollection<ChiTietDonThuocLo> ChiTietDonThuocLos { get; set; } = new List<ChiTietDonThuocLo>();

    public virtual NhaCungCap? MaNccNavigation { get; set; }

    public virtual DanhMucThuoc? MaThuocNavigation { get; set; }
}
