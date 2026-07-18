using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class NhanVien
{
    public string MaNv { get; set; } = null!;

    public int? UserId { get; set; }

    public string HoTen { get; set; } = null!;

    public string? ChuyenMon { get; set; }

    public string? MaKhoa { get; set; }

    public string? Sdt { get; set; }

    public string? Email { get; set; }

    public virtual ICollection<DatLichKham> DatLichKhams { get; set; } = new List<DatLichKham>();

    public virtual ICollection<HoaDon> HoaDons { get; set; } = new List<HoaDon>();

    public virtual ICollection<LichLamViec> LichLamViecs { get; set; } = new List<LichLamViec>();

    public virtual DanhMucKhoa? MaKhoaNavigation { get; set; }

    public virtual ICollection<PhieuKham> PhieuKhams { get; set; } = new List<PhieuKham>();

    public virtual User? User { get; set; }
}
