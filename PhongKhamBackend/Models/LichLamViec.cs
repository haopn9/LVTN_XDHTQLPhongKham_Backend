using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class LichLamViec
{
    public int MaLich { get; set; }

    public string MaNv { get; set; } = null!;

    public DateOnly NgayLamViec { get; set; }

    public string CaLamViec { get; set; } = null!;

    public string? PhongKham { get; set; }

    public string? GhiChu { get; set; }

    public DateTime NgayDangKy { get; set; }

    public virtual NhanVien MaNvNavigation { get; set; } = null!;
}
