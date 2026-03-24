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

        public async Task<IActionResult> Index(int? category)
        {
            ViewBag.CurrentCategory = category;

            var viewModel = new HomeViewModel
            {
                DanhMucs = await _context.DanhMucSanPhams.ToListAsync()
            };

            if (category.HasValue)
            {
                viewModel.SanPhams = await _context.SanPhams
                    .Where(sp => sp.MaDanhMuc == category.Value)
                    .ToListAsync();
            }
            else
            {
                viewModel.SanPhams = await _context.SanPhams.ToListAsync();
            }

            return View(viewModel);
        }
    }
}