using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Models;
using AISmartHome.Data;
using Microsoft.AspNetCore.Http;

namespace AISmartHome.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly AISmartHomeDbContext _context;

        public EmployeeController(AISmartHomeDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // BƯỚC CHẶN: Chỉ cho phép Nhân viên (hoặc Admin) vào xem
            var role = HttpContext.Session.GetString("Role");
            if (role != "Employee" && role != "Admin")
            {
                TempData["LoginError"] = "Bạn cần đăng nhập bằng tài khoản Nhân viên!";
                TempData["ShowLoginModal"] = "true";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Username = HttpContext.Session.GetString("Username");

            // Lấy danh sách tất cả yêu cầu chưa lắp đặt cho nhân viên đi làm
            var installRequests = await _context.YeuCauLapDats
                                        .Where(y => y.TrangThaiLapDat != "Đã hoàn thành")
                                        .OrderByDescending(y => y.NgayLap)
                                        .ToListAsync();

            return View(installRequests);
        }

        // Dùng chung hàm Logout để nhân viên cũng thoát ra được
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}