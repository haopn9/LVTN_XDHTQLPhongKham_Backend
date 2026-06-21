using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class DanhMucKhoa
{
    public string MaKhoa { get; set; } = null!;

    public string TenKhoa { get; set; } = null!;

    public virtual ICollection<NhanVien> NhanViens { get; set; } = new List<NhanVien>();
}
