using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("VaiTro")]
public partial class VaiTro
{
    [Key]
    public int MaVaiTro { get; set; }

    [StringLength(100)]
    public string? TenVaiTro { get; set; }

    [StringLength(255)]
    public string? MoTa { get; set; }

    [InverseProperty("MaVaiTroNavigation")]
    public virtual ICollection<TaiKhoan> TaiKhoans { get; set; } = new List<TaiKhoan>();
}
