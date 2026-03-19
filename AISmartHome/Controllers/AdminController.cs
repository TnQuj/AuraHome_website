using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Models;
using AISmartHome.Data;

namespace AISmartHome.Controllers
{
    public class AdminController : Controller
    {
        private readonly AISmartHomeDbContext _context;

        public AdminController(AISmartHomeDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalRevenue = await _context.DonHangs.SumAsync(d => d.TongTien);
            ViewBag.OrderCount = await _context.DonHangs.CountAsync();
            ViewBag.ProductCount = await _context.SanPhams.CountAsync();

            var pendingInstalls = await _context.YeuCauLapDats
                                        .Where(y => y.TrangThaiLapDat == "Chưa lắp đặt")
                                        .Take(5).ToListAsync();
            return View(pendingInstalls);
        }

        public async Task<IActionResult> ManageProducts()
        {
            var products = await _context.SanPhams.Include(s => s.MaDanhMucNavigation).ToListAsync();
            return View("ManageProducts", products);
        }

        public async Task<IActionResult> ManageOrders()
        {
            var orders = await _context.DonHangs.OrderByDescending(o => o.NgayDatHang).ToListAsync();
            return View("ManageOrders", orders);
        }

        public async Task<IActionResult> ManageEmployees()
        {
            var employees = await _context.NhanViens.ToListAsync();
            return View("ManageEmployees", employees);
        }
    }
}