using System.ComponentModel.DataAnnotations;

namespace AISmartHome.Models
{
    public class NhanVienViewModel
    {
        // === THÔNG TIN CÁ NHÂN ===
        [Required(ErrorMessage = "Vui lòng nhập tên nhân viên")]
        public string? TenNhanVien { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        public string? SoDienThoai { get; set; }

        public string? DiaChi { get; set; }

        // === THÔNG TIN TÀI KHOẢN ĐĂNG NHẬP ===
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        public string? TenDangNhap { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên")]
        public string? MatKhau { get; set; }
    }
}