using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("ChiTietDonHang")]
public partial class ChiTietDonHang
{
    [Key]
    public int MaChiTietDonHang { get; set; }

    public int? MaDonHang { get; set; }

    public int? MaSanPham { get; set; }

    public int? SoLuong { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? Gia { get; set; }

    [ForeignKey("MaDonHang")]
    [InverseProperty("ChiTietDonHangs")]
    public virtual DonHang? MaDonHangNavigation { get; set; }

    [ForeignKey("MaSanPham")]
    [InverseProperty("ChiTietDonHangs")]
    public virtual SanPham? MaSanPhamNavigation { get; set; }
}
