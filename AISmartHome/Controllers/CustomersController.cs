using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Models;
using AISmartHome.Data;

namespace AISmartHome.Controllers
{
    // Tên class khớp hoàn toàn với file bạn vừa tạo
    public class CustomersController : Controller
    {
        private readonly AISmartHomeDbContext _context;

        public CustomersController(AISmartHomeDbContext context)
        {
            _context = context;
        }

        // Tên hàm là Index, nó sẽ tự động tìm file Views/Customers/Index.cshtml
        public async Task<IActionResult> Index()
        {
            var products = await _context.SanPhams.ToListAsync();
            return View(products);
        }

        public IActionResult GioHang()
        {
            return View();
        }

        public IActionResult TraCuuDonHang()
        {
            return View();
        }

        public IActionResult DangNhap()
        {
            return View();
        }
    }
}