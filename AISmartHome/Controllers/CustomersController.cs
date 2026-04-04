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
        public async Task<IActionResult> Index(int? category, string sortOrder)
        {
            // Giữ lại tham số cho View
            ViewBag.CurrentCategory = category;

            // Load danh mục
            var viewModel = new HomeViewModel
            {
                DanhMucs = await _context.DanhMucSanPhams.ToListAsync()
            };

            // 1. BẮT ĐẦU TẠO CÂU TRUY VẤN (Chưa lấy dữ liệu vội)
            var query = _context.SanPhams.AsQueryable();

            // 2. LỌC THEO DANH MỤC (Nếu người dùng có bấm vào danh mục)
            if (category.HasValue)
            {
                query = query.Where(sp => sp.MaDanhMuc == category.Value);
            }

            // 3. SẮP XẾP SẢN PHẨM (Giá, Tên)
            switch (sortOrder)
            {
                case "price_asc": // Giá: Thấp đến Cao
                    query = query.OrderBy(sp => sp.GiaBan);
                    break;
                case "price_desc": // Giá: Cao đến Thấp
                    query = query.OrderByDescending(sp => sp.GiaBan);
                    break;
                case "name_asc": // Tên: A đến Z
                    query = query.OrderBy(sp => sp.TenSanPham);
                    break;
                case "name_desc": // Tên: Z đến A
                    query = query.OrderByDescending(sp => sp.TenSanPham);
                    break;
                default:
                    // Mặc định: Sản phẩm mới nhất lên đầu (hoặc bạn có thể bỏ dòng này đi)
                    query = query.OrderByDescending(sp => sp.MaSanPham);
                    break;
            }

            // 4. CHỐT SỔ: Chạy xuống Database lấy dữ liệu thực tế và gán vào ViewModel
            viewModel.SanPhams = await query.ToListAsync();

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
        [HttpPost] // Nếu thiếu dòng này, Javascript gọi POST sẽ bị lỗi 404/405
        public async Task<IActionResult> RemoveFromCartAjax(int id)
        {
            int maKhachHangHienTai = 1; // Đang test với khách hàng số 1
            var cart = await GetOrCreateCartAsync(maKhachHangHienTai); // Hoặc cách bạn lấy giỏ hàng

            // 1. TÌM VÀ XÓA SẢN PHẨM TRONG GIỎ
            var chiTiet = cart.ChiTietGioHangs.FirstOrDefault(c => c.MaSanPham == id);
            if (chiTiet != null)
            {
                _context.ChiTietGioHangs.Remove(chiTiet);
                await _context.SaveChangesAsync();

                // Cập nhật lại tổng tiền (Quan trọng)
                cart.TongTien = cart.ChiTietGioHangs.Where(c => c.MaSanPham != id).Sum(c => (c.SoLuong ?? 0) * (c.Gia ?? 0));
                _context.GioHangs.Update(cart);
                await _context.SaveChangesAsync();
            }

            // 2. LẤY LẠI GIỎ HÀNG SAU KHI XÓA ĐỂ TRẢ VỀ CHO JAVASCRIPT
            var updatedCart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs).ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            // Xử lý an toàn: Nếu xóa xong mà giỏ hàng rỗng
            if (updatedCart == null || updatedCart.ChiTietGioHangs == null || !updatedCart.ChiTietGioHangs.Any())
            {
                return Json(new { success = true, totalItems = 0, totalPrice = 0, items = new object[0] });
            }

            // Đóng gói danh sách sản phẩm còn lại
            var items = updatedCart.ChiTietGioHangs.Select(c => new {
                maSanPham = c.MaSanPham,
                tenSanPham = c.MaSanPhamNavigation?.TenSanPham ?? "",
                hinhAnh = string.IsNullOrEmpty(c.MaSanPhamNavigation?.HinhAnh) ? "https://via.placeholder.com/150" : $"/img/{c.MaSanPhamNavigation?.HinhAnh}",
                soLuong = c.SoLuong,
                gia = c.Gia,
                thanhTien = (c.SoLuong ?? 0) * (c.Gia ?? 0)
            }).ToList();

            // 3. TRẢ VỀ KẾT QUẢ CHO JAVASCRIPT
            return Json(new
            {
                success = true,
                totalItems = items.Sum(x => x.soLuong) ?? 0, // Tính lại tổng số món
                totalPrice = updatedCart.TongTien ?? 0, // Tổng tiền mới
                items = items // Danh sách HTML
            });
        }
        // =========================================================
        // HÀM DÀNH RIÊNG CHO TRANG GIỎ HÀNG CHÍNH (Cart.cshtml)
        // Không dùng AJAX, xóa xong thì F5 load lại trang
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            int maKhachHangHienTai = 1; // Khách hàng số 1
            var cart = await GetOrCreateCartAsync(maKhachHangHienTai);

            // Tìm và xóa sản phẩm
            var chiTiet = cart.ChiTietGioHangs.FirstOrDefault(c => c.MaSanPham == id);
            if (chiTiet != null)
            {
                _context.ChiTietGioHangs.Remove(chiTiet);
                await _context.SaveChangesAsync();

                // Tính lại tổng tiền
                cart.TongTien = cart.ChiTietGioHangs.Where(c => c.MaSanPham != id).Sum(c => (c.SoLuong ?? 0) * (c.Gia ?? 0));
                _context.GioHangs.Update(cart);
                await _context.SaveChangesAsync();
            }

            // Tự động load lại trang Cart sau khi xóa xong
            return RedirectToAction("Cart");
        }
        // Lấy dữ liệu cho Giỏ hàng trượt
        [HttpGet]
        public async Task<IActionResult> GetMiniCartAjax()
        {
            int maKhachHangHienTai = 1; // Khách hàng số 1
            var cart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs).ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            // Nếu giỏ hàng trống
            if (cart == null || cart.ChiTietGioHangs == null || !cart.ChiTietGioHangs.Any())
            {
                return Json(new { success = true, totalItems = 0, totalPrice = 0, items = new object[0] });
            }

            // Nếu có sản phẩm, đóng gói dữ liệu
            var items = cart.ChiTietGioHangs.Select(c => new {
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
                totalPrice = cart.TongTien ?? 0,
                items = items
            });
        }

        // GET: Customers/Checkout
        public async Task<IActionResult> Checkout()
        {
            int maKhachHangHienTai = 1; // Thay bằng ID thực tế

            var gioHang = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs)
                    .ThenInclude(ct => ct.MaSanPhamNavigation) // Quan trọng để lấy TenSanPham
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (gioHang == null) return RedirectToAction("Index");

            // Tính toán tổng tiền ngay tại Controller để truyền sang View cho chính xác
            ViewBag.TongTien = gioHang.ChiTietGioHangs.Sum(x => (x.Gia ?? 0) * (x.SoLuong ?? 0));

            return View(gioHang);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(
    string FullName,
    string Phone,
    string Email,
    string Province, // Thêm Tỉnh
    string District, // Thêm Huyện
    string Address,  // Địa chỉ cụ thể
    string Note,
    string PaymentMethod,
    bool CanLapDat = false) // BƯỚC 1: Thêm tham số CanLapDat (Mặc định là false nếu khách không tick)
        {
            // Giả lập ID khách hàng (Sau này thay bằng User.Identity nếu có đăng nhập)
            int maKhachHangHienTai = 1;

            // 1. Lấy thông tin giỏ hàng kèm theo chi tiết
            var gioHang = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            // Kiểm tra giỏ hàng trống
            if (gioHang == null || !gioHang.ChiTietGioHangs.Any())
            {
                return RedirectToAction("Cart");
            }

            // 2. Tạo hóa đơn mới (DonHang)
            var donHang = new DonHang
            {
                MaKhachHang = maKhachHangHienTai,
                NgayDatHang = DateTime.Now,
                TongTien = gioHang.ChiTietGioHangs.Sum(x => (x.Gia ?? 0) * (x.SoLuong ?? 0)),
                TrangThaiDonHang = "Chờ xử lý",

                // Gộp tất cả thông tin địa chỉ và liên hệ vào cột GhiChu để Admin dễ quản lý
                GhiChu = $"[KH: {FullName}] - [SĐT: {Phone}] - [Email: {Email}] - [Đ/C: {Address}, {District}, {Province}] - [Note: {Note}]",

                // Nếu DB của bạn có cột TenKhachHang, SoDienThoai riêng thì gán thêm:
                TenKhachHang = FullName,
                Email = Email,
                SoDienThoai = Phone,
                DiaChiGiaoHang = $"{Address}, {District}, {Province}"
            };

            _context.DonHangs.Add(donHang);
            await _context.SaveChangesAsync(); // Lưu để lấy MaDonHang (Identity)

            // =========================================================================
            // BƯỚC 2: TẠO YÊU CẦU LẮP ĐẶT NẾU KHÁCH HÀNG TÍCH CHỌN
            // =========================================================================
            if (CanLapDat == true)
            {
                var yeuCauMoi = new YeuCauLapDat
                {
                    MaDonHang = donHang.MaDonHang, // Nối với Đơn hàng vừa sinh ra
                    DiaChiLapDat = donHang.DiaChiGiaoHang, // Dùng luôn địa chỉ giao hàng
                    NgayLap = DateTime.Now,
                    TrangThaiLapDat = "Chờ báo giá", // Đưa vào trạng thái chờ
                    PhiLapDat = null,  // Chưa có tiền báo giá
                    GhiChuBaoGia = null,
                    MaNhanVien = null  // Chưa phân công
                };

                _context.YeuCauLapDats.Add(yeuCauMoi);
                // Không cần SaveChanges ngay vì lát nữa ở dưới sẽ Save 1 thể
            }
            // =========================================================================

            // 3. Chuyển chi tiết từ Giỏ hàng sang Chi tiết hóa đơn
            foreach (var item in gioHang.ChiTietGioHangs)
            {
                var ctDonHang = new ChiTietDonHang
                {
                    MaDonHang = donHang.MaDonHang, // ID vừa sinh ra ở trên
                    MaSanPham = item.MaSanPham,
                    SoLuong = item.SoLuong,
                    Gia = item.Gia
                };
                _context.ChiTietDonHangs.Add(ctDonHang);
            }

            // 4. Dọn dẹp giỏ hàng sau khi đã đặt hàng
            _context.ChiTietGioHangs.RemoveRange(gioHang.ChiTietGioHangs);

            // 5. Lưu toàn bộ (Chi tiết đơn + Xóa giỏ + Yêu cầu lắp đặt nếu có) và trả về thông báo
            try
            {
                // Thực hiện lưu toàn bộ thay đổi
                await _context.SaveChangesAsync();

                // TRẢ VỀ JSON THÀNH CÔNG
                return Json(new { success = true, orderId = donHang.MaDonHang });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }


        // Tạo class để map với chuỗi JSON từ JS gửi lên
        public class CartUpdateModel
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCart([FromBody] CartUpdateModel model)
        {
            int maKhachHangHienTai = 1; // ID khách hàng (Fix cứng tạm thời)

            // Lấy giỏ hàng của khách
            var gioHang = await _context.GioHangs
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (gioHang == null) return Json(new { success = false, message = "Lỗi giỏ hàng" });

            // Tìm món hàng đó trong giỏ
            var cartItem = await _context.ChiTietGioHangs
                .FirstOrDefaultAsync(c => c.MaSanPham == model.ProductId && c.MaGioHang == gioHang.MaGioHang);

            if (cartItem != null)
            {
                // Cập nhật số lượng mới và LƯU LẠI
                cartItem.SoLuong = model.Quantity;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Không tìm thấy sản phẩm" });
        }

        public async Task<IActionResult> BuyNow(int id)
        {
            int maKhachHangHienTai = 1; // Giả lập ID khách hàng (Sau này đổi thành ID thật)

            // 1. Kiểm tra sản phẩm có tồn tại không
            var sanPham = await _context.SanPhams.FindAsync(id);
            if (sanPham == null) return NotFound();

            // 2. Tìm hoặc tạo giỏ hàng cho khách này
            var gioHang = await _context.GioHangs
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (gioHang == null)
            {
                gioHang = new GioHang { MaKhachHang = maKhachHangHienTai };
                _context.GioHangs.Add(gioHang);
                await _context.SaveChangesAsync();
            }

            // 3. Kiểm tra sản phẩm đã có trong giỏ chưa
            var cartItem = await _context.ChiTietGioHangs
                .FirstOrDefaultAsync(c => c.MaSanPham == id && c.MaGioHang == gioHang.MaGioHang);

            if (cartItem != null)
            {
                // Nếu có rồi thì tăng số lượng lên 1
                cartItem.SoLuong += 1;
            }
            else
            {
                // Nếu chưa có thì thêm mới vào giỏ
                _context.ChiTietGioHangs.Add(new ChiTietGioHang
                {
                    MaGioHang = gioHang.MaGioHang,
                    MaSanPham = id,
                    SoLuong = 1,
                    // Lưu ý: Sửa lại thuộc tính giá này cho đúng với Database của bạn (GiaBan, GiaKhuyenMai, v.v.)
                    Gia = sanPham.GiaBan
                });
            }

            // 4. Lưu thay đổi vào Database
            await _context.SaveChangesAsync();

            // 5. Chuyển thẳng sang trang Thanh toán (Checkout)
            return RedirectToAction("Checkout");
        }
    }
}