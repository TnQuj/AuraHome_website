using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json.Serialization;
using System;
using AISmartHome.Data;
using AISmartHome.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;

namespace AISmartHome.Controllers // Nhớ đổi tên namespace cho đúng với project của bạn
{
    [Route("api/[controller]")]
    [ApiController]
    public class OtpController : Controller
    {
        private readonly IMemoryCache _cache;
        private readonly AISmartHomeDbContext _context; // 👈 KHAI BÁO THÊM CONTEXT

        public OtpController(IMemoryCache cache, AISmartHomeDbContext context)
        {
            _cache = cache;
            _context = context;
        }

        [HttpPost("send")]
        public IActionResult SendOtp([FromBody] OtpRequest request)
        {
            // 1. Kiểm tra SĐT từ giao diện gửi lên
            if (request == null || string.IsNullOrEmpty(request.Phone))
            {
                return BadRequest(new { success = false, message = "Không nhận được số điện thoại!" });
            }

            // 2. Làm sạch SĐT (Đưa về chuẩn 09xxx...)
            string cleanPhone = request.Phone.Replace(" ", "").Replace(".", "").Replace("-", "").Trim();
            if (cleanPhone.StartsWith("84")) cleanPhone = "0" + cleanPhone.Substring(2);

            // 3. Tạo mã OTP giả lập gồm 6 chữ số
            string otpCode = new Random().Next(100000, 999999).ToString();

            // 4. LƯU VÀO CACHE (Thời hạn 5 phút)
            _cache.Set($"OTP_{cleanPhone}", otpCode, TimeSpan.FromMinutes(5));

            // 5. Trả mã về cho giao diện
            return Ok(new { success = true, otp = otpCode });
        }
        [HttpPost("VerifyOtpLogin")]
        public async Task<IActionResult> VerifyOtpLogin([FromForm] string phone, [FromForm] string otpCode, [FromForm] string fullName, [FromForm] string email)
        {
            // 1. Kiểm tra đầu vào (Nên log thử để xem dữ liệu có vào đến đây không)
            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(otpCode))
            {
                return Json(new { success = false, message = "Dữ liệu bị thất lạc khi gửi lên máy chủ!" });
            }

            string cleanPhone = phone.Replace(" ", "").Replace(".", "").Replace("-", "").Trim();
            if (cleanPhone.StartsWith("84")) cleanPhone = "0" + cleanPhone.Substring(2);

            // 2. KIỂM TRA MÃ OTP TRONG CACHE 
            if (!_cache.TryGetValue($"OTP_{cleanPhone}", out string savedOtp))
            {
                return Json(new { success = false, message = "Mã OTP đã hết hạn!" });
            }

            if (savedOtp != otpCode.Trim())
            {
                return Json(new { success = false, message = "Mã OTP không chính xác!" });
            }

            _cache.Remove($"OTP_{cleanPhone}");

            // 3. TÌM HOẶC TẠO MỚI (Dùng try-catch để bắt lỗi DB nếu có)
            try
            {
                var khachHang = await _context.KhachHangs.FirstOrDefaultAsync(k => k.SoDienThoai == cleanPhone);

                if (khachHang == null)
                {
                    khachHang = new KhachHang
                    {
                        SoDienThoai = cleanPhone,
                        TenKhachHang = !string.IsNullOrEmpty(fullName) ? fullName : "Khách vãng lai",
                        Email = email,
                        ThoiGianTruyCap = DateTime.Now
                    };
                    _context.KhachHangs.Add(khachHang);
                }
                else
                {
                    // Cập nhật tên nếu khách hàng cung cấp tên mới
                    if (!string.IsNullOrEmpty(fullName))
                    {
                        khachHang.TenKhachHang = fullName;
                    }
                    if (string.IsNullOrEmpty(khachHang.Email))
                    {
                        khachHang.Email = email;
                    }
                    khachHang.ThoiGianTruyCap = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                // 4. CẤP THẺ ĐĂNG NHẬP
                var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, khachHang.MaKhachHang.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, khachHang.SoDienThoai)
        };

