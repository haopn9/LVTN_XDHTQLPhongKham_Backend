using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class LoaiDichVu
{
    public int MaLoaiDv { get; set; }

    public string TenLoai { get; set; } = null!;

    public virtual ICollection<DichVuYte> DichVuYtes { get; set; } = new List<DichVuYte>();
}
