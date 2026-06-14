using System;
using System.Collections.Generic;

namespace PhongKhamBackend.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public int? RoleId { get; set; }

    public bool? IsActive { get; set; }

    public virtual NhanVien? NhanVien { get; set; }

    public virtual Role? Role { get; set; }
}
