using System.Diagnostics;
using AISmartHome.Data;
using AISmartHome.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
         private readonly AISmartHomeDbContext _context;

        public HomeController(ILogger<HomeController> logger, AISmartHomeDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            
            var viewModel = new HomeViewModel
            {
                SanPhams = await _context.SanPhams.OrderByDescending(s => s.MaSanPham).Take(8).ToListAsync(),
                DanhMucs = await _context.DanhMucSanPhams.ToListAsync()
            };

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
