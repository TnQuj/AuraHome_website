using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("BaiViet")]
public partial class BaiViet
{
    [Key]
    public int MaBaiViet { get; set; }

    [StringLength(255)]
    public string? TieuDe { get; set; }

    public string? NoiDung { get; set; }

    [StringLength(255)]
    public string? HinhAnh { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayDang { get; set; }

    public int? MaDanhMucBaiViet { get; set; }

    public int? MaTaiKhoan { get; set; }

    [ForeignKey("MaDanhMucBaiViet")]
    [InverseProperty("BaiViets")]
    public virtual DanhMucBaiViet? MaDanhMucBaiVietNavigation { get; set; }

    [ForeignKey("MaTaiKhoan")]
    [InverseProperty("BaiViets")]
    public virtual TaiKhoan? MaTaiKhoanNavigation { get; set; }
    // Trong Models/BaiViet.cs
    public bool IsApproved { get; set; } = false; // Mặc định là chưa duyệt
    public string? NguonTin { get; set; } // Lưu link gốc để đối chiếu
}
