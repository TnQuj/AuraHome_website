using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Models;
using AISmartHome.Data;

namespace AISmartHome.Controllers
{
    public class CustomersController : Controller
    {
        private readonly AISmartHomeDbContext _context;

        public CustomersController(AISmartHomeDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new HomeViewModel
            {
                SanPhams = await _context.SanPhams.ToListAsync(),
                DanhMucs = await _context.DanhMucSanPhams.ToListAsync()
            };

            return View(viewModel);
        }


        public async Task<IActionResult> Categories()
        {
            var viewModel = new HomeViewModel
            {
                SanPhams = await _context.SanPhams.ToListAsync(),
                DanhMucs = await _context.DanhMucSanPhams.ToListAsync()
            };

            return View(viewModel);
        }
    }
}