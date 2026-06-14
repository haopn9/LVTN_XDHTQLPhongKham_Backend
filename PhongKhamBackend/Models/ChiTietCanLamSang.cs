using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class ChiTietCanLamSang
{
    public int MaChiTiet { get; set; }

    public string? MaPhieu { get; set; }

    public string? MaDv { get; set; }

    public string? KetQua { get; set; }

    public int? TrangThaiCls { get; set; }

    public virtual DichVuYte? MaDvNavigation { get; set; }

    public virtual PhieuKham? MaPhieuNavigation { get; set; }
}
