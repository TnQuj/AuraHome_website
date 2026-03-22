using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Data;
using AISmartHome.Models;
using AISmartHome.Services;

namespace AISmartHome.Controllers
{
    public class BaiVietsController : Controller
    {
        private readonly AISmartHomeDbContext _context;
        private readonly TaoTinTuDong _taoTinService;

        public BaiVietsController(AISmartHomeDbContext context, TaoTinTuDong taoTinService)
        {
            _context = context;
            _taoTinService = taoTinService;
        }

        // GET: BaiViets
        public async Task<IActionResult> Index()
        {
            var aISmartHomeDbContext = _context.BaiViets.Include(b => b.MaDanhMucBaiVietNavigation).Include(b => b.MaTaiKhoanNavigation);
            return View(await aISmartHomeDbContext.ToListAsync());
        }

        // GET: BaiViets/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var baiViet = await _context.BaiViets
                .Include(b => b.MaDanhMucBaiVietNavigation)
                .Include(b => b.MaTaiKhoanNavigation)
                .FirstOrDefaultAsync(m => m.MaBaiViet == id);
            if (baiViet == null)
            {
                return NotFound();
            }

            return View(baiViet);
        }

        // GET: BaiViets/Create
        public IActionResult Create()
        {
            ViewData["MaDanhMucBaiViet"] = new SelectList(_context.DanhMucBaiViets, "MaDanhMucBaiViet", "MaDanhMucBaiViet");
            ViewData["MaTaiKhoan"] = new SelectList(_context.TaiKhoans, "MaTaiKhoan", "MaTaiKhoan");
            return View();
        }

        // POST: BaiViets/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaBaiViet,TieuDe,NoiDung,HinhAnh,NgayDang,MaDanhMucBaiViet,MaTaiKhoan")] BaiViet baiViet)
        {
            if (ModelState.IsValid)
            {
                _context.Add(baiViet);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["MaDanhMucBaiViet"] = new SelectList(_context.DanhMucBaiViets, "MaDanhMucBaiViet", "MaDanhMucBaiViet", baiViet.MaDanhMucBaiViet);
            ViewData["MaTaiKhoan"] = new SelectList(_context.TaiKhoans, "MaTaiKhoan", "MaTaiKhoan", baiViet.MaTaiKhoan);
            return View(baiViet);
        }

        // GET: BaiViets/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var baiViet = await _context.BaiViets.FindAsync(id);
            if (baiViet == null)
            {
                return NotFound();
            }
            ViewData["MaDanhMucBaiViet"] = new SelectList(_context.DanhMucBaiViets, "MaDanhMucBaiViet", "MaDanhMucBaiViet", baiViet.MaDanhMucBaiViet);
            ViewData["MaTaiKhoan"] = new SelectList(_context.TaiKhoans, "MaTaiKhoan", "MaTaiKhoan", baiViet.MaTaiKhoan);
            return View(baiViet);
        }

        // POST: BaiViets/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaBaiViet,TieuDe,NoiDung,HinhAnh,NgayDang,MaDanhMucBaiViet,MaTaiKhoan")] BaiViet baiViet)
        {
            if (id != baiViet.MaBaiViet)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(baiViet);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BaiVietExists(baiViet.MaBaiViet))
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
            ViewData["MaDanhMucBaiViet"] = new SelectList(_context.DanhMucBaiViets, "MaDanhMucBaiViet", "MaDanhMucBaiViet", baiViet.MaDanhMucBaiViet);
            ViewData["MaTaiKhoan"] = new SelectList(_context.TaiKhoans, "MaTaiKhoan", "MaTaiKhoan", baiViet.MaTaiKhoan);
            return View(baiViet);
        }

        // GET: BaiViets/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var baiViet = await _context.BaiViets
                .Include(b => b.MaDanhMucBaiVietNavigation)
                .Include(b => b.MaTaiKhoanNavigation)
                .FirstOrDefaultAsync(m => m.MaBaiViet == id);
            if (baiViet == null)
            {
                return NotFound();
            }

            return View(baiViet);
        }

        // POST: BaiViets/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var baiViet = await _context.BaiViets.FindAsync(id);
            if (baiViet != null)
            {
                _context.BaiViets.Remove(baiViet);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var baiViet = await _context.BaiViets.FindAsync(id);
            if (baiViet == null) return NotFound();

            baiViet.IsApproved = true;
            baiViet.NgayDang = DateTime.Now; // Cập nhật lại ngày đăng là lúc duyệt

            _context.Update(baiViet);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool BaiVietExists(int id)
        {
            return _context.BaiViets.Any(e => e.MaBaiViet == id);
        }

        [HttpPost]
        public async Task<IActionResult> CrawlNews()
        {
            try
            {
                var newsList = await _taoTinService.LayTinTuRssAsync("https://vnexpress.net/rss/so-hoa.rss");

                if (newsList == null || !newsList.Any())
                    return Json(new { success = false, message = "Không lấy được tin từ RSS" });

                int count = 0;
                foreach (var news in newsList)
                {
                    // Kiểm tra tiêu đề chính xác
                    bool isExist = await _context.BaiViets.AnyAsync(b => b.TieuDe == news.TieuDe);

                    if (!isExist)
                    {
                        // Gán đúng ID bạn đã thấy trong SQL (MaDanhMuc=1, MaTaiKhoan=1)
                        news.MaDanhMucBaiViet = 1;
                        news.MaTaiKhoan = 1;
                        news.IsApproved = false; // ĐỂ FALSE ĐỂ NÓ HIỆN NÚT DUYỆT
                        news.NgayDang = DateTime.Now;

                        // Nếu HinhAnh bị NULL trong DB, EF sẽ chặn lưu. Hãy gán tạm:
                        if (string.IsNullOrEmpty(news.HinhAnh)) news.HinhAnh = "news-default.jpg";

                        _context.BaiViets.Add(news);
                        count++;
                    }
                }

                if (count > 0)
                {
                    // Lệnh quan trọng nhất
                    int rowsAffected = await _context.SaveChangesAsync();
                    return Json(new { success = true, message = $"Đã lưu thành công {count} bài vào Database!" });
                }

                return Json(new { success = true, message = "Không có tin mới để thêm." });
            }
            catch (Exception ex)
            {
                // Trả về lỗi chi tiết để chúng ta biết tại sao SQL từ chối lưu
                var errorDetail = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi SQL: " + errorDetail });
            }
        }

    }
}
