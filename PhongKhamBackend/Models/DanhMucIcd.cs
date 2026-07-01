using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class DanhMucIcd
{
    public string MaIcd { get; set; } = null!;

    public string TenBenh { get; set; } = null!;

    public virtual ICollection<PhieuKham> MaPhieus { get; set; } = new List<PhieuKham>();
}
