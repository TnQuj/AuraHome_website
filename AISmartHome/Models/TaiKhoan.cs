using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("TaiKhoan")]
[Index("TenDangNhap", Name = "UQ__TaiKhoan__55F68FC08677D6B2", IsUnique = true)]
public partial class TaiKhoan
{
    [Key]
    public int MaTaiKhoan { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? TenDangNhap { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string? MatKhau { get; set; }

    public int? MaVaiTro { get; set; }

    public bool? TrangThai { get; set; }

    [InverseProperty("MaTaiKhoanNavigation")]
    public virtual ICollection<BaiViet> BaiViets { get; set; } = new List<BaiViet>();

    [ForeignKey("MaVaiTro")]
    [InverseProperty("TaiKhoans")]
    public virtual VaiTro? MaVaiTroNavigation { get; set; }

    [InverseProperty("MaTaiKhoanNavigation")]
    public virtual ICollection<NhanVien> NhanViens { get; set; } = new List<NhanVien>();
}
