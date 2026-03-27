using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("HinhAnhSanPham")]
public partial class HinhAnhSanPham
{
    [Key]
    public int MaHinhAnh { get; set; }

    [StringLength(255)]
    public string UrlHinhAnh { get; set; } = null!;

    public int MaSanPham { get; set; }

    [ForeignKey("MaSanPham")]
    [InverseProperty("HinhAnhSanPhams")]
    public virtual SanPham MaSanPhamNavigation { get; set; } = null!;
}
