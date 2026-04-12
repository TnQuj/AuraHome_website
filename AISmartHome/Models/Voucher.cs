using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AISmartHome.Models
{
    public class Voucher
    {
        [Key]
        public int MaVoucher { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; }

        [StringLength(255)]
        public string MoTa { get; set; }

        [StringLength(50)]
        public string LoaiGiamGia { get; set; } // "PhanTram" hoặc "SoTien"

        [Column(TypeName = "decimal(18,2)")]
        public decimal GiaTriGiam { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GiaTriDonToiThieu { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GiamToiDa { get; set; } = 0;

        public DateTime NgayBatDau { get; set; }
        public DateTime NgayHetHan { get; set; }

        public int SoLuongToiDa { get; set; } = 100;
        public int SoLuongDaDung { get; set; } = 0;

        public bool TrangThai { get; set; } = true;

        // Liên kết 1 Voucher - Nhiều Lịch sử sử dụng
        public virtual ICollection<VoucherHistory> VoucherHistories { get; set; } = new List<VoucherHistory>();
    }
}