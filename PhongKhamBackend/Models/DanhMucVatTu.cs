using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class DanhMucVatTu
{
    public string MaVatTu { get; set; } = null!;

    public string TenVatTu { get; set; } = null!;

    public string? QuyCach { get; set; }

    public string DonViTinh { get; set; } = null!;

    public bool IsActive { get; set; }
}
