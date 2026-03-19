using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("YeuCauLapDat")]
public partial class YeuCauLapDat
{
    [Key]
    public int MaYeuCauLapDat { get; set; }

    public int? MaDonHang { get; set; }

    [StringLength(255)]
    public string? DiaChiLapDat { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayLap { get; set; }

    [StringLength(100)]
    public string? TrangThaiLapDat { get; set; }

    public int? MaNhanVien { get; set; }

    [ForeignKey("MaDonHang")]
    [InverseProperty("YeuCauLapDats")]
    public virtual DonHang? MaDonHangNavigation { get; set; }

    [ForeignKey("MaNhanVien")]
    [InverseProperty("YeuCauLapDats")]
    public virtual NhanVien? MaNhanVienNavigation { get; set; }
}
