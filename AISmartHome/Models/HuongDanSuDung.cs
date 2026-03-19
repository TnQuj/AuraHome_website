using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("HuongDanSuDung")]
public partial class HuongDanSuDung
{
    [Key]
    public int MaHuongDan { get; set; }

    public int? MaSanPham { get; set; }

    [StringLength(255)]
    public string? TieuDe { get; set; }

    public string? NoiDung { get; set; }

    [StringLength(255)]
    public string? VideoUrl { get; set; }

    [ForeignKey("MaSanPham")]
    [InverseProperty("HuongDanSuDungs")]
    public virtual SanPham? MaSanPhamNavigation { get; set; }
}
