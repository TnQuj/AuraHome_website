using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Models;
using AISmartHome.Data;
using Microsoft.AspNetCore.Http; // Bắt buộc để dùng ISession
using System.Text.Json;          // Bắt buộc để dùng JsonSerializer

namespace AISmartHome.Controllers
{
    public class CustomersController : Controller
    {
        private readonly AISmartHomeDbContext _context;

        public CustomersController(AISmartHomeDbContext context)
        {
            _context = context;
        }

        // =======================================================
        // TRANG DANH SÁCH & TRANG CHỦ
        // =======================================================
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

        // =======================================================
        // TRANG CHI TIẾT SẢN PHẨM
        // =======================================================
        public async Task<IActionResult> Detail(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sanPham = await _context.SanPhams
                .Include(sp => sp.HinhAnhSanPhams)
                .FirstOrDefaultAsync(m => m.MaSanPham == id);

            if (sanPham == null)
            {
                return NotFound();
            }

            // Gửi thêm sản phẩm tương tự sang View (Tùy chọn)
            ViewBag.SanPhamsTuongTu = await _context.SanPhams
                .Where(x => x.MaDanhMuc == sanPham.MaDanhMuc && x.MaSanPham != id)
                .Take(4)
                .ToListAsync();

            return View(sanPham);
            // =======================================================
            // QUẢN LÝ GIỎ HÀNG (LƯU TRỰC TIẾP VÀO DATABASE)
            // =======================================================
        }
            // 1. Hàm hỗ trợ: Lấy Giỏ hàng của khách hàng (Nếu chưa có thì tự tạo mới)
        private async Task<GioHang> GetOrCreateCartAsync(int maKhachHang)
        {
            // Tìm giỏ hàng của khách này, Include luôn ChiTietGioHang và thông tin SanPham bên trong
            var cart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs)
                    .ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHang);

            // Nếu khách chưa có giỏ hàng nào trong CSDL, tạo giỏ hàng rỗng mới
            if (cart == null)
            {
                cart = new GioHang
                {
                    MaKhachHang = maKhachHang,
                    NgayTao = DateTime.Now,
                    TongTien = 0
                };
                _context.GioHangs.Add(cart);
                await _context.SaveChangesAsync();
            }
            return cart;
        }

        // 2. Hiển thị trang giỏ hàng
        public async Task<IActionResult> Cart()
        {
            int maKhachHangHienTai = 1; // TẠM THỜI GÁN CỨNG LÀ KHÁCH HÀNG SỐ 1
            var cart = await GetOrCreateCartAsync(maKhachHangHienTai);

            return View(cart); // Gửi nguyên đối tượng GioHang sang View
        }

        //3. Thêm hàm này vào dưới hàm AddToCart cũ
        [HttpPost]
        public async Task<IActionResult> AddToCartAjax(int id, int quantity = 1)
        {
            int maKhachHangHienTai = 1; // Tạm gán khách hàng số 1
            var cart = await GetOrCreateCartAsync(maKhachHangHienTai);
            var sp = await _context.SanPhams.FindAsync(id);

            if (sp == null) return Json(new { success = false, message = "Không tìm thấy sản phẩm" });

            // Thêm hoặc tăng số lượng
            var chiTiet = cart.ChiTietGioHangs.FirstOrDefault(c => c.MaSanPham == id);
            if (chiTiet != null)
            {
                chiTiet.SoLuong += quantity;
            }
            else
            {
                _context.ChiTietGioHangs.Add(new ChiTietGioHang
                {
                    MaGioHang = cart.MaGioHang,
                    MaSanPham = id,
                    SoLuong = quantity,
                    Gia = sp.GiaBan
                });
            }
            await _context.SaveChangesAsync();

            // Cập nhật tổng tiền
            cart.TongTien = cart.ChiTietGioHangs.Sum(c => c.SoLuong * c.Gia);
            _context.GioHangs.Update(cart);
            await _context.SaveChangesAsync();

            // Lấy lại dữ liệu giỏ hàng mới nhất để trả về cho Javascript
            var updatedCart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs).ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            // Đóng gói dữ liệu thành JSON
            var items = updatedCart.ChiTietGioHangs.Select(c => new {
                maSanPham = c.MaSanPham,
                tenSanPham = c.MaSanPhamNavigation.TenSanPham,
                hinhAnh = string.IsNullOrEmpty(c.MaSanPhamNavigation.HinhAnh) ? "https://via.placeholder.com/150" : $"/img/{c.MaSanPhamNavigation.HinhAnh}",
                soLuong = c.SoLuong,
                gia = c.Gia,
                thanhTien = c.SoLuong * c.Gia
            });

            return Json(new
            {
                success = true,
                totalItems = items.Sum(x => x.soLuong),
                totalPrice = updatedCart.TongTien,
                items = items
            });
        }

        // 4. Xóa sản phẩm khỏi giỏ
        [HttpPost]
        public async Task<IActionResult> RemoveFromCartAjax(int id)
        {
            int maKhachHangHienTai = 1;
            var cart = await GetOrCreateCartAsync(maKhachHangHienTai);

            // Tìm và xóa chi tiết giỏ hàng
            var chiTiet = cart.ChiTietGioHangs.FirstOrDefault(c => c.MaSanPham == id);
            if (chiTiet != null)
            {
                _context.ChiTietGioHangs.Remove(chiTiet);
                await _context.SaveChangesAsync();

                // Cập nhật lại tổng tiền
                cart.TongTien = cart.ChiTietGioHangs.Where(c => c.MaSanPham != id).Sum(c => (c.SoLuong ?? 0) * (c.Gia ?? 0));
                _context.GioHangs.Update(cart);
                await _context.SaveChangesAsync();
            }

            // Lấy lại dữ liệu mới nhất
            var updatedCart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs).ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            // CÁCH XỬ LÝ AN TOÀN TRÁNH LỖI ÉP KIỂU:
            // Nếu giỏ hàng đã bị xóa sạch (hoặc không tồn tại), trả về mảng rỗng ngay lập tức
            if (updatedCart == null || updatedCart.ChiTietGioHangs == null || !updatedCart.ChiTietGioHangs.Any())
            {
                return Json(new
                {
                    success = true,
                    totalItems = 0,
                    totalPrice = 0,
                    items = new object[0]
                });
            }

            // Đóng gói dữ liệu nếu giỏ hàng vẫn còn sản phẩm
            var items = updatedCart.ChiTietGioHangs.Select(c => new {
                maSanPham = c.MaSanPham,
                tenSanPham = c.MaSanPhamNavigation?.TenSanPham ?? "",
                hinhAnh = string.IsNullOrEmpty(c.MaSanPhamNavigation?.HinhAnh) ? "https://via.placeholder.com/150" : $"/img/{c.MaSanPhamNavigation?.HinhAnh}",
                soLuong = c.SoLuong,
                gia = c.Gia,
                thanhTien = (c.SoLuong ?? 0) * (c.Gia ?? 0)
            }).ToList();

            return Json(new
            {
                success = true,
                totalItems = items.Sum(x => x.soLuong) ?? 0,
                totalPrice = updatedCart.TongTien ?? 0,
                items = items
            });
        }


    }
}