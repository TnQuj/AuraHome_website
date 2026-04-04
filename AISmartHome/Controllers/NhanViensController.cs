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
    public class NhanViensController : Controller
    {
        private readonly AISmartHomeDbContext _context;

        public NhanViensController(AISmartHomeDbContext context)
        {
            _context = context;
        }

        // GET: NhanViens
        public async Task<IActionResult> Index()
        {
            var aISmartHomeDbContext = _context.NhanViens.Include(n => n.MaTaiKhoanNavigation);
            return View(await aISmartHomeDbContext.ToListAsync());
        }

        // GET: NhanViens/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var nhanVien = await _context.NhanViens
                .Include(n => n.MaTaiKhoanNavigation)
                .FirstOrDefaultAsync(m => m.MaNhanVien == id);
            if (nhanVien == null)
            {
                return NotFound();
            }

            return View(nhanVien);
        }

        // GET: NhanViens/Create
        public IActionResult Create()
        {
            ViewData["MaTaiKhoan"] = new SelectList(_context.TaiKhoans, "MaTaiKhoan", "MaTaiKhoan");
            return View();
        }

        // POST: NhanViens/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NhanVienViewModel model)
        {
            if (ModelState.IsValid)
            {
                // 1. Kiểm tra xem Tên đăng nhập có bị trùng với ai chưa
                bool isExist = await _context.TaiKhoans.AnyAsync(t => t.TenDangNhap == model.TenDangNhap);
                if (isExist)
                {
                    ModelState.AddModelError("TenDangNhap", "Tên đăng nhập này đã tồn tại. Vui lòng chọn tên khác!");
                    return View(model);
                }

                try
                {
                    // 2. Tìm ID của quyền "Nhân Viên" (Giả sử trong DB bạn đặt là Employee)
                    var role = await _context.VaiTros.FirstOrDefaultAsync(v => v.TenVaiTro == "Employee" || v.TenVaiTro == "Nhân Viên");
                    int roleId = role != null ? role.MaVaiTro : 2; // Mặc định là 2 nếu không tìm thấy

                    // 3. TẠO TÀI KHOẢN TRƯỚC
                    var taiKhoanMoi = new TaiKhoan
                    {
                        TenDangNhap = model.TenDangNhap,
                        MatKhau = model.MatKhau,
                        MaVaiTro = roleId,
                        TrangThai = true // Kích hoạt ngay lập tức
                    };

                    _context.TaiKhoans.Add(taiKhoanMoi);
                    await _context.SaveChangesAsync(); // Lưu để sinh ra MaTaiKhoan tự động

                    // 4. TẠO NHÂN VIÊN VÀ KẾT NỐI VỚI TÀI KHOẢN VỪA TẠO
                    var nhanVienMoi = new NhanVien
                    {
                        TenNhanVien = model.TenNhanVien,
                        SoDienThoai = model.SoDienThoai,
                        DiaChi = model.DiaChi,
                        MaTaiKhoan = taiKhoanMoi.MaTaiKhoan // Lấy ID vừa sinh ra ở bước 3 gắn vào đây
                    };

                    _context.NhanViens.Add(nhanVienMoi);
                    await _context.SaveChangesAsync();

                    // Xong xuôi thì đưa Admin về lại trang Danh sách
                    TempData["SuccessMessage"] = "Thêm nhân viên và cấp tài khoản thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Có lỗi xảy ra khi lưu vào CSDL: " + ex.Message);
                }
            }

            // Nếu có lỗi (nhập thiếu ô), trả lại form kèm theo thông báo đỏ
            return View(model);
        }
        // GET: NhanViens/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var nhanVien = await _context.NhanViens.FindAsync(id);
            if (nhanVien == null)
            {
                return NotFound();
            }
            ViewData["MaTaiKhoan"] = new SelectList(_context.TaiKhoans, "MaTaiKhoan", "MaTaiKhoan", nhanVien.MaTaiKhoan);
            return View(nhanVien);
        }

        // POST: NhanViens/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaNhanVien,TenNhanVien,SoDienThoai,DiaChi,MaTaiKhoan")] NhanVien nhanVien)
        {
            if (id != nhanVien.MaNhanVien)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(nhanVien);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!NhanVienExists(nhanVien.MaNhanVien))
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
            ViewData["MaTaiKhoan"] = new SelectList(_context.TaiKhoans, "MaTaiKhoan", "MaTaiKhoan", nhanVien.MaTaiKhoan);
            return View(nhanVien);
        }

        // GET: NhanViens/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var nhanVien = await _context.NhanViens
                .Include(n => n.MaTaiKhoanNavigation)
                .FirstOrDefaultAsync(m => m.MaNhanVien == id);
            if (nhanVien == null)
            {
                return NotFound();
            }

            return View(nhanVien);
        }

        // POST: NhanViens/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var nhanVien = await _context.NhanViens.FindAsync(id);
            if (nhanVien != null)
            {
                _context.NhanViens.Remove(nhanVien);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool NhanVienExists(int id)
        {
            return _context.NhanViens.Any(e => e.MaNhanVien == id);
        }
    }
}
