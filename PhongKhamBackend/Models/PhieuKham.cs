using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class PhieuKham
{
    public string MaPhieu { get; set; } = null!;

    public string? MaBn { get; set; }

    public string? MaNv { get; set; }

    public DateTime? NgayKham { get; set; }

    public int? Mach { get; set; }

    public double? NhietDo { get; set; }

    public string? HuyetAp { get; set; }

    public double? CanNang { get; set; }

    public double? ChieuCao { get; set; }

    public string? KetLuan { get; set; }

    public string? LyDoKham { get; set; }

    public int? TrangThaiKham { get; set; }

    public virtual ICollection<ChiTietVatTuPhieuKham> ChiTietVatTuPhieuKhams { get; set; } = new List<ChiTietVatTuPhieuKham>();

    public virtual ICollection<DichVuYte> DichVuYtes { get; set; } = new List<DichVuYte>();

    public virtual ICollection<DonThuoc> DonThuocs { get; set; } = new List<DonThuoc>();

    public virtual ICollection<HoaDon> HoaDons { get; set; } = new List<HoaDon>();

    public virtual BenhNhan? MaBnNavigation { get; set; }

    public virtual NhanVien? MaNvNavigation { get; set; }

    public virtual ICollection<DanhMucIcd> MaIcds { get; set; } = new List<DanhMucIcd>();
}
