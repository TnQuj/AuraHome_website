using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("DanhMucSanPham")]
public partial class DanhMucSanPham
{
    [Key]
    public int MaDanhMuc { get; set; }

    [StringLength(255)]
    public string TenDanhMuc { get; set; } = null!;

    public string? MoTa { get; set; }

    [InverseProperty("MaDanhMucNavigation")]
    public virtual ICollection<SanPham> SanPhams { get; set; } = new List<SanPham>();
}
