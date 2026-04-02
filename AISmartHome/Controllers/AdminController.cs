using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Models;
using AISmartHome.Data;
using Microsoft.AspNetCore.Http; // Thêm thư viện này để dùng Session

namespace AISmartHome.Controllers
{
    public class AdminController : Controller
    {
        private readonly AISmartHomeDbContext _context;

        public AdminController(AISmartHomeDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // 1. HÀM XỬ LÝ KHI BẤM NÚT "ĐĂNG NHẬP" TỪ MODAL
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> Login(string Username, string Password)
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                TempData["LoginError"] = "Vui lòng nhập đầy đủ tài khoản và mật khẩu.";
                TempData["ShowLoginModal"] = "true";
                return Redirect(Request.Headers["Referer"].ToString());
            }

            string cleanUsername = Username.Trim();
            string cleanPassword = Password.Trim();

            // TÌM TÀI KHOẢN (Bỏ điều kiện MaVaiTro == 1 để tìm được cả Nhân viên)
            var account = await _context.TaiKhoans.FirstOrDefaultAsync(t =>
                t.TenDangNhap == cleanUsername &&
                t.TrangThai == true
            );

            if (account != null && account.MatKhau != null && account.MatKhau.Trim() == cleanPassword)
            {
                // ĐĂNG NHẬP THÀNH CÔNG -> Lưu thông tin chung
                HttpContext.Session.SetString("Username", account.TenDangNhap);

                // PHÂN LUỒNG DỰA VÀO VAI TRÒ
                if (account.MaVaiTro == 1)
                {
                    // 1. Dành cho Admin
                    HttpContext.Session.SetString("Role", "Admin");
                    return RedirectToAction("Index", "Admin");
                }
                else if (account.MaVaiTro == 2)
                {
                    // 2. Dành cho Nhân viên
                    HttpContext.Session.SetString("Role", "Employee");
                    return RedirectToAction("Index", "Employee"); // Sẽ tạo Controller này ở Bước 3
                }
                else
                {
                    TempData["LoginError"] = "Tài khoản không được phân quyền hợp lệ!";
                    TempData["ShowLoginModal"] = "true";
                    return Redirect(Request.Headers["Referer"].ToString());
                }
            }
            else
            {
                TempData["LoginError"] = "Tài khoản hoặc mật khẩu không chính xác!";
                TempData["ShowLoginModal"] = "true";
                return Redirect(Request.Headers["Referer"].ToString());
            }
        }

        // =========================================================
        // 2. HÀM INDEX ĐÃ ĐƯỢC BẢO VỆ (CODE CŨ CỦA BẠN GIỮ NGUYÊN)
        // =========================================================
        public async Task<IActionResult> Index()
        {
            // BƯỚC CHẶN: Kiểm tra xem có đúng là Admin không?
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") // Đổi chỗ này
            {
                TempData["LoginError"] = "Khu vực này chỉ dành cho Quản trị viên!";
                TempData["ShowLoginModal"] = "true";
                return RedirectToAction("Index", "Home");
            }

            // === CODE THỐNG KÊ GIỮ NGUYÊN ===
            ViewBag.TotalRevenue = await _context.DonHangs.SumAsync(d => d.TongTien);
            ViewBag.OrderCount = await _context.DonHangs.CountAsync();
            ViewBag.ProductCount = await _context.SanPhams.CountAsync();

            var pendingInstalls = await _context.YeuCauLapDats
                                        .Where(y => y.TrangThaiLapDat == "Chưa lắp đặt")
                                        .Take(5).ToListAsync();
            return View(pendingInstalls);
        }

        // =========================================================
        // 3. HÀM ĐĂNG XUẤT 
        // =========================================================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // Xóa sạch dữ liệu đăng nhập
            return RedirectToAction("Index", "Home"); // Trở về trang chủ
        }
    }
}