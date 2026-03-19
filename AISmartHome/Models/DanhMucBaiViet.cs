using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Models;

[Table("DanhMucBaiViet")]
public partial class DanhMucBaiViet
{
    [Key]
    public int MaDanhMucBaiViet { get; set; }

    [StringLength(255)]
    public string? TenDanhMuc { get; set; }

    [StringLength(255)]
    public string? MoTa { get; set; }

    [InverseProperty("MaDanhMucBaiVietNavigation")]
    public virtual ICollection<BaiViet> BaiViets { get; set; } = new List<BaiViet>();
}
