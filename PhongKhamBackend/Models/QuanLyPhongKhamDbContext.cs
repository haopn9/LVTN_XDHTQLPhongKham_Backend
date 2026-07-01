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

    public virtual DbSet<ChiTietDichVuYte> ChiTietDichVuYtes { get; set; }

    public virtual DbSet<ChiTietDonThuoc> ChiTietDonThuocs { get; set; }

    public virtual DbSet<ChiTietVatTuPhieuKham> ChiTietVatTuPhieuKhams { get; set; }

    public virtual DbSet<DanhMucIcd> DanhMucIcds { get; set; }

    public virtual DbSet<DanhMucKhoa> DanhMucKhoas { get; set; }

    public virtual DbSet<DanhMucThuoc> DanhMucThuocs { get; set; }

    public virtual DbSet<DanhMucVatTu> DanhMucVatTus { get; set; }

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
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=QuanLyPhongKham_DB;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BenhNhan>(entity =>
        {
            entity.HasKey(e => e.MaBn).HasName("PK__BenhNhan__272475AD10698BB0");

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

        modelBuilder.Entity<ChiTietDichVuYte>(entity =>
        {
            entity.HasKey(e => e.MaDv).HasName("PK__ChiTietD__27258657DE1F580E");

            entity.ToTable("ChiTietDichVuYTe");

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

            entity.HasOne(d => d.MaLoaiDvNavigation).WithMany(p => p.ChiTietDichVuYtes)
                .HasForeignKey(d => d.MaLoaiDv)
                .HasConstraintName("FK__ChiTietDi__MaLoa__5DCAEF64");
        });

        modelBuilder.Entity<ChiTietDonThuoc>(entity =>
        {
            entity.HasKey(e => new { e.MaDonThuoc, e.MaThuoc }).HasName("PK__ChiTietD__2A4281837865FE95");

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
                .HasConstraintName("FK__ChiTietDo__MaDon__01142BA1");

            entity.HasOne(d => d.MaThuocNavigation).WithMany(p => p.ChiTietDonThuocs)
                .HasForeignKey(d => d.MaThuoc)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChiTietDo__MaThu__02084FDA");
        });

        modelBuilder.Entity<ChiTietVatTuPhieuKham>(entity =>
        {
            entity.HasKey(e => new { e.MaPhieu, e.MaVatTu }).HasName("PK__ChiTietV__96DD9856669FC284");

            entity.ToTable("ChiTietVatTuPhieuKham");

            entity.Property(e => e.MaPhieu)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MaVatTu)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.DonGia).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.MaPhieuNavigation).WithMany(p => p.ChiTietVatTuPhieuKhams)
                .HasForeignKey(d => d.MaPhieu)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChiTietVa__MaPhi__08B54D69");

            entity.HasOne(d => d.MaVatTuNavigation).WithMany(p => p.ChiTietVatTuPhieuKhams)
                .HasForeignKey(d => d.MaVatTu)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChiTietVa__MaVat__09A971A2");
        });

        modelBuilder.Entity<DanhMucIcd>(entity =>
        {
            entity.HasKey(e => e.MaIcd).HasName("PK__DanhMucI__3B5EE7552A34A347");

            entity.ToTable("DanhMucICD");

            entity.Property(e => e.MaIcd)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaICD");
            entity.Property(e => e.TenBenh).HasMaxLength(255);
        });

        modelBuilder.Entity<DanhMucKhoa>(entity =>
        {
            entity.HasKey(e => e.MaKhoa).HasName("PK__DanhMucK__653904055E806A13");

            entity.ToTable("DanhMucKhoa");

            entity.HasIndex(e => e.TenKhoa, "UQ__DanhMucK__AAD36158867FAF6B").IsUnique();

            entity.Property(e => e.MaKhoa)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.TenKhoa).HasMaxLength(100);
        });

        modelBuilder.Entity<DanhMucThuoc>(entity =>
        {
            entity.HasKey(e => e.MaThuoc).HasName("PK__DanhMucT__4BB1F620C7A172E3");

            entity.ToTable("DanhMucThuoc");

            entity.Property(e => e.MaThuoc)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.DonViTinh).HasMaxLength(50);
            entity.Property(e => e.HoatChat).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TenThuoc).HasMaxLength(255);
        });

        modelBuilder.Entity<DanhMucVatTu>(entity =>
        {
            entity.HasKey(e => e.MaVatTu).HasName("PK__DanhMucV__0BD27B6AD9EB7A67");

            entity.ToTable("DanhMucVatTu");

            entity.Property(e => e.MaVatTu)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.DonViTinh).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.QuyCach).HasMaxLength(255);
            entity.Property(e => e.TenVatTu).HasMaxLength(255);
        });

        modelBuilder.Entity<DatLichKham>(entity =>
        {
            entity.HasKey(e => e.MaDatLich).HasName("PK__DatLichK__35B3DED8D1A17B24");

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
            entity.HasKey(e => e.MaChiTiet).HasName("PK__DichVuYT__CDF0A1149764F3D7");

            entity.ToTable("DichVuYTe");

            entity.Property(e => e.MaDv)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaDV");
            entity.Property(e => e.MaPhieu)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.TrangThaiDichVu).HasDefaultValue(0);

            entity.HasOne(d => d.MaDvNavigation).WithMany(p => p.DichVuYtes)
                .HasForeignKey(d => d.MaDv)
                .HasConstraintName("FK__DichVuYTe__MaDV__70DDC3D8");

            entity.HasOne(d => d.MaPhieuNavigation).WithMany(p => p.DichVuYtes)
                .HasForeignKey(d => d.MaPhieu)
                .HasConstraintName("FK__DichVuYTe__MaPhi__6FE99F9F");
        });

        modelBuilder.Entity<DonThuoc>(entity =>
        {
            entity.HasKey(e => e.MaDonThuoc).HasName("PK__DonThuoc__3EF99EE1C233F37F");

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
                .HasConstraintName("FK__DonThuoc__MaPhie__7D439ABD");
        });

        modelBuilder.Entity<HoaDon>(entity =>
        {
            entity.HasKey(e => e.MaHoaDon).HasName("PK__HoaDon__835ED13B60E9587B");

            entity.ToTable("HoaDon");

            entity.Property(e => e.MaHoaDon)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MaNv)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaNV");
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

            entity.HasOne(d => d.MaNvNavigation).WithMany(p => p.HoaDons)
                .HasForeignKey(d => d.MaNv)
                .HasConstraintName("FK__HoaDon__MaNV__0D7A0286");

            entity.HasOne(d => d.MaPhieuNavigation).WithMany(p => p.HoaDons)
                .HasForeignKey(d => d.MaPhieu)
                .HasConstraintName("FK__HoaDon__MaPhieu__0C85DE4D");
        });

        modelBuilder.Entity<LoThuoc>(entity =>
        {
            entity.HasKey(e => e.MaLo).HasName("PK__LoThuoc__2725C756E5AA4151");

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
                .HasConstraintName("FK__LoThuoc__MaNCC__7A672E12");

            entity.HasOne(d => d.MaThuocNavigation).WithMany(p => p.LoThuocs)
                .HasForeignKey(d => d.MaThuoc)
                .HasConstraintName("FK__LoThuoc__MaThuoc__797309D9");
        });

        modelBuilder.Entity<LoaiDichVu>(entity =>
        {
            entity.HasKey(e => e.MaLoaiDv).HasName("PK__LoaiDich__12274865EE53E8BC");

            entity.ToTable("LoaiDichVu");

            entity.Property(e => e.MaLoaiDv).HasColumnName("MaLoaiDV");
            entity.Property(e => e.TenLoai).HasMaxLength(100);
        });

        modelBuilder.Entity<NhaCungCap>(entity =>
        {
            entity.HasKey(e => e.MaNcc).HasName("PK__NhaCungC__3A185DEBF787CE3E");

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
            entity.HasKey(e => e.MaNv).HasName("PK__NhanVien__2725D70A6EC2C541");

            entity.ToTable("NhanVien");

            entity.HasIndex(e => e.UserId, "UQ__NhanVien__1788CCADEE7324B3").IsUnique();

            entity.Property(e => e.MaNv)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaNV");
            entity.Property(e => e.ChuyenMon).HasMaxLength(100);
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.HoTen).HasMaxLength(100);
            entity.Property(e => e.MaKhoa)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Sdt)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasColumnName("SDT");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.MaKhoaNavigation).WithMany(p => p.NhanViens)
                .HasForeignKey(d => d.MaKhoa)
                .HasConstraintName("FK__NhanVien__MaKhoa__571DF1D5");

            entity.HasOne(d => d.User).WithOne(p => p.NhanVien)
                .HasForeignKey<NhanVien>(d => d.UserId)
                .HasConstraintName("FK__NhanVien__UserID__5629CD9C");
        });

        modelBuilder.Entity<PhieuKham>(entity =>
        {
            entity.HasKey(e => e.MaPhieu).HasName("PK__PhieuKha__2660BFE04255DB24");

            entity.ToTable("PhieuKham");

            entity.Property(e => e.MaPhieu)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.HuyetAp)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MaBn)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaBN");
            entity.Property(e => e.MaNv)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("MaNV");
            entity.Property(e => e.NgayKham)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TrangThaiKham).HasDefaultValue(0);

            entity.HasOne(d => d.MaBnNavigation).WithMany(p => p.PhieuKhams)
                .HasForeignKey(d => d.MaBn)
                .HasConstraintName("FK__PhieuKham__MaBN__66603565");

            entity.HasOne(d => d.MaNvNavigation).WithMany(p => p.PhieuKhams)
                .HasForeignKey(d => d.MaNv)
                .HasConstraintName("FK__PhieuKham__MaNV__6754599E");

            entity.HasMany(d => d.MaIcds).WithMany(p => p.MaPhieus)
                .UsingEntity<Dictionary<string, object>>(
                    "ChiTietPhieuKhamIcd",
                    r => r.HasOne<DanhMucIcd>().WithMany()
                        .HasForeignKey("MaIcd")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__ChiTietPh__MaICD__6D0D32F4"),
                    l => l.HasOne<PhieuKham>().WithMany()
                        .HasForeignKey("MaPhieu")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__ChiTietPh__MaPhi__6C190EBB"),
                    j =>
                    {
                        j.HasKey("MaPhieu", "MaIcd").HasName("PK__ChiTietP__65D5519523C26A7E");
                        j.ToTable("ChiTietPhieuKhamICD");
                        j.IndexerProperty<string>("MaPhieu")
                            .HasMaxLength(20)
                            .IsUnicode(false);
                        j.IndexerProperty<string>("MaIcd")
                            .HasMaxLength(20)
                            .IsUnicode(false)
                            .HasColumnName("MaICD");
                    });
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE3AC17D4354");

            entity.HasIndex(e => e.RoleName, "UQ__Roles__8A2B6160E2390039").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CCAC3DB38950");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E4F2DB03F4").IsUnique();

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
