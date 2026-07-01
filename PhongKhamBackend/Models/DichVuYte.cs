using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class DichVuYte
{
    public int MaChiTiet { get; set; }

    public string? MaPhieu { get; set; }

    public string? MaDv { get; set; }

    public string? KetQua { get; set; }

    public int? TrangThaiDichVu { get; set; }

    public virtual ChiTietDichVuYte? MaDvNavigation { get; set; }

    public virtual PhieuKham? MaPhieuNavigation { get; set; }
}
