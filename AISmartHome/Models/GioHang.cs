using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("GioHang")]
public partial class GioHang
{
    [Key]
    public int MaGioHang { get; set; }

    public int? MaKhachHang { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? TongTien { get; set; }

    [InverseProperty("MaGioHangNavigation")]
    public virtual ICollection<ChiTietGioHang> ChiTietGioHangs { get; set; } = new List<ChiTietGioHang>();

    [ForeignKey("MaKhachHang")]
    [InverseProperty("GioHangs")]
    public virtual KhachHang? MaKhachHangNavigation { get; set; }
}
