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

    public int? DiemTichLuy { get; set; } = 0;

    public string? HangThanhVien { get; set; } = "Đồng";

    // Liên kết 1 Khách hàng - Nhiều Lịch sử dùng Voucher
    public virtual ICollection<VoucherHistory> VoucherHistories { get; set; } = new List<VoucherHistory>();

    [InverseProperty("MaKhachHangNavigation")]
    public virtual ICollection<DonHang> DonHangs { get; set; } = new List<DonHang>();

    [InverseProperty("MaKhachHangNavigation")]
    public virtual ICollection<GioHang> GioHangs { get; set; } = new List<GioHang>();
}
