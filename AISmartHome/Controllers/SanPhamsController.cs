using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Data;
using AISmartHome.Models;
using System.IO;
using ClosedXML.Excel;
namespace AISmartHome.Controllers
{
    public class SanPhamsController : Controller
    {
        private readonly AISmartHomeDbContext _context;

        public SanPhamsController(AISmartHomeDbContext context)
        {
            _context = context;
        }

        // GET: SanPhams
        public async Task<IActionResult> Index()
        {
            var aISmartHomeDbContext = _context.SanPhams.Include(s => s.MaDanhMucNavigation);
            return View(await aISmartHomeDbContext.ToListAsync());
        }

        // GET: SanPhams/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sanPham = await _context.SanPhams
                .Include(s => s.MaDanhMucNavigation)
                .FirstOrDefaultAsync(m => m.MaSanPham == id);
            if (sanPham == null)
            {
                return NotFound();
            }

            return View(sanPham);
        }

        // GET: SanPhams/Create
        public IActionResult Create()
        {
            ViewData["MaDanhMuc"] = new SelectList(_context.DanhMucSanPhams, "MaDanhMuc", "TenDanhMuc");
            return View();
        }

        // POST: SanPhams/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        // BƯỚC 1: Đã xóa "HinhAnh" khỏi [Bind]
        public async Task<IActionResult> Create([Bind("MaSanPham,TenSanPham,GiaBan,MoTa,SoLuong,MaDanhMuc")] SanPham sanPham, IFormFile? HinhAnhUpload)
        {
            // BƯỚC 2: Xóa bỏ kiểm tra (Validation) tự động của hệ thống đối với các trường này
            ModelState.Remove("HinhAnh");
            ModelState.Remove("MaDanhMucNavigation");
            ModelState.Remove("ChiTietDonHangs");
            ModelState.Remove("ChiTietGioHangs");
            ModelState.Remove("HuongDanSuDungs");

            if (ModelState.IsValid)
            {
                // KỊCH BẢN 1: NGƯỜI DÙNG CÓ CHỌN ẢNH
                if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(HinhAnhUpload.FileName);
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string filePath = Path.Combine(uploadsFolder, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await HinhAnhUpload.CopyToAsync(stream);
                    }

                    // Gắn tên file thật vào sản phẩm
                    sanPham.HinhAnh = fileName;
                }
                // KỊCH BẢN 2: NGƯỜI DÙNG KHÔNG CHỌN ẢNH (GIẢI QUYẾT LỖI NULL Ở ĐÂY)
                else
                {
                    // Gán một giá trị chuỗi hợp lệ để Database không báo lỗi Null
                    // Lời khuyên: Bạn nên copy 1 bức ảnh logo hoặc ảnh trống đặt tên là "no-image.png" để vào thư mục wwwroot/img/
                    sanPham.HinhAnh = "no-image.png";
                }

                try
                {
                    _context.Add(sanPham);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index)); // Xong thì về trang chủ
                }
                catch (Exception ex)
                {
                    // Nếu Database vẫn cự tuyệt, lỗi sẽ hiện ra rõ ràng để ta biết
                    ModelState.AddModelError("", "Lỗi khi lưu vào CSDL: " + ex.Message);
                }
            }

            // Nếu code chạy đến đây nghĩa là có lỗi nhập liệu (ví dụ để trống Tên Sản Phẩm)
            // Cần load lại dropdown Danh Mục để giao diện không bị sập
            ViewData["MaDanhMuc"] = new SelectList(_context.DanhMucSanPhams, "MaDanhMuc", "TenDanhMuc", sanPham.MaDanhMuc);
            return View(sanPham);
        }

        // GET: SanPhams/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sanPham = await _context.SanPhams.FindAsync(id);
            if (sanPham == null)
            {
                return NotFound();
            }
            ViewData["MaDanhMuc"] = new SelectList(_context.DanhMucSanPhams, "MaDanhMuc", "TenDanhMuc", sanPham.MaDanhMuc);
            return View(sanPham);
        }

        // POST: SanPhams/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        // BƯỚC 1: Thêm tham số IFormFile HinhAnhUpload vào hàm Edit
        public async Task<IActionResult> Edit(int id, [Bind("MaSanPham,TenSanPham,GiaBan,MoTa,HinhAnh,SoLuong,MaDanhMuc")] SanPham sanPham, IFormFile? HinhAnhUpload)
        {
            if (id != sanPham.MaSanPham)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // BƯỚC 2: Xử lý file ảnh được upload
                    if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                    {
                        // Tạo tên file ngẫu nhiên (dùng Guid) để không bao giờ bị trùng tên làm lỗi ảnh cũ
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(HinhAnhUpload.FileName);

                        // Đường dẫn lưu file vào thư mục wwwroot/images/
                        string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", fileName);

                        // Copy file từ bộ nhớ tạm vào ổ cứng server
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await HinhAnhUpload.CopyToAsync(stream);
                        }

                        // Cập nhật tên ảnh mới vào Model để lưu xuống Database
                        sanPham.HinhAnh = fileName;
                    }
                    // LƯU Ý: Nếu HinhAnhUpload == null, hệ thống sẽ tự động dùng lại tên ảnh cũ 
                    // được gửi lên từ thẻ <input type="hidden" asp-for="HinhAnh" /> ở Giao diện

                    _context.Update(sanPham);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SanPhamExists(sanPham.MaSanPham)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index)); // Quay lại trang danh sách
            }

            // Nếu có lỗi nhập liệu, load lại SelectList danh mục
            ViewData["MaDanhMuc"] = new SelectList(_context.DanhMucSanPhams, "MaDanhMuc", "TenDanhMuc", sanPham.MaDanhMuc);
            return View(sanPham);
        }

        // GET: SanPhams/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sanPham = await _context.SanPhams
                .Include(s => s.MaDanhMucNavigation)
                .FirstOrDefaultAsync(m => m.MaSanPham == id);
            if (sanPham == null)
            {
                return NotFound();
            }

            return View(sanPham);
        }

        // POST: SanPhams/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var sanPham = await _context.SanPhams.FindAsync(id);
            if (sanPham != null)
            {
                _context.SanPhams.Remove(sanPham);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: SanPhams/ImportExcel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportExcel(IFormFile? fileExcel)
        {
            if (fileExcel == null || fileExcel.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn một file Excel hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            if (!fileExcel.FileName.EndsWith(".xlsx") && !fileExcel.FileName.EndsWith(".xls"))
            {
                TempData["Error"] = "Chỉ hỗ trợ định dạng .xlsx hoặc .xls";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using (var stream = new MemoryStream())
                {
                    await fileExcel.CopyToAsync(stream);
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var rows = worksheet.RangeUsed().RowsUsed();
                        int count = 0;

                        foreach (var row in rows.Skip(1))
                        {
                            string tenSP = row.Cell(1).Value.ToString();
                            if (string.IsNullOrWhiteSpace(tenSP)) continue;

                            var sanPham = new SanPham
                            {
                                TenSanPham = tenSP,
                                GiaBan = decimal.TryParse(row.Cell(2).Value.ToString(), out decimal gia) ? gia : 0,
                                SoLuong = int.TryParse(row.Cell(3).Value.ToString(), out int sl) ? sl : 0,
                                MoTa = row.Cell(4).Value.ToString(),
                                HinhAnh = !string.IsNullOrWhiteSpace(row.Cell(5).Value.ToString())
                                            ? row.Cell(5).Value.ToString()
                                            : "default-product.png",
                                MaDanhMuc = int.TryParse(row.Cell(6).Value.ToString(), out int maDM) ? maDM : null
                            };

                            _context.SanPhams.Add(sanPham);
                            count++;
                        }

                        await _context.SaveChangesAsync();
                        TempData["Success"] = $"Đã nhập thành công {count} sản phẩm từ file Excel!";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi đọc file Excel: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
        private bool SanPhamExists(int id)
        {
            return _context.SanPhams.Any(e => e.MaSanPham == id);
        }
    }
}
