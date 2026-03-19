using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("DonHang")]
public partial class DonHang
{
    [Key]
    public int MaDonHang { get; set; }

    public int? MaKhachHang { get; set; }

    [StringLength(255)]
    public string? TenKhachHang { get; set; }

    [StringLength(15)]
    [Unicode(false)]
    public string? SoDienThoai { get; set; }

    [StringLength(255)]
    public string? DiaChiGiaoHang { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayDatHang { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? TongTien { get; set; }

    [StringLength(100)]
    public string? TrangThaiDonHang { get; set; }

    [InverseProperty("MaDonHangNavigation")]
    public virtual ICollection<ChiTietDonHang> ChiTietDonHangs { get; set; } = new List<ChiTietDonHang>();

    [ForeignKey("MaKhachHang")]
    [InverseProperty("DonHangs")]
    public virtual KhachHang? MaKhachHangNavigation { get; set; }

    [InverseProperty("MaDonHangNavigation")]
    public virtual ICollection<YeuCauLapDat> YeuCauLapDats { get; set; } = new List<YeuCauLapDat>();
}
