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
        private readonly IWebHostEnvironment _hostEnvironment;
        
        public BaiVietsController(AISmartHomeDbContext context, TaoTinTuDong taoTinService, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _taoTinService = taoTinService;
            _hostEnvironment = hostEnvironment;
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

        // 1. HÀM GET: Hiển thị giao diện chỉnh sửa
        // GET: BaiViets/Edit/5
        [HttpGet] // Thêm rõ ràng để tránh nhầm lẫn
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
            // Chỉnh sửa Text hiển thị trong SelectList để dễ nhìn hơn (tên danh mục thay vì ID)
            ViewData["MaDanhMucBaiViet"] = new SelectList(_context.DanhMucBaiViets, "MaDanhMucBaiViet", "TenDanhMuc", baiViet.MaDanhMucBaiViet);
            ViewData["MaTaiKhoan"] = new SelectList(_context.TaiKhoans, "MaTaiKhoan", "HoTen", baiViet.MaTaiKhoan);
            return View(baiViet);
        }

        // 2. HÀM POST: Xử lý lưu dữ liệu và Upload ảnh
        // POST: BaiViets/Edit/5
        // POST: BaiViets/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BaiViet baiViet, IFormFile HinhAnhUpload)
        {
            if (id != baiViet.MaBaiViet) return NotFound();

            // 1. Lấy bài viết cũ từ Database lên
            var baiVietDb = await _context.BaiViets.FindAsync(id);
            if (baiVietDb == null) return NotFound();

            // 2. QUAN TRỌNG: Xóa các lỗi Validation không cần thiết để tránh bị load lại trang
            ModelState.Remove("MaTaiKhoan"); // Form không gửi lên
            ModelState.Remove("MaTaiKhoanNavigation"); // Thuộc tính liên kết (nếu có)
            ModelState.Remove("MaDanhMucBaiVietNavigation"); // Thuộc tính liên kết (nếu có)

            // Nếu ô Ngày đăng trên Form bị bỏ trống, ta xóa lỗi và sẽ dùng lại ngày cũ
            if (baiViet.NgayDang == default(DateTime) || baiViet.NgayDang == null)
            {
                ModelState.Remove("NgayDang");
            }

            // 3. Kiểm tra xem dữ liệu còn lại (Tiêu đề, Nội dung) đã hợp lệ chưa
            if (ModelState.IsValid)
            {
                try
                {
                    // Cập nhật nội dung Text
                    baiVietDb.TieuDe = baiViet.TieuDe;
                    baiVietDb.NoiDung = baiViet.NoiDung;
                    baiVietDb.MaDanhMucBaiViet = baiViet.MaDanhMucBaiViet;

                    // Chỉ cập nhật ngày đăng nếu người dùng có chọn ngày mới trên form
                    if (baiViet.NgayDang != default(DateTime))
                    {
                        baiVietDb.NgayDang = baiViet.NgayDang;
                    }

                    // Xử lý Upload Ảnh mới (nếu người dùng có chọn)
                    if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                    {
                        string wwwRootPath = _hostEnvironment.WebRootPath;
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(HinhAnhUpload.FileName);
                        string path = Path.Combine(wwwRootPath, "img", fileName);

                        using (var fileStream = new FileStream(path, FileMode.Create))
                        {
                            await HinhAnhUpload.CopyToAsync(fileStream);
                        }
                        baiVietDb.HinhAnh = fileName; // Gán ảnh mới
                    }
                    // Nếu không chọn ảnh mới, baiVietDb.HinhAnh vẫn giữ nguyên giá trị cũ tự động

                    // Lưu vào Database
                    _context.Update(baiVietDb);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BaiVietExists(baiVietDb.MaBaiViet)) return NotFound();
                    else throw;
                }
            }

            // 4. Nếu vẫn bị load lại trang, đoạn code này sẽ giúp in ra lỗi thực sự trong cửa sổ Output (Debug) của Visual Studio
            foreach (var modelStateKey in ModelState.Keys)
            {
                var modelStateVal = ModelState[modelStateKey];
                foreach (var error in modelStateVal.Errors)
                {
                    System.Diagnostics.Debug.WriteLine($"LỖI VALIDATION - Thuộc tính: {modelStateKey}, Lỗi: {error.ErrorMessage}");
                }
            }

            // Load lại danh mục nếu lưu thất bại
            ViewData["MaDanhMucBaiViet"] = new SelectList(_context.DanhMucBaiViets, "MaDanhMucBaiViet", "TenDanhMuc", baiVietDb.MaDanhMucBaiViet);
            return View(baiVietDb);
        }
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

        // POST: BaiViets/Hide/5 (Nút Hạ Bài)
        [HttpPost]
        public async Task<IActionResult> Hide(int id)
        {
            var baiViet = await _context.BaiViets.FindAsync(id);
            if (baiViet != null)
            {
                baiViet.IsApproved = false; // Đổi trạng thái thành chưa duyệt (bị ẩn khỏi web)
                _context.Update(baiViet);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: BaiViets/DeleteConfirmed/5 (Nút Xóa nhanh)
        [HttpPost, ActionName("DeleteConfirmed")]
        public async Task<IActionResult> DeleteConfirmedPost(int id)
        {
            var baiViet = await _context.BaiViets.FindAsync(id);
            if (baiViet != null)
            {
                _context.BaiViets.Remove(baiViet);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

    }
}
