using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Text.Json;

namespace AISmartHome.Controllers // Nhớ đổi tên namespace cho đúng với project của bạn
{
    [Route("api/[controller]")]
    [ApiController]
    public class OtpController : ControllerBase
    {
        private readonly IMemoryCache _cache;

        // Thay bằng API Token bạn lấy từ trang quản trị SpeedSMS
        private readonly string _speedSmsToken = "EPUwkXa2lSj2lpToprSHTaDGYnpBMZqt";

        public OtpController(IMemoryCache cache)
        {
            _cache = cache;
        }

        [HttpPost("send")]
        public IActionResult SendOtp([FromBody] OtpRequest request)
        {
            if (string.IsNullOrEmpty(request.Phone))
                return BadRequest(new { success = false, message = "Số điện thoại không hợp lệ." });

            string phone = request.Phone;
            if (phone.StartsWith("0")) phone = "84" + phone.Substring(1);

            // Tạo mã OTP
            string otpCode = new Random().Next(100000, 999999).ToString();
            _cache.Set($"OTP_{phone}", otpCode, TimeSpan.FromMinutes(5));

            // --- TẠM THỜI TẮT GỌI SPEEDSMS ĐỂ TRÁNH LỖI BLOCK CODE ---
            /*
            using var client = new HttpClient();
            // ... (Code gọi SpeedSMS để lại đây chờ duyệt Brandname) ...
            */

            // IN MÃ OTP RA CONSOLE HOẶC TRẢ VỀ ĐỂ BẠN TEST TRƯỚC
            System.Diagnostics.Debug.WriteLine($"[TEST] MÃ OTP CỦA BẠN LÀ: {otpCode}");

            // Trả luôn mã OTP về cho Javascript (Chỉ dùng khi Dev, khi thật phải xóa biến otp đi nhé)
            return Ok(new { success = true, message = "Đã tạo mã OTP.", otp = otpCode });
        }

        [HttpPost("verify")]
        public IActionResult VerifyOtp([FromBody] VerifyRequest request)
        {
            string phone = request.Phone;
            if (phone.StartsWith("0"))
                phone = "84" + phone.Substring(1);

            // Kiểm tra xem OTP có tồn tại trong bộ nhớ không
            if (_cache.TryGetValue($"OTP_{phone}", out string savedOtp))
            {
                if (savedOtp == request.Otp)
                {
                    _cache.Remove($"OTP_{phone}"); // Xóa OTP sau khi dùng thành công
                    return Ok(new { success = true });
                }
            }

            return BadRequest(new { success = false, message = "Mã OTP không chính xác hoặc đã hết hạn." });
        }
    }

    // Các class hỗ trợ nhận dữ liệu từ Javascript
    public class OtpRequest
    {
        public string Phone { get; set; }
    }

    public class VerifyRequest
    {
        public string Phone { get; set; }
        public string Otp { get; set; }
    }
}