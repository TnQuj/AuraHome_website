using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("KhachHang")]
public partial class KhachHang
{
    [Key]
    public int MaKhachHang { get; set; }

    [StringLength(255)]
    public string? TenKhachHang { get; set; }

    [StringLength(15)]
    [Unicode(false)]
    public string? SoDienThoai { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string? Email { get; set; }

    [StringLength(255)]
    public string? DiaChi { get; set; }

    [InverseProperty("MaKhachHangNavigation")]
    public virtual ICollection<DonHang> DonHangs { get; set; } = new List<DonHang>();

    [InverseProperty("MaKhachHangNavigation")]
    public virtual ICollection<GioHang> GioHangs { get; set; } = new List<GioHang>();
}
