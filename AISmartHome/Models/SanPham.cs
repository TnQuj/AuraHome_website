using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("SanPham")]
public partial class SanPham
{
    [Key]
    public int MaSanPham { get; set; }

    [StringLength(255)]
    public string TenSanPham { get; set; } = null!;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal GiaBan { get; set; }

    public string? MoTa { get; set; }

    [StringLength(255)]
    public string? HinhAnh { get; set; }

    public int? SoLuong { get; set; }

    public int? MaDanhMuc { get; set; }

    [InverseProperty("MaSanPhamNavigation")]
    public virtual ICollection<ChiTietDonHang> ChiTietDonHangs { get; set; } = new List<ChiTietDonHang>();

    [InverseProperty("MaSanPhamNavigation")]
    public virtual ICollection<ChiTietGioHang> ChiTietGioHangs { get; set; } = new List<ChiTietGioHang>();

    [InverseProperty("MaSanPhamNavigation")]
    public virtual ICollection<HuongDanSuDung> HuongDanSuDungs { get; set; } = new List<HuongDanSuDung>();

    [ForeignKey("MaDanhMuc")]
    [InverseProperty("SanPhams")]
    public virtual DanhMucSanPham? MaDanhMucNavigation { get; set; }
}
