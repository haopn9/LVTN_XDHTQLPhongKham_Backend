using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class LoaiDichVu
{
    public int MaLoaiDv { get; set; }

    public string TenLoai { get; set; } = null!;

    public virtual ICollection<ChiTietDichVuYte> ChiTietDichVuYtes { get; set; } = new List<ChiTietDichVuYte>();
}
