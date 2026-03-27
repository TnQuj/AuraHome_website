using System;
using System.Collections.Generic;
using AISmartHome.Models;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Data;

public partial class AISmartHomeDbContext : DbContext
{
    public AISmartHomeDbContext()
    {
    }

    public AISmartHomeDbContext(DbContextOptions<AISmartHomeDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BaiViet> BaiViets { get; set; }

    public virtual DbSet<ChiTietDonHang> ChiTietDonHangs { get; set; }

    public virtual DbSet<ChiTietGioHang> ChiTietGioHangs { get; set; }

    public virtual DbSet<DanhMucBaiViet> DanhMucBaiViets { get; set; }

    public virtual DbSet<DanhMucSanPham> DanhMucSanPhams { get; set; }

    public virtual DbSet<DonHang> DonHangs { get; set; }

    public virtual DbSet<GioHang> GioHangs { get; set; }

    public virtual DbSet<HinhAnhSanPham> HinhAnhSanPhams { get; set; }

    public virtual DbSet<HuongDanSuDung> HuongDanSuDungs { get; set; }

    public virtual DbSet<KhachHang> KhachHangs { get; set; }

    public virtual DbSet<NhanVien> NhanViens { get; set; }

    public virtual DbSet<SanPham> SanPhams { get; set; }

    public virtual DbSet<TaiKhoan> TaiKhoans { get; set; }

    public virtual DbSet<VaiTro> VaiTros { get; set; }

    public virtual DbSet<YeuCauLapDat> YeuCauLapDats { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=localhost;Database=AISmartHomeDB;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BaiViet>(entity =>
        {
            entity.HasKey(e => e.MaBaiViet).HasName("PK__BaiViet__AEDD56476223661E");

            entity.Property(e => e.NgayDang).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.MaDanhMucBaiVietNavigation).WithMany(p => p.BaiViets).HasConstraintName("FK_BaiViet_DanhMuc");

            entity.HasOne(d => d.MaTaiKhoanNavigation).WithMany(p => p.BaiViets).HasConstraintName("FK_BaiViet_TaiKhoan");
        });

        modelBuilder.Entity<ChiTietDonHang>(entity =>
        {
            entity.HasKey(e => e.MaChiTietDonHang).HasName("PK__ChiTietD__4B0B45DD4968419A");

            entity.HasOne(d => d.MaDonHangNavigation).WithMany(p => p.ChiTietDonHangs).HasConstraintName("FK_CTDH_DonHang");

            entity.HasOne(d => d.MaSanPhamNavigation).WithMany(p => p.ChiTietDonHangs).HasConstraintName("FK_CTDH_SanPham");
        });

        modelBuilder.Entity<ChiTietGioHang>(entity =>
        {
            entity.HasKey(e => e.MaChiTietGioHang).HasName("PK__ChiTietG__BBF474983058865F");

            entity.HasOne(d => d.MaGioHangNavigation).WithMany(p => p.ChiTietGioHangs).HasConstraintName("FK_CTGH_GioHang");

            entity.HasOne(d => d.MaSanPhamNavigation).WithMany(p => p.ChiTietGioHangs).HasConstraintName("FK_ChiTietGioHang_SanPham");
        });

        modelBuilder.Entity<DanhMucBaiViet>(entity =>
        {
            entity.HasKey(e => e.MaDanhMucBaiViet).HasName("PK__DanhMucB__125631BF50C4C56E");
        });

        modelBuilder.Entity<DanhMucSanPham>(entity =>
        {
            entity.HasKey(e => e.MaDanhMuc).HasName("PK__DanhMucS__B375088758A3E9D9");
        });

        modelBuilder.Entity<DonHang>(entity =>
        {
            entity.HasKey(e => e.MaDonHang).HasName("PK__DonHang__129584ADA16A2626");

            entity.Property(e => e.NgayDatHang).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.MaKhachHangNavigation).WithMany(p => p.DonHangs).HasConstraintName("FK_DonHang_KhachHang");
        });

        modelBuilder.Entity<GioHang>(entity =>
        {
            entity.HasKey(e => e.MaGioHang).HasName("PK__GioHang__F5001DA369E6DB28");

            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.MaKhachHangNavigation).WithMany(p => p.GioHangs).HasConstraintName("FK_GioHang_KhachHang");
        });

        modelBuilder.Entity<HinhAnhSanPham>(entity =>
        {
            entity.HasKey(e => e.MaHinhAnh).HasName("PK__HinhAnhS__A9C37A9BF582421D");

            entity.HasOne(d => d.MaSanPhamNavigation).WithMany(p => p.HinhAnhSanPhams).HasConstraintName("FK_HinhAnh_SanPham");
        });

        modelBuilder.Entity<HuongDanSuDung>(entity =>
        {
            entity.HasKey(e => e.MaHuongDan).HasName("PK__HuongDan__3D465C430399FAFB");

            entity.HasOne(d => d.MaSanPhamNavigation).WithMany(p => p.HuongDanSuDungs).HasConstraintName("FK_HDSD_SanPham");
        });

        modelBuilder.Entity<KhachHang>(entity =>
        {
            entity.HasKey(e => e.MaKhachHang).HasName("PK__KhachHan__88D2F0E53A7C255D");
        });

        modelBuilder.Entity<NhanVien>(entity =>
        {
            entity.HasKey(e => e.MaNhanVien).HasName("PK__NhanVien__77B2CA473040550B");

            entity.HasOne(d => d.MaTaiKhoanNavigation).WithMany(p => p.NhanViens).HasConstraintName("FK_NhanVien_TaiKhoan");
        });

        modelBuilder.Entity<SanPham>(entity =>
        {
            entity.HasKey(e => e.MaSanPham).HasName("PK__SanPham__FAC7442D357A40FF");

            entity.Property(e => e.SoLuong).HasDefaultValue(0);

            entity.HasOne(d => d.MaDanhMucNavigation).WithMany(p => p.SanPhams).HasConstraintName("FK_SanPham_DanhMuc");
        });

        modelBuilder.Entity<TaiKhoan>(entity =>
        {
            entity.HasKey(e => e.MaTaiKhoan).HasName("PK__TaiKhoan__AD7C6529FA8EEA7E");

            entity.Property(e => e.TrangThai).HasDefaultValue(true);

            entity.HasOne(d => d.MaVaiTroNavigation).WithMany(p => p.TaiKhoans).HasConstraintName("FK_TaiKhoan_VaiTro");
        });

        modelBuilder.Entity<VaiTro>(entity =>
        {
            entity.HasKey(e => e.MaVaiTro).HasName("PK__VaiTro__C24C41CFF234C65A");
        });

        modelBuilder.Entity<YeuCauLapDat>(entity =>
        {
            entity.HasKey(e => e.MaYeuCauLapDat).HasName("PK__YeuCauLa__28AAB1BCB08DEA5C");

            entity.HasOne(d => d.MaDonHangNavigation).WithMany(p => p.YeuCauLapDats).HasConstraintName("FK_YCLD_DonHang");

            entity.HasOne(d => d.MaNhanVienNavigation).WithMany(p => p.YeuCauLapDats).HasConstraintName("FK_YCLD_NhanVien");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
