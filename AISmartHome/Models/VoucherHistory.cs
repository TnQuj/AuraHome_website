using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AISmartHome.Models
{
    [Table("VoucherHistory")]
    public class VoucherHistory
    {
        [Key]
        public int Id { get; set; }

        [StringLength(255)]
        public string Email { get; set; }

        // Khóa ngoại liên kết bảng Voucher
        public int? MaVoucher { get; set; }
        [ForeignKey("MaVoucher")]
        public virtual Voucher VoucherNavigation { get; set; }

        // Khóa ngoại liên kết bảng KhachHang
        public int? MaKhachHang { get; set; }
        [ForeignKey("MaKhachHang")]
        public virtual KhachHang KhachHangNavigation { get; set; }

        public DateTime NgaySuDung { get; set; } = DateTime.Now;

        // Lưu lại mã đơn hàng để biết khách dùng voucher này cho đơn nào
        public int? MaDonHang { get; set; }
    }
}