                var claimsIdentity = new System.Security.Claims.ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
                    new System.Security.Claims.ClaimsPrincipal(claimsIdentity),
                    new Microsoft.AspNetCore.Authentication.AuthenticationProperties { IsPersistent = true });

                // Cập nhật Cookie đồng bộ cho trình duyệt
                Response.Cookies.Append("VerifiedPhone", cleanPhone, new CookieOptions { Expires = DateTime.Now.AddDays(30) });

                return Json(new { success = true, message = "Lưu hồ sơ thành công!" });
            }
            catch (Exception ex)
            {
                // Nếu có lỗi ở DB (ví dụ sai kiểu dữ liệu), nó sẽ báo ở đây thay vì im lặng
                return Json(new { success = false, message = "Lỗi lưu Database: " + ex.Message });
            }
        }
        // =========================================================
        // 2. API HỎI THĂM TRẠNG THÁI (JS SẼ GỌI LIÊN TỤC 3 GIÂY 1 LẦN)
        // =========================================================
        [HttpGet("CheckStatus")]
        public async Task<IActionResult> CheckStatus(int orderId)
        {
            var donHang = await _context.DonHangs.FindAsync(orderId);
            if (donHang == null) return NotFound();

            // Kiểm tra xem trạng thái đã được đổi thành "Đã thanh toán" hay chưa
            bool isPaid = donHang.TrangThaiThanhToan == "Đã thanh toán" || donHang.TrangThaiThanhToan == "Đã cọc";

            return Ok(new { isPaid = isPaid });
        }

        // =========================================================
        // 3. API HỦY ĐƠN HÀNG (KHI BẤM NÚT HỦY)
        // =========================================================
        [HttpPost("CancelOrder")]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var donHang = await _context.DonHangs.FindAsync(orderId);
            if (donHang != null && donHang.TrangThaiDonHang == "Chờ xử lý")
            {
                // Trả lại số lượng tồn kho cho sản phẩm
                var chiTiet = _context.ChiTietDonHangs.Where(c => c.MaDonHang == orderId).ToList();
                foreach (var item in chiTiet)
                {
                    var sp = await _context.SanPhams.FindAsync(item.MaSanPham);
                    if (sp != null) sp.SoLuong += item.SoLuong;
                }

                // Đổi trạng thái thành Đã hủy
                donHang.TrangThaiDonHang = "Đã hủy";
                await _context.SaveChangesAsync();
            }
            return Ok(new { success = true });
        }

        [HttpGet("SimulatePayment")]
        public async Task<IActionResult> SimulatePayment(int orderId)
        {
            var donHang = await _context.DonHangs.FindAsync(orderId);
            if (donHang != null)
            {
                donHang.TrangThaiThanhToan = "Đã thanh toán"; // Đổi trạng thái
                await _context.SaveChangesAsync();

                // Trả về một trang HTML đẹp mắt hiển thị trên điện thoại
                string html = @"
                    <html>
                    <body style='display:flex; justify-content:center; align-items:center; height:100vh; background-color:#f8fafc; font-family:sans-serif;'>
                        <div style='text-align:center; padding:20px; background:white; border-radius:15px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                            <h1 style='color:#10b981; font-size:40px; margin-bottom:10px;'>✅</h1>
                            <h2 style='color:#0f172a;'>Giả lập thanh toán thành công!</h2>
                            <p style='color:#64748b;'>Đã báo cho máy chủ. Hãy nhìn lên màn hình máy tính của bạn!</p>
                        </div>
                    </body>
                    </html>";
                return Content(html, "text/html");
            }
            return Content("Không tìm thấy đơn hàng!");
        }
    }
} // <-- Dấu ngoặc đóng của class OtpController nằm ở đây mới chuẩn!

    // ========================================================
    // CÁC CLASS HỖ TRỢ ĐỌC DỮ LIỆU
    // ========================================================
    public class OtpRequest
    {
        [JsonPropertyName("phone")]
        public string Phone { get; set; }
    }

    public class VerifyRequest
    {
        [JsonPropertyName("phone")]
        public string Phone { get; set; }

        [JsonPropertyName("otp")]
        public string Otp { get; set; }
    }
