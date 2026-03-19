using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("NhanVien")]
public partial class NhanVien
{
    [Key]
    public int MaNhanVien { get; set; }

    [StringLength(255)]
    public string? TenNhanVien { get; set; }

    [StringLength(15)]
    [Unicode(false)]
    public string? SoDienThoai { get; set; }

    [StringLength(255)]
    public string? DiaChi { get; set; }

    public int? MaTaiKhoan { get; set; }

    [ForeignKey("MaTaiKhoan")]
    [InverseProperty("NhanViens")]
    public virtual TaiKhoan? MaTaiKhoanNavigation { get; set; }

    [InverseProperty("MaNhanVienNavigation")]
    public virtual ICollection<YeuCauLapDat> YeuCauLapDats { get; set; } = new List<YeuCauLapDat>();
}
