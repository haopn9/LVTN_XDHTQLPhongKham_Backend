using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace PhongKhamBackend.Models;

public partial class QuanLyPhongKhamDbContext : DbContext
{
    public QuanLyPhongKhamDbContext()
    {
    }

    public QuanLyPhongKhamDbContext(DbContextOptions<QuanLyPhongKhamDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BenhNhan> BenhNhans { get; set; }

    public virtual DbSet<ChiTietCanLamSang> ChiTietCanLamSangs { get; set; }

    public virtual DbSet<ChiTietDonThuoc> ChiTietDonThuocs { get; set; }

    public virtual DbSet<DanhMucIcd> DanhMucIcds { get; set; }

    public virtual DbSet<DanhMucThuoc> DanhMucThuocs { get; set; }

    public virtual DbSet<DatLichKham> DatLichKhams { get; set; }

    public virtual DbSet<DichVuYte> DichVuYtes { get; set; }

    public virtual DbSet<DonThuoc> DonThuocs { get; set; }

    public virtual DbSet<HoaDon> HoaDons { get; set; }

    public virtual DbSet<LoThuoc> LoThuocs { get; set; }

    public virtual DbSet<LoaiDichVu> LoaiDichVus { get; set; }

    public virtual DbSet<NhaCungCap> NhaCungCaps { get; set; }

    public virtual DbSet<NhanVien> NhanViens { get; set; }

    public virtual DbSet<PhieuKham> PhieuKhams { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Connection string được inject từ Program.cs qua AddDbContext
        // Fallback này chỉ dùng khi chạy EF CLI tools (scaffold, migrations...)
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=QuanLyPhongKham_DB;Trusted_Connection=True;TrustServerCertificate=True;");
        }
    }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BenhNhan>(entity =>
        {
            entity.HasKey(e => e.MaBn).HasName("PK__BenhNhan__272475ADCD202485");

            entity.ToTable("BenhNhan");

            entity.Property(e => e.MaBn)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaBN");
            entity.Property(e => e.DiaChi).HasMaxLength(255);
            entity.Property(e => e.GioiTinh).HasMaxLength(10);
            entity.Property(e => e.HoTen).HasMaxLength(100);
            entity.Property(e => e.Sdt)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasColumnName("SDT");
        });

