using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class LoVatTu
{
    public string MaLo { get; set; } = null!;

    public string MaVatTu { get; set; } = null!;

    public int MaNcc { get; set; }

    public int SoLuongNhap { get; set; }

    public int SoLuongTon { get; set; }

    public decimal GiaNhap { get; set; }

    public decimal GiaBan { get; set; }

    public DateOnly? NgaySanXuat { get; set; }

    public DateOnly HanSuDung { get; set; }

    public virtual ICollection<ChiTietVatTuLo> ChiTietVatTuLos { get; set; } = new List<ChiTietVatTuLo>();

    public virtual NhaCungCap MaNccNavigation { get; set; } = null!;

    public virtual DanhMucVatTu MaVatTuNavigation { get; set; } = null!;
}
