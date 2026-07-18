using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class DatLichKham
{
    public int MaDatLich { get; set; }

    public string HoTenKhach { get; set; } = null!;

    public string Sdt { get; set; } = null!;

    public DateOnly? NgayHen { get; set; }

    public string? YeuCauKham { get; set; }

    public string? TrangThai { get; set; }

    public string? MaNv { get; set; }

    public string? CaHen { get; set; }

    public virtual NhanVien? MaNvNavigation { get; set; }
}
