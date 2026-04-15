using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Models;
using AISmartHome.Data;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Linq;

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
        // 1. HIỂN THỊ FORM ĐĂNG NHẬP
        // =========================================================
        [HttpGet]
        public IActionResult Login()
        {
            // Kiểm tra: Nếu đã đăng nhập rồi thì đẩy thẳng vào hệ thống, không bắt đăng nhập lại
            var role = HttpContext.Session.GetString("Role");
            if (role == "Admin") return RedirectToAction("Index", "Admin");
            if (role == "Employee") return RedirectToAction("Index", "Employee"); // Nếu bạn có file EmployeeController

            return View(); // Trả về file Login.cshtml mà chúng ta làm ở bước trước
        }

        // =========================================================
        // 2. XỬ LÝ LOGIC KHI BẤM "ĐĂNG NHẬP HỆ THỐNG"
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string Username, string Password)
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ tài khoản và mật khẩu.";
                return View();
            }

            string cleanUsername = Username.Trim();
            string cleanPassword = Password.Trim();

            // Tìm tài khoản trong DB
            var account = await _context.TaiKhoans.FirstOrDefaultAsync(t =>
                t.TenDangNhap == cleanUsername &&
                t.TrangThai == true
            );

            // Kiểm tra mật khẩu (Thực tế nên dùng mã hóa như MD5 hoặc Bcrypt)
            if (account != null && account.MatKhau != null && account.MatKhau.Trim() == cleanPassword)
            {
                // LƯU SESSION ĐĂNG NHẬP
                HttpContext.Session.SetString("Username", account.TenDangNhap);
                HttpContext.Session.SetInt32("AccountId", account.MaTaiKhoan);

                // PHÂN LUỒNG QUYỀN HẠN
                if (account.MaVaiTro == 1) // 1 = Admin
                {
                    HttpContext.Session.SetString("Role", "Admin");
                    return RedirectToAction("Index", "Admin");
                }
                else if (account.MaVaiTro == 2) // 2 = Nhân viên (Kỹ thuật/Bán hàng)
                {
                    HttpContext.Session.SetString("Role", "Employee");
                    return RedirectToAction("Index", "Employee");
                }
                else
                {
                    ViewBag.Error = "Tài khoản không được phân quyền hợp lệ!";
                    return View();
                }
            }
            else
            {
                ViewBag.Error = "Tài khoản hoặc mật khẩu không chính xác!";
                return View();
            }
        }

        // =========================================================
        // 3. TRANG TỔNG QUAN ADMIN (ĐÃ ĐƯỢC BẢO VỆ CHẶT CHẼ)
        // =========================================================
        public async Task<IActionResult> Index()
        {
            // BƯỚC CHẶN: Không phải Admin thì đuổi ra trang Đăng nhập ngay lập tức!
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Login", "Admin");
            }

            // === LẤY DỮ LIỆU THỐNG KÊ (Giữ nguyên của bạn) ===
            ViewBag.TotalRevenue = await _context.DonHangs.SumAsync(d => d.TongTien);
            ViewBag.OrderCount = await _context.DonHangs.CountAsync();
            ViewBag.ProductCount = await _context.SanPhams.CountAsync();

            var pendingInstalls = await _context.YeuCauLapDats
                                        .Where(y => y.TrangThaiLapDat == "Chưa lắp đặt")
                                        .Take(5).ToListAsync();
            return View(pendingInstalls);
        }

        // =========================================================
        // 4. HÀM ĐĂNG XUẤT 
        // =========================================================
        public IActionResult Logout()
        {
            // Chỉ xóa thông tin của nhân viên/admin, tránh xóa nhầm giỏ hàng của khách nếu test chung trình duyệt
            HttpContext.Session.Remove("Username");
            HttpContext.Session.Remove("AccountId");
            HttpContext.Session.Remove("Role");

            return RedirectToAction("Login", "Admin");
        }

        public IActionResult ExitToWebsite()
        {
            // 1. Xóa sạch mọi quyền lực của Admin/Nhân viên
            HttpContext.Session.Remove("Username");
            HttpContext.Session.Remove("AccountId");
            HttpContext.Session.Remove("Role");

            // 2. Chuyển hướng về trang chủ của khách hàng (Home/Index)
            return RedirectToAction("Index", "Home");
        }
    }
}