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
            string Province, 
            string District, 
            string Address,  
            string Note,
            string PaymentMethod,
            string PaymentMode, // BẮT BUỘC THÊM THAM SỐ NÀY VÀO ĐÂY
            bool CanLapDat = false) 
        {
    try
    {
        int maKhachHangHienTai = 1; // Giả lập ID khách hàng

        // 1. Lấy thông tin giỏ hàng
        var gioHang = await _context.GioHangs
            .Include(g => g.ChiTietGioHangs)
            .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

        if (gioHang == null || !gioHang.ChiTietGioHangs.Any())
        {
            return Json(new { success = false, message = "Giỏ hàng trống!" });
        }

        // Tính tổng tiền từ các món hàng trong giỏ
        decimal tongTien = gioHang.ChiTietGioHangs.Sum(c => (c.SoLuong ?? 0) * (c.Gia ?? 0));

        // 2. Khởi tạo đơn hàng mới với thông tin từ Form
        var donHang = new DonHang
        {
            MaKhachHang = maKhachHangHienTai,
            NgayDatHang = DateTime.Now,
            TongTien = tongTien,
            TenKhachHang = FullName,
            SoDienThoai = Phone,
            DiaChiGiaoHang = $"{Address}, {District}, {Province}", // Ghép chuỗi địa chỉ
            PhuongThucThanhToan = PaymentMethod ?? "Chuyển khoản",
            TrangThaiDonHang = "Chờ xử lý" // TRẠNG THÁI GIAO HÀNG (Luôn là Chờ xử lý)
        };

        // ====================================================================
        // 3. LOGIC XỬ LÝ TRẠNG THÁI THANH TOÁN (ĐÃ SỬA CHUẨN XÁC)
        // ====================================================================
        string paymentMode = Request.Form["PaymentMode"];
                if (CanLapDat == true && PaymentMode == "30")
                {
                    // Có lắp đặt VÀ khách chọn mode cọc 30%
                    donHang.TrangThaiThanhToan = "Đã cọc";
                }
                else
                {
                    // Khách trả 100% (Cho dù có lắp đặt hay giao chuẩn)
                    donHang.TrangThaiThanhToan = "Đã thanh toán";
                }
                // 4. Lưu đơn hàng vào Database ĐỂ LẤY ID TỰ TĂNG
                _context.DonHangs.Add(donHang);
        await _context.SaveChangesAsync();

        // 5. Nếu có lắp đặt -> Sinh ra Yêu cầu lắp đặt
        if (CanLapDat == true)
        {
            var yeuCauMoi = new YeuCauLapDat
            {
                MaDonHang = donHang.MaDonHang,
                DiaChiLapDat = donHang.DiaChiGiaoHang,
                NgayLap = DateTime.Now,
                TrangThaiLapDat = "Chưa lắp đặt", // Đưa vào trạng thái mặc định cho thợ
                PhiLapDat = null,  
                GhiChuBaoGia = Note, // Đẩy ghi chú của khách sang cho thợ đọc
                MaNhanVien = null  
            };
            _context.YeuCauLapDats.Add(yeuCauMoi);
        }

        // 6. Chuyển chi tiết từ Giỏ hàng sang Chi tiết đơn hàng
        foreach (var item in gioHang.ChiTietGioHangs)
        {
            _context.ChiTietDonHangs.Add(new ChiTietDonHang
            {
                MaDonHang = donHang.MaDonHang, 
                MaSanPham = item.MaSanPham,
                SoLuong = item.SoLuong,
                Gia = item.Gia
            });
        }

        // 7. Dọn dẹp giỏ hàng sau khi đã đặt hàng thành công
        _context.ChiTietGioHangs.RemoveRange(gioHang.ChiTietGioHangs);
        gioHang.TongTien = 0;
        _context.GioHangs.Update(gioHang);

        // 8. Lưu tất cả thay đổi (Yêu cầu lắp đặt, Chi tiết đơn, Dọn giỏ hàng)
        await _context.SaveChangesAsync();

                // Đánh dấu trình duyệt này đang là của khách hàng có SĐT này
                HttpContext.Session.SetString("CustomerPhone", Phone);

                // TRẢ VỀ JSON THÀNH CÔNG CHO JAVASCRIPT BẬT QR CODE
                return Json(new { success = true, orderId = donHang.MaDonHang });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
    }
}

        // Tạo class để map với chuỗi JSON từ JS gửi lên
        public class CartUpdateModel
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        // Hàm này giúp kiểm tra xem trình duyệt đã có ID chưa, chưa có thì tạo mới
        private async Task<int> GetOrSetGuestCustomerId()
        {
            // Đọc Session xem đã có ID chưa
            string guestIdStr = HttpContext.Session.GetString("GuestCustomerId");
            if (!string.IsNullOrEmpty(guestIdStr))
            {
                return int.Parse(guestIdStr);
            }

            // Nếu chưa có (Khách mới tinh), tạo 1 record "Khách vãng lai" ảo trong DB
            var newGuest = new KhachHang
            {
                TenKhachHang = "Khách vãng lai" // Số điện thoại, địa chỉ... tạm thời để trống
            };

            _context.KhachHangs.Add(newGuest);
            await _context.SaveChangesAsync();

            // Lưu ID vừa tạo vào Session để lần sau khách bấm thêm đồ sẽ không bị tạo mới nữa
            HttpContext.Session.SetString("GuestCustomerId", newGuest.MaKhachHang.ToString());

            return newGuest.MaKhachHang;
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

        // 1. THÊM THAM SỐ "quantity" VÀO ĐÂY (Mặc định là 1 nếu khách lỡ nhập sai)
        public async Task<IActionResult> BuyNow(int id, int quantity = 1)
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId();
            var sanPham = await _context.SanPhams.FindAsync(id);
            if (sanPham == null) return NotFound();

            var gioHang = await _context.GioHangs
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (gioHang == null)
            {
                gioHang = new GioHang { MaKhachHang = maKhachHangHienTai };
                _context.GioHangs.Add(gioHang);
                await _context.SaveChangesAsync();
            }

            var cartItem = await _context.ChiTietGioHangs
                .FirstOrDefaultAsync(c => c.MaSanPham == id && c.MaGioHang == gioHang.MaGioHang);

            if (cartItem != null)
            {
                // 2. THAY VÌ CỘNG 1, HÃY CỘNG VỚI SỐ LƯỢNG KHÁCH ĐÃ CHỌN
                cartItem.SoLuong += quantity;
            }
            else
            {
                _context.ChiTietGioHangs.Add(new ChiTietGioHang
                {
                    MaGioHang = gioHang.MaGioHang,
                    MaSanPham = id,
                    SoLuong = quantity, // 3. GÁN ĐÚNG SỐ LƯỢNG KHI MUA MỚI
                    Gia = sanPham.GiaBan
                });
            }

            await _context.SaveChangesAsync();

            // Chuyển thẳng sang trang Thanh toán
            return RedirectToAction("Checkout");
        }



        // GET & POST chung 1 đường dẫn: /TraCuuDonHang
        [Route("Customers/OrderHistory")]
        public async Task<IActionResult> OrderHistory(string phone)
        {
            // 1. Nếu khách mới bấm vào link (chưa nhập SĐT) -> Trả về giao diện Form trống
            if (string.IsNullOrEmpty(phone))
            {
                return View(null);
            }

            // 2. Nếu khách đã nhập SĐT -> Tiến hành quét trong DB
            // Tìm đơn hàng khớp số điện thoại trên Đơn Hàng HOẶC số điện thoại của Khách Hàng
            var orders = await _context.DonHangs
                .Include(d => d.ChiTietDonHangs)
                    .ThenInclude(c => c.MaSanPhamNavigation)
                .Include(d => d.YeuCauLapDats)
                .Where(d => d.SoDienThoai == phone || d.MaKhachHangNavigation!.SoDienThoai == phone)
                .OrderByDescending(d => d.NgayDatHang)
                .ToListAsync();

            // 3. Xử lý kết quả
            if (!orders.Any())
            {
                ViewBag.Error = $"Không tìm thấy đơn hàng nào với số điện thoại {phone}";
                return View(null); // Trả lại form trống kèm báo lỗi
            }
            // Lưu số điện thoại vào Session để chuông thông báo bắt đầu hoạt động
            ViewBag.Phone = phone; // Lưu lại SĐT để hiển thị ra View

            HttpContext.Session.SetString("CustomerPhone", phone);

            return View(orders); // Trả về danh sách đơn hàng
        }


        [Route("Customers/HuyDonHangXacNhan")]
        public async Task<IActionResult> HuyDonHangXacNhan(int maDonHang, string phone)
        {
            // 1. Tìm đơn hàng
            var order = await _context.DonHangs
                .Include(d => d.YeuCauLapDats)
                .FirstOrDefaultAsync(d => d.MaDonHang == maDonHang && (d.SoDienThoai == phone || d.MaKhachHangNavigation.SoDienThoai == phone));

            if (order == null) return NotFound();

            // 2. Kiểm tra lại điều kiện (bảo mật kép phòng hờ khách dùng tool hack)
            bool choPhepHuy = order.TrangThaiDonHang == "Chờ xử lý"
                           && (order.YeuCauLapDats == null || !order.YeuCauLapDats.Any() || order.YeuCauLapDats.First().TrangThaiLapDat == "Chưa lắp đặt");

            if (choPhepHuy)
            {
                // 3. Cập nhật trạng thái
                order.TrangThaiDonHang = "Đã hủy";

                // Hủy luôn yêu cầu lắp đặt (nếu có)
                if (order.YeuCauLapDats != null && order.YeuCauLapDats.Any())
                {
                    var lapDat = order.YeuCauLapDats.First();
                    lapDat.TrangThaiLapDat = "Bị hủy";
                    _context.Update(lapDat);
                }

                _context.Update(order);
                await _context.SaveChangesAsync();
            }

            // 4. Quay lại trang tra cứu của chính số điện thoại đó
            return RedirectToAction("OrderHistory", new { phone = phone });
        }

        // GET: /SanPhams/Search?keyword=xyz
        public async Task<IActionResult> Search(string keyword)
        {
            // Nếu khách cố tình gõ khoảng trắng hoặc không gõ gì thì đẩy về trang chủ/trang sản phẩm
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return RedirectToAction("Index");
            }

            // Lưu lại từ khóa để in ra giao diện "Kết quả tìm kiếm cho: xyz"
            ViewBag.Keyword = keyword;

            // Tìm kiếm tương đối (Contains) trong Tên sản phẩm hoặc Mô tả
            var searchResults = await _context.SanPhams
                .Where(s => s.TenSanPham.Contains(keyword) || s.MoTa.Contains(keyword))
                .OrderByDescending(s => s.MaSanPham) // Có thể đổi thành xếp theo Giá, Số lượng...
                .ToListAsync();

            return View(searchResults);
        }


    }
}