        modelBuilder.Entity<ChiTietCanLamSang>(entity =>
        {
            entity.HasKey(e => e.MaChiTiet).HasName("PK__ChiTietC__CDF0A114FF0B9302");

            entity.ToTable("ChiTietCanLamSang");

            entity.Property(e => e.MaDv)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaDV");
            entity.Property(e => e.MaPhieu)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.TrangThaiCls)
                .HasDefaultValue(0)
                .HasColumnName("TrangThaiCLS");

            entity.HasOne(d => d.MaDvNavigation).WithMany(p => p.ChiTietCanLamSangs)
                .HasForeignKey(d => d.MaDv)
                .HasConstraintName("FK__ChiTietCan__MaDV__6B24EA82");

            entity.HasOne(d => d.MaPhieuNavigation).WithMany(p => p.ChiTietCanLamSangs)
                .HasForeignKey(d => d.MaPhieu)
                .HasConstraintName("FK__ChiTietCa__MaPhi__6A30C649");
        });

        modelBuilder.Entity<ChiTietDonThuoc>(entity =>
        {
            entity.HasKey(e => new { e.MaDonThuoc, e.MaThuoc }).HasName("PK__ChiTietD__2A42818379950964");

            entity.ToTable("ChiTietDonThuoc");

            entity.Property(e => e.MaDonThuoc)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MaThuoc)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CachDung).HasMaxLength(255);
            entity.Property(e => e.TrangThaiPhatThuoc).HasDefaultValue(false);

            entity.HasOne(d => d.MaDonThuocNavigation).WithMany(p => p.ChiTietDonThuocs)
                .HasForeignKey(d => d.MaDonThuoc)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChiTietDo__MaDon__7A672E12");

            entity.HasOne(d => d.MaThuocNavigation).WithMany(p => p.ChiTietDonThuocs)
                .HasForeignKey(d => d.MaThuoc)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChiTietDo__MaThu__7B5B524B");
        });

        modelBuilder.Entity<DanhMucIcd>(entity =>
        {
            entity.HasKey(e => e.MaIcd).HasName("PK__DanhMucI__3B5EE7558DD9B721");

            entity.ToTable("DanhMucICD");

            entity.Property(e => e.MaIcd)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaICD");
            entity.Property(e => e.TenBenh).HasMaxLength(255);
        });

        modelBuilder.Entity<DanhMucThuoc>(entity =>
        {
            entity.HasKey(e => e.MaThuoc).HasName("PK__DanhMucT__4BB1F620EB14D20E");

            entity.ToTable("DanhMucThuoc");

            entity.Property(e => e.MaThuoc)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.DonViTinh).HasMaxLength(50);
            entity.Property(e => e.HoatChat).HasMaxLength(255);
            entity.Property(e => e.TenThuoc).HasMaxLength(255);
        });

        modelBuilder.Entity<DatLichKham>(entity =>
        {
            entity.HasKey(e => e.MaDatLich).HasName("PK__DatLichK__35B3DED8B5D49E18");

            entity.ToTable("DatLichKham");

            entity.Property(e => e.HoTenKhach).HasMaxLength(100);
            entity.Property(e => e.Sdt)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasColumnName("SDT");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(50)
                .HasDefaultValue("ChoXacNhan");
        });

        modelBuilder.Entity<DichVuYte>(entity =>
        {
            entity.HasKey(e => e.MaDv).HasName("PK__DichVuYT__272586570B6B445E");

            entity.ToTable("DichVuYTe");

            entity.Property(e => e.MaDv)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaDV");
            entity.Property(e => e.GiaTien).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MaLoaiDv).HasColumnName("MaLoaiDV");
            entity.Property(e => e.TenDv)
                .HasMaxLength(255)
                .HasColumnName("TenDV");
            entity.Property(e => e.TrangThai).HasDefaultValue(true);

            entity.HasOne(d => d.MaLoaiDvNavigation).WithMany(p => p.DichVuYtes)
                .HasForeignKey(d => d.MaLoaiDv)
                .HasConstraintName("FK__DichVuYTe__MaLoa__59FA5E80");
        });

        modelBuilder.Entity<DonThuoc>(entity =>
        {
            entity.HasKey(e => e.MaDonThuoc).HasName("PK__DonThuoc__3EF99EE17DD94185");

            entity.ToTable("DonThuoc");

            entity.Property(e => e.MaDonThuoc)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MaPhieu)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.NgayKeDon)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.MaPhieuNavigation).WithMany(p => p.DonThuocs)
                .HasForeignKey(d => d.MaPhieu)
                .HasConstraintName("FK__DonThuoc__MaPhie__76969D2E");
        });

        modelBuilder.Entity<HoaDon>(entity =>
        {
            entity.HasKey(e => e.MaHoaDon).HasName("PK__HoaDon__835ED13BA315B1D9");

            entity.ToTable("HoaDon");

            entity.Property(e => e.MaHoaDon)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MaNvThuNgan)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaNV_ThuNgan");
            entity.Property(e => e.MaPhieu)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.NgayThanhToan)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ThanhTien).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TongTienDichVu)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TongTienThuoc)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TrangThaiThanhToan).HasDefaultValue(false);

            entity.HasOne(d => d.MaNvThuNganNavigation).WithMany(p => p.HoaDons)
                .HasForeignKey(d => d.MaNvThuNgan)
                .HasConstraintName("FK__HoaDon__MaNV_Thu__00200768");

            entity.HasOne(d => d.MaPhieuNavigation).WithMany(p => p.HoaDons)
                .HasForeignKey(d => d.MaPhieu)
                .HasConstraintName("FK__HoaDon__MaPhieu__7F2BE32F");
        });

        modelBuilder.Entity<LoThuoc>(entity =>
        {
            entity.HasKey(e => e.MaLo).HasName("PK__LoThuoc__2725C7562E7514FF");

            entity.ToTable("LoThuoc");

            entity.Property(e => e.MaLo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.GiaBan).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GiaNhap).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MaNcc).HasColumnName("MaNCC");
            entity.Property(e => e.MaThuoc)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.MaNccNavigation).WithMany(p => p.LoThuocs)
                .HasForeignKey(d => d.MaNcc)
                .HasConstraintName("FK__LoThuoc__MaNCC__73BA3083");

            entity.HasOne(d => d.MaThuocNavigation).WithMany(p => p.LoThuocs)
                .HasForeignKey(d => d.MaThuoc)
                .HasConstraintName("FK__LoThuoc__MaThuoc__72C60C4A");
        });

        modelBuilder.Entity<LoaiDichVu>(entity =>
        {
            entity.HasKey(e => e.MaLoaiDv).HasName("PK__LoaiDich__12274865859A7ED5");

            entity.ToTable("LoaiDichVu");

            entity.Property(e => e.MaLoaiDv).HasColumnName("MaLoaiDV");
            entity.Property(e => e.TenLoai).HasMaxLength(100);
        });

        modelBuilder.Entity<NhaCungCap>(entity =>
        {
            entity.HasKey(e => e.MaNcc).HasName("PK__NhaCungC__3A185DEB37242F1B");

            entity.ToTable("NhaCungCap");

            entity.Property(e => e.MaNcc).HasColumnName("MaNCC");
            entity.Property(e => e.DiaChi).HasMaxLength(255);
            entity.Property(e => e.Sdt)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasColumnName("SDT");
            entity.Property(e => e.TenNcc)
                .HasMaxLength(255)
                .HasColumnName("TenNCC");
        });

        modelBuilder.Entity<NhanVien>(entity =>
        {
            entity.HasKey(e => e.MaNv).HasName("PK__NhanVien__2725D70A2C6222B4");

            entity.ToTable("NhanVien");

            entity.HasIndex(e => e.UserId, "UQ__NhanVien__1788CCADB7EC7681").IsUnique();

            entity.Property(e => e.MaNv)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaNV");
            entity.Property(e => e.ChuyenMon).HasMaxLength(100);
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.HoTen).HasMaxLength(100);
            entity.Property(e => e.Sdt)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasColumnName("SDT");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithOne(p => p.NhanVien)
                .HasForeignKey<NhanVien>(d => d.UserId)
                .HasConstraintName("FK__NhanVien__UserID__534D60F1");
        });

        modelBuilder.Entity<PhieuKham>(entity =>
        {
            entity.HasKey(e => e.MaPhieu).HasName("PK__PhieuKha__2660BFE00DD3E179");

            entity.ToTable("PhieuKham");

            entity.Property(e => e.MaPhieu)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.HuyetAp)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MaBacSi)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MaBn)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaBN");
            entity.Property(e => e.MaIcd)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaICD");
            entity.Property(e => e.MaNvTiepDon)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaNV_TiepDon");
            entity.Property(e => e.NgayKham)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TrangThaiKham).HasDefaultValue(0);

            entity.HasOne(d => d.MaBacSiNavigation).WithMany(p => p.PhieuKhamMaBacSiNavigations)
                .HasForeignKey(d => d.MaBacSi)
                .HasConstraintName("FK__PhieuKham__MaBac__6477ECF3");

            entity.HasOne(d => d.MaBnNavigation).WithMany(p => p.PhieuKhams)
                .HasForeignKey(d => d.MaBn)
                .HasConstraintName("FK__PhieuKham__MaBN__628FA481");

            entity.HasOne(d => d.MaIcdNavigation).WithMany(p => p.PhieuKhams)
                .HasForeignKey(d => d.MaIcd)
                .HasConstraintName("FK__PhieuKham__MaICD__66603565");

            entity.HasOne(d => d.MaNvTiepDonNavigation).WithMany(p => p.PhieuKhamMaNvTiepDonNavigations)
                .HasForeignKey(d => d.MaNvTiepDon)
                .HasConstraintName("FK__PhieuKham__MaNV___6383C8BA");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE3AC8D0C676");

            entity.HasIndex(e => e.RoleName, "UQ__Roles__8A2B616024DB0518").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CCACC395A10A");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E47AEB3E09").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK__Users__RoleID__4E88ABD4");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
