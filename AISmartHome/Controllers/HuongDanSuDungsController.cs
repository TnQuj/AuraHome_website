using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Data;
using AISmartHome.Models;

namespace AISmartHome.Controllers
{
    public class HuongDanSuDungsController : Controller
    {
        private readonly AISmartHomeDbContext _context;

        public HuongDanSuDungsController(AISmartHomeDbContext context)
        {
            _context = context;
        }

        // GET: HuongDanSuDungs
        public async Task<IActionResult> Index()
        {
            var aISmartHomeDbContext = _context.HuongDanSuDungs.Include(h => h.MaSanPhamNavigation);
            return View(await aISmartHomeDbContext.ToListAsync());
        }

        // GET: HuongDanSuDungs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var huongDanSuDung = await _context.HuongDanSuDungs
                .Include(h => h.MaSanPhamNavigation)
                .FirstOrDefaultAsync(m => m.MaHuongDan == id);
            if (huongDanSuDung == null)
            {
                return NotFound();
            }

            return View(huongDanSuDung);
        }

        // GET: HuongDanSuDungs/Create
        public IActionResult Create()
        {
            ViewData["MaSanPham"] = new SelectList(_context.SanPhams, "MaSanPham", "MaSanPham");
            return View();
        }

        // POST: HuongDanSuDungs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaHuongDan,MaSanPham,TieuDe,NoiDung,VideoUrl")] HuongDanSuDung huongDanSuDung)
        {
            if (ModelState.IsValid)
            {
                _context.Add(huongDanSuDung);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["MaSanPham"] = new SelectList(_context.SanPhams, "MaSanPham", "MaSanPham", huongDanSuDung.MaSanPham);
            return View(huongDanSuDung);
        }

        // GET: HuongDanSuDungs/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var huongDanSuDung = await _context.HuongDanSuDungs.FindAsync(id);
            if (huongDanSuDung == null)
            {
                return NotFound();
            }
            ViewData["MaSanPham"] = new SelectList(_context.SanPhams, "MaSanPham", "MaSanPham", huongDanSuDung.MaSanPham);
            return View(huongDanSuDung);
        }

        // POST: HuongDanSuDungs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaHuongDan,MaSanPham,TieuDe,NoiDung,VideoUrl")] HuongDanSuDung huongDanSuDung)
        {
            if (id != huongDanSuDung.MaHuongDan)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(huongDanSuDung);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!HuongDanSuDungExists(huongDanSuDung.MaHuongDan))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["MaSanPham"] = new SelectList(_context.SanPhams, "MaSanPham", "MaSanPham", huongDanSuDung.MaSanPham);
            return View(huongDanSuDung);
        }

        // GET: HuongDanSuDungs/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var huongDanSuDung = await _context.HuongDanSuDungs
                .Include(h => h.MaSanPhamNavigation)
                .FirstOrDefaultAsync(m => m.MaHuongDan == id);
            if (huongDanSuDung == null)
            {
                return NotFound();
            }

            return View(huongDanSuDung);
        }

        // POST: HuongDanSuDungs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var huongDanSuDung = await _context.HuongDanSuDungs.FindAsync(id);
            if (huongDanSuDung != null)
            {
                _context.HuongDanSuDungs.Remove(huongDanSuDung);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool HuongDanSuDungExists(int id)
        {
            return _context.HuongDanSuDungs.Any(e => e.MaHuongDan == id);
        }
    }
}
