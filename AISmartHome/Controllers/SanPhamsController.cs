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
            if (id == null) return NotFound();

            var sanPham = await _context.SanPhams
                .Include(s => s.MaDanhMucNavigation)
                .Include(s => s.HinhAnhSanPhams) // BẮT BUỘC PHẢI CÓ DÒNG NÀY THÌ MỚI HIỆN ẢNH PHỤ
                .FirstOrDefaultAsync(m => m.MaSanPham == id);

            if (sanPham == null) return NotFound();

            return View(sanPham);
        }

        // GET: SanPhams/Create
        public IActionResult Create()
        {
            ViewData["MaDanhMuc"] = new SelectList(_context.DanhMucSanPhams, "MaDanhMuc", "TenDanhMuc");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Lưu ý: Mình đã xóa biến List<IFormFile> ở tham số đi, chỉ giữ lại ảnh chính
        public async Task<IActionResult> Create([Bind("MaSanPham,TenSanPham,GiaBan,MoTa,SoLuong,MaDanhMuc")] SanPham sanPham,
                                        IFormFile HinhAnhUpload)
        {
            ModelState.Remove("HinhAnh");
            ModelState.Remove("HinhAnhSanPhams");
            ModelState.Remove("MaDanhMucNavigation");

            if (ModelState.IsValid)
            {
                string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img");
                if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                // 1. LƯU ẢNH CHÍNH
                if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                {
                    string tenFileChinh = Guid.NewGuid().ToString() + Path.GetExtension(HinhAnhUpload.FileName);
                    string duongDanChinh = Path.Combine(uploadPath, tenFileChinh);

                    using (var stream = new FileStream(duongDanChinh, FileMode.Create))
                    {
                        await HinhAnhUpload.CopyToAsync(stream);
                    }
                    sanPham.HinhAnh = tenFileChinh;
                }

                // 2. LƯU SẢN PHẨM VÀO DB ĐỂ CÓ ID
                _context.Add(sanPham);
                await _context.SaveChangesAsync();

                // =========================================================
                // 3. CÁCH LẤY ẢNH PHỤ SIÊU CHẮC CHẮN (THÒ TAY TRỰC TIẾP VÀO REQUEST)
                // =========================================================
                var hinhAnhChiTietFiles = HttpContext.Request.Form.Files.GetFiles("HinhAnhChiTiet");

                if (hinhAnhChiTietFiles != null && hinhAnhChiTietFiles.Count > 0)
                {
                    foreach (var file in hinhAnhChiTietFiles)
                    {
                        if (file.Length > 0)
                        {
                            string tenFilePhu = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                            string duongDanPhu = Path.Combine(uploadPath, tenFilePhu);

                            using (var stream = new FileStream(duongDanPhu, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            var hinhAnhPhu = new HinhAnhSanPham
                            {
                                UrlHinhAnh = tenFilePhu,
                                MaSanPham = sanPham.MaSanPham
                            };
                            _context.HinhAnhSanPhams.Add(hinhAnhPhu);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["MaDanhMuc"] = new SelectList(_context.DanhMucSanPhams, "MaDanhMuc", "TenDanhMuc", sanPham.MaDanhMuc);
            return View(sanPham);
        }

        // GET: SanPhams/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var sanPham = await _context.SanPhams
                .Include(s => s.HinhAnhSanPhams) // Phải kéo ảnh phụ ra thì giao diện Edit mới thấy
                .FirstOrDefaultAsync(m => m.MaSanPham == id);

            if (sanPham == null) return NotFound();

            ViewData["MaDanhMuc"] = new SelectList(_context.DanhMucSanPhams, "MaDanhMuc", "TenDanhMuc", sanPham.MaDanhMuc);
            return View(sanPham);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaSanPham,TenSanPham,GiaBan,MoTa,SoLuong,MaDanhMuc,HinhAnh")] SanPham sanPham,
                              IFormFile? HinhAnhUpload, string? IdsToXoa) // Thêm IdsToXoa ở đây
        {
            if (id != sanPham.MaSanPham) return NotFound();

            ModelState.Remove("HinhAnh");
            ModelState.Remove("HinhAnhSanPhams");
            ModelState.Remove("MaDanhMucNavigation");

            if (ModelState.IsValid)
            {
                try
                {
                    string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img");

                    // --- BỔ SUNG: XỬ LÝ XÓA ẢNH CHI TIẾT ĐÃ CHỌN ---
                    if (!string.IsNullOrEmpty(IdsToXoa))
                    {
                        // Chuyển chuỗi "1,2,3" thành danh sách số nguyên
                        var ids = IdsToXoa.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(int.Parse).ToList();

                        foreach (var anhId in ids)
                        {
                            var anhXoa = await _context.HinhAnhSanPhams.FindAsync(anhId);
                            if (anhXoa != null)
                            {
                                // 1. Xóa file vật lý trong thư mục wwwroot/img
                                string filePath = Path.Combine(uploadPath, anhXoa.UrlHinhAnh);
                                if (System.IO.File.Exists(filePath))
                                {
                                    System.IO.File.Delete(filePath);
                                }

                                // 2. Xóa bản ghi trong Database
                                _context.HinhAnhSanPhams.Remove(anhXoa);
                            }
                        }
                        // Lưu thay đổi xóa trước khi thực hiện các bước tiếp theo
                        await _context.SaveChangesAsync();
                    }
                    // ----------------------------------------------

                    // 1. UPDATE ẢNH CHÍNH
                    if (HinhAnhUpload != null && HinhAnhUpload.Length > 0)
                    {
                        string tenFileChinh = Guid.NewGuid().ToString() + Path.GetExtension(HinhAnhUpload.FileName);
                        string duongDanChinh = Path.Combine(uploadPath, tenFileChinh);

                        using (var stream = new FileStream(duongDanChinh, FileMode.Create))
                        {
                            await HinhAnhUpload.CopyToAsync(stream);
                        }
                        sanPham.HinhAnh = tenFileChinh;
                    }

                    // 2. UPDATE SẢN PHẨM
                    _context.Update(sanPham);
                    await _context.SaveChangesAsync();

                    // 3. CÁCH LẤY ẢNH PHỤ (Giữ nguyên logic của bạn)
                    var hinhAnhChiTietFiles = HttpContext.Request.Form.Files.GetFiles("HinhAnhChiTiet");
                    if (hinhAnhChiTietFiles != null && hinhAnhChiTietFiles.Count > 0)
                    {
                        foreach (var file in hinhAnhChiTietFiles)
                        {
                            if (file.Length > 0)
                            {
                                string tenFilePhu = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                                string duongDanPhu = Path.Combine(uploadPath, tenFilePhu);

                                using (var stream = new FileStream(duongDanPhu, FileMode.Create))
                                {
                                    await file.CopyToAsync(stream);
                                }

                                var hinhAnhPhu = new HinhAnhSanPham
                                {
                                    UrlHinhAnh = tenFilePhu,
                                    MaSanPham = sanPham.MaSanPham
                                };
                                _context.HinhAnhSanPhams.Add(hinhAnhPhu);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SanPhamExists(sanPham.MaSanPham)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["MaDanhMuc"] = new SelectList(_context.DanhMucSanPhams, "MaDanhMuc", "TenDanhMuc", sanPham.MaDanhMuc);
            return View(sanPham);
        }
        // GET: SanPhams/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var sanPham = await _context.SanPhams
                .Include(s => s.MaDanhMucNavigation)
                .Include(s => s.HinhAnhSanPhams) // Phải kéo theo ảnh phụ để View đếm số lượng
                .FirstOrDefaultAsync(m => m.MaSanPham == id);

            if (sanPham == null) return NotFound();

            return View(sanPham);
        }

        // POST: SanPhams/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // BẮT BUỘC: Phải include rổ ảnh phụ vào thì mới lấy được tên file để xóa ổ cứng
            var sanPham = await _context.SanPhams
                .Include(s => s.HinhAnhSanPhams)
                .FirstOrDefaultAsync(m => m.MaSanPham == id);

            if (sanPham != null)
            {
                string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img");

                // ===============================================
                // BƯỚC 1: XÓA FILE ẢNH CHÍNH KHỎI Ổ CỨNG
                // ===============================================
                if (!string.IsNullOrEmpty(sanPham.HinhAnh))
                {
                    string mainImgPath = Path.Combine(uploadPath, sanPham.HinhAnh);
                    if (System.IO.File.Exists(mainImgPath))
                    {
                        System.IO.File.Delete(mainImgPath); // Tiêu diệt file!
                    }
                }

                // ===============================================
                // BƯỚC 2: XÓA TẤT CẢ FILE ẢNH PHỤ KHỎI Ổ CỨNG
                // ===============================================
                if (sanPham.HinhAnhSanPhams != null && sanPham.HinhAnhSanPhams.Any())
                {
                    foreach (var anh in sanPham.HinhAnhSanPhams)
                    {
                        string subImgPath = Path.Combine(uploadPath, anh.UrlHinhAnh);
                        if (System.IO.File.Exists(subImgPath))
                        {
                            System.IO.File.Delete(subImgPath); // Tiêu diệt file phụ!
                        }
                    }
                }

                // ===============================================
                // BƯỚC 3: XÓA DATA KHỎI DATABASE
                // ===============================================
                // Nhờ SQL có lệnh "ON DELETE CASCADE" bạn đã setup lúc đầu, xóa Sản phẩm 
                // sẽ tự động xóa sạch các dòng dữ liệu trong bảng HinhAnhSanPham. Rất nhàn!
                _context.SanPhams.Remove(sanPham);
                await _context.SaveChangesAsync();
            }

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

                        foreach (var row in rows.Skip(1)) // Bỏ qua dòng tiêu đề
                        {
                            // Cột 2 (B) là Tên Sản Phẩm
                            string tenSP = row.Cell(2).Value.ToString().Trim();
                            if (string.IsNullOrWhiteSpace(tenSP)) continue;

                            var sanPham = new SanPham
                            {
                                TenSanPham = tenSP,

                                // Cột 3 (C) là Giá Bán
                                GiaBan = decimal.TryParse(row.Cell(3).Value.ToString(), out decimal gia) ? gia : 0,

                                // Cột 4 (D) là Mô Tả
                                MoTa = row.Cell(4).Value.ToString(),

                                // Cột 5 (E) là Hình Ảnh
                                HinhAnh = !string.IsNullOrWhiteSpace(row.Cell(5).Value.ToString())
                                            ? row.Cell(5).Value.ToString()
                                            : "default-product.png",

                                // Cột 6 (F) là Số Lượng
                                SoLuong = int.TryParse(row.Cell(6).Value.ToString(), out int sl) ? sl : 0,

                                // Cột 7 (G) là Mã Danh Mục
                                MaDanhMuc = int.TryParse(row.Cell(7).Value.ToString(), out int maDM) ? maDM : null
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