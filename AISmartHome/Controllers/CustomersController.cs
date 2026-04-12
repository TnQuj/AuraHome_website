using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Models;
using AISmartHome.Data;
using Microsoft.AspNetCore.Http; // Bắt buộc để dùng ISession
using System.Text.Json;          // Bắt buộc để dùng JsonSerializer
using System;
using System.Linq;
using System.Threading.Tasks;

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
        // HÀM HỖ TRỢ: ĐỊNH DANH KHÁCH HÀNG QUA SESSION (BẢO MẬT GIỎ HÀNG)
        // =======================================================
        private async Task<int> GetOrSetGuestCustomerId()
        {
            string guestIdStr = HttpContext.Session.GetString("GuestCustomerId");
            if (!string.IsNullOrEmpty(guestIdStr))
            {
                return int.Parse(guestIdStr);
            }

            // Khách mới tinh -> Tạo Khách vãng lai ảo
            var newGuest = new KhachHang
            {
                TenKhachHang = "Khách vãng lai",
                SoDienThoai = "",
                DiaChi = ""
            };

            _context.KhachHangs.Add(newGuest);
            await _context.SaveChangesAsync();

            // Lưu Session
            HttpContext.Session.SetString("GuestCustomerId", newGuest.MaKhachHang.ToString());

            return newGuest.MaKhachHang;
        }

        private async Task<GioHang> GetOrCreateCartAsync(int maKhachHang)
        {
            var cart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs)
                    .ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHang);

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

        // =======================================================
        // TRANG DANH SÁCH & TRANG CHỦ
        // =======================================================
        public async Task<IActionResult> Index(int? category, string sortOrder)
        {
            ViewBag.CurrentCategory = category;

            var viewModel = new HomeViewModel
            {
                DanhMucs = await _context.DanhMucSanPhams.ToListAsync()
            };

            var query = _context.SanPhams.AsQueryable();

            if (category.HasValue)
            {
                query = query.Where(sp => sp.MaDanhMuc == category.Value);
            }

            switch (sortOrder)
            {
                case "price_asc": query = query.OrderBy(sp => sp.GiaBan); break;
                case "price_desc": query = query.OrderByDescending(sp => sp.GiaBan); break;
                case "name_asc": query = query.OrderBy(sp => sp.TenSanPham); break;
                case "name_desc": query = query.OrderByDescending(sp => sp.TenSanPham); break;
                default: query = query.OrderByDescending(sp => sp.MaSanPham); break;
            }

            viewModel.SanPhams = await query.ToListAsync();
            return View(viewModel);
        }

        // =======================================================
        // TÌM KIẾM SẢN PHẨM
        // =======================================================
        [Route("Customers/Search")]
        public async Task<IActionResult> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return RedirectToAction("Index");
            }

            ViewBag.Keyword = keyword;

            var searchResults = await _context.SanPhams
                .Where(s => s.TenSanPham.Contains(keyword) || s.MoTa.Contains(keyword))
                .OrderByDescending(s => s.MaSanPham)
                .ToListAsync();

            return View(searchResults);
        }

        // =======================================================
        // TRANG CHI TIẾT SẢN PHẨM
        // =======================================================
        public async Task<IActionResult> Detail(int? id)
        {
            if (id == null) return NotFound();

            var sanPham = await _context.SanPhams
                .Include(sp => sp.HinhAnhSanPhams)
                .FirstOrDefaultAsync(m => m.MaSanPham == id);

            if (sanPham == null) return NotFound();

            ViewBag.SanPhamsTuongTu = await _context.SanPhams
                .Where(x => x.MaDanhMuc == sanPham.MaDanhMuc && x.MaSanPham != id)
                .Take(4)
                .ToListAsync();

            return View(sanPham);
        }

        // =======================================================
        // QUẢN LÝ GIỎ HÀNG (AJAX & TRANG CHÍNH)
        // =======================================================
        public async Task<IActionResult> Cart()
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId(); // Đã bảo mật
            var cart = await GetOrCreateCartAsync(maKhachHangHienTai);
            return View(cart);
        }

        [HttpPost]
        public async Task<IActionResult> AddToCartAjax(int id, int quantity = 1)
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId(); // Đã bảo mật
            var cart = await GetOrCreateCartAsync(maKhachHangHienTai);
            var sp = await _context.SanPhams.FindAsync(id);

            if (sp == null) return Json(new { success = false, message = "Không tìm thấy sản phẩm" });

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

            cart.TongTien = cart.ChiTietGioHangs.Sum(c => c.SoLuong * c.Gia);
            _context.GioHangs.Update(cart);
            await _context.SaveChangesAsync();

            var updatedCart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs).ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

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

        [HttpPost]
        public async Task<IActionResult> RemoveFromCartAjax(int id)
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId(); // Đã bảo mật
            var cart = await GetOrCreateCartAsync(maKhachHangHienTai);

            var chiTiet = cart.ChiTietGioHangs.FirstOrDefault(c => c.MaSanPham == id);
            if (chiTiet != null)
            {
                _context.ChiTietGioHangs.Remove(chiTiet);
                await _context.SaveChangesAsync();

                cart.TongTien = cart.ChiTietGioHangs.Where(c => c.MaSanPham != id).Sum(c => (c.SoLuong ?? 0) * (c.Gia ?? 0));
                _context.GioHangs.Update(cart);
                await _context.SaveChangesAsync();
            }

            var updatedCart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs).ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (updatedCart == null || updatedCart.ChiTietGioHangs == null || !updatedCart.ChiTietGioHangs.Any())
            {
                return Json(new { success = true, totalItems = 0, totalPrice = 0, items = new object[0] });
            }

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

        [HttpGet]
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId(); // Đã bảo mật
            var cart = await GetOrCreateCartAsync(maKhachHangHienTai);

            var chiTiet = cart.ChiTietGioHangs.FirstOrDefault(c => c.MaSanPham == id);
            if (chiTiet != null)
            {
                _context.ChiTietGioHangs.Remove(chiTiet);
                await _context.SaveChangesAsync();

                cart.TongTien = cart.ChiTietGioHangs.Where(c => c.MaSanPham != id).Sum(c => (c.SoLuong ?? 0) * (c.Gia ?? 0));
                _context.GioHangs.Update(cart);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Cart");
        }

        [HttpGet]
        public async Task<IActionResult> GetMiniCartAjax()
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId(); // Đã bảo mật
            var cart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs).ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (cart == null || cart.ChiTietGioHangs == null || !cart.ChiTietGioHangs.Any())
            {
                return Json(new { success = true, totalItems = 0, totalPrice = 0, items = new object[0] });
            }

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

        public class CartUpdateModel
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCart([FromBody] CartUpdateModel model)
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId(); // Đã bảo mật

            var gioHang = await _context.GioHangs
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (gioHang == null) return Json(new { success = false, message = "Lỗi giỏ hàng" });

            var cartItem = await _context.ChiTietGioHangs
                .FirstOrDefaultAsync(c => c.MaSanPham == model.ProductId && c.MaGioHang == gioHang.MaGioHang);

            if (cartItem != null)
            {
                cartItem.SoLuong = model.Quantity;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Không tìm thấy sản phẩm" });
        }

        // =======================================================
        // MUA NGAY & THANH TOÁN
        // =======================================================
        [Route("Customers/BuyNow")]
        public async Task<IActionResult> BuyNow(int id, int quantity = 1)
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId();

            var sanPham = await _context.SanPhams.FindAsync(id);
            if (sanPham == null) return NotFound("Sản phẩm không tồn tại.");

            var gioHang = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (gioHang == null)
            {
                gioHang = new GioHang { MaKhachHang = maKhachHangHienTai };
                _context.GioHangs.Add(gioHang);
                await _context.SaveChangesAsync();
            }
            else
            {
                if (gioHang.ChiTietGioHangs != null && gioHang.ChiTietGioHangs.Any())
                {
                    _context.ChiTietGioHangs.RemoveRange(gioHang.ChiTietGioHangs);
                    await _context.SaveChangesAsync();
                }
            }

            var newItem = new ChiTietGioHang
            {
                MaGioHang = gioHang.MaGioHang,
                MaSanPham = id,
                SoLuong = quantity,
                Gia = sanPham.GiaBan
            };

            _context.ChiTietGioHangs.Add(newItem);
            await _context.SaveChangesAsync();

            return RedirectToAction("CheckOut");
        }

        [Route("Customers/Checkout")]
        public async Task<IActionResult> Checkout()
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId(); // Đã bảo mật

            var gioHang = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs)
                    .ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (gioHang == null || gioHang.ChiTietGioHangs == null || !gioHang.ChiTietGioHangs.Any())
            {
                return RedirectToAction("Index");
            }

            return View(gioHang);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(
            string FullName, string Phone, string Email,
            string Province, string District, string Address,
            string Note, string PaymentMethod, string PaymentMode,
            string VoucherCode, // MỚI THÊM: Nhận mã voucher từ giao diện
            bool CanLapDat = false)
        {
            try
            {
                int maKhachHangHienTai = await GetOrSetGuestCustomerId();

                // =======================================================
                // 1. MỚI THÊM: ĐỒNG BỘ THÔNG TIN KHÁCH HÀNG XUỐNG ADMIN
                // =======================================================
                var khachHang = await _context.KhachHangs.FindAsync(maKhachHangHienTai);
                if (khachHang != null)
                {
                    khachHang.TenKhachHang = FullName;
                    khachHang.SoDienThoai = Phone;
                    khachHang.Email = Email; // Lưu lại Email khách vừa nhập
                    khachHang.DiaChi = $"{Address}, {District}, {Province}";
                    _context.KhachHangs.Update(khachHang);
                    // Thông tin này sẽ lập tức hiện lên trang Quản lý Khách Hàng của Admin
                }

                var gioHang = await _context.GioHangs
                    .Include(g => g.ChiTietGioHangs)
                    .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

                if (gioHang == null || !gioHang.ChiTietGioHangs.Any())
                {
                    return Json(new { success = false, message = "Giỏ hàng trống!" });
                }

                decimal tongTien = gioHang.ChiTietGioHangs.Sum(c => (c.SoLuong ?? 0) * (c.Gia ?? 0));

                // =======================================================
                // 2. MỚI THÊM: XỬ LÝ VOUCHER (Tính tiền & Khóa mã)
                // =======================================================
                VoucherHistory lichSuVoucherDung = null;

                if (!string.IsNullOrEmpty(VoucherCode) && !string.IsNullOrEmpty(Email))
                {
                    var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == VoucherCode && v.TrangThai == true);
                    if (voucher != null)
                    {
                        // Kiểm tra chéo: Email này có thực sự sở hữu mã này không? Mã đã bị dùng chưa?
                        lichSuVoucherDung = await _context.VoucherHistories.FirstOrDefaultAsync(vh => vh.Email == Email && vh.MaVoucher == voucher.MaVoucher && vh.MaDonHang == null);

                        if (lichSuVoucherDung != null)
                        {
                            // Tính toán số tiền được giảm
                            decimal soTienGiam = 0;
                            if (voucher.LoaiGiamGia == "PhanTram")
                            {
                                soTienGiam = tongTien * ((decimal)voucher.GiaTriGiam / 100m);
                                if (voucher.GiamToiDa > 0 && soTienGiam > voucher.GiamToiDa)
                                {
                                    soTienGiam = (decimal)voucher.GiamToiDa;
                                }
                            }
                            else
                            {
                                soTienGiam = (decimal)voucher.GiaTriGiam;
                            }

                            // Không cho giảm âm tiền
                            if (soTienGiam > tongTien) soTienGiam = tongTien;

                            // Trừ tiền vào tổng hóa đơn
                            tongTien -= soTienGiam;

                            // Tăng số lượt đã dùng của Voucher lên 1
                            voucher.SoLuongDaDung += 1;
                            _context.Vouchers.Update(voucher);
                        }
                    }
                }

                // =======================================================
                // 3. TẠO ĐƠN HÀNG (Giữ nguyên logic của bạn)
                // =======================================================
                var donHang = new DonHang
                {
                    MaKhachHang = maKhachHangHienTai,
                    NgayDatHang = DateTime.Now,
                    TongTien = tongTien, // Lúc này tongTien đã được trừ Voucher (nếu có)
                    TenKhachHang = FullName,
                    SoDienThoai = Phone,
                    DiaChiGiaoHang = $"{Address}, {District}, {Province}",
                    PhuongThucThanhToan = PaymentMethod ?? "Chuyển khoản",
                    TrangThaiDonHang = "Chờ xử lý"
                };

                if (CanLapDat == true && PaymentMode == "30")
                {
                    donHang.TrangThaiThanhToan = "Đã cọc";
                }
                else
                {
                    donHang.TrangThaiThanhToan = "Đã thanh toán";
                }

                _context.DonHangs.Add(donHang);
                await _context.SaveChangesAsync(); // Cần Save để sinh ra MaDonHang

                // =======================================================
                // 4. MỚI THÊM: GẮN MÃ ĐƠN HÀNG VÀO LỊCH SỬ VOUCHER
                // =======================================================
                if (lichSuVoucherDung != null)
                {
                    lichSuVoucherDung.MaDonHang = donHang.MaDonHang; // Đánh dấu mã đã dùng cho đơn này
                    lichSuVoucherDung.NgaySuDung = DateTime.Now;
                    _context.VoucherHistories.Update(lichSuVoucherDung);
                    await _context.SaveChangesAsync();
                }

                // --- XỬ LÝ YÊU CẦU LẮP ĐẶT VÀ CHI TIẾT ĐƠN HÀNG (Giữ nguyên) ---
                if (CanLapDat == true)
                {
                    var yeuCauMoi = new YeuCauLapDat
                    {
                        MaDonHang = donHang.MaDonHang,
                        DiaChiLapDat = donHang.DiaChiGiaoHang,
                        NgayLap = DateTime.Now,
                        TrangThaiLapDat = "Chưa lắp đặt",
                        PhiLapDat = null,
                        GhiChuBaoGia = Note,
                        MaNhanVien = null
                    };
                    _context.YeuCauLapDats.Add(yeuCauMoi);
                }

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

                _context.ChiTietGioHangs.RemoveRange(gioHang.ChiTietGioHangs);
                gioHang.TongTien = 0;
                _context.GioHangs.Update(gioHang);

                await _context.SaveChangesAsync();

                HttpContext.Session.SetString("CustomerPhone", Phone);

                return Json(new { success = true, orderId = donHang.MaDonHang });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // =======================================================
        // TRA CỨU & HỦY ĐƠN HÀNG
        // =======================================================
        [Route("Customers/OrderHistory")]
        public async Task<IActionResult> OrderHistory(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return View(null);

            var orders = await _context.DonHangs
                .Include(d => d.ChiTietDonHangs)
                    .ThenInclude(c => c.MaSanPhamNavigation)
                .Include(d => d.YeuCauLapDats)
                .Where(d => d.SoDienThoai == phone || d.MaKhachHangNavigation!.SoDienThoai == phone)
                .OrderByDescending(d => d.NgayDatHang)
                .ToListAsync();

            if (!orders.Any())
            {
                ViewBag.Error = $"Không tìm thấy đơn hàng nào với số điện thoại {phone}";
                return View(null);
            }

            ViewBag.Phone = phone;
            HttpContext.Session.SetString("CustomerPhone", phone);

            return View(orders);
        }

        [Route("Customers/HuyDonHangXacNhan")]
        public async Task<IActionResult> HuyDonHangXacNhan(int maDonHang, string phone)
        {
            var order = await _context.DonHangs
                .Include(d => d.YeuCauLapDats)
                .FirstOrDefaultAsync(d => d.MaDonHang == maDonHang && (d.SoDienThoai == phone || d.MaKhachHangNavigation!.SoDienThoai == phone));

            if (order == null) return NotFound();

            bool choPhepHuy = order.TrangThaiDonHang == "Chờ xử lý"
                           && (order.YeuCauLapDats == null || !order.YeuCauLapDats.Any() || order.YeuCauLapDats.First().TrangThaiLapDat == "Chưa lắp đặt");

            if (choPhepHuy)
            {
                order.TrangThaiDonHang = "Đã hủy";

                if (order.YeuCauLapDats != null && order.YeuCauLapDats.Any())
                {
                    var lapDat = order.YeuCauLapDats.First();
                    lapDat.TrangThaiLapDat = "Bị hủy";
                    _context.Update(lapDat);
                }

                _context.Update(order);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("OrderHistory", new { phone = phone });
        }

        [HttpPost]
        public async Task<IActionResult> CheckVoucher(string code, decimal totalAmount)
        {
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == code && v.TrangThai == true);

            if (voucher == null || DateTime.Now > voucher.NgayHetHan || voucher.SoLuongDaDung >= voucher.SoLuongToiDa)
            {
                return Json(new { success = false, message = "Mã giảm giá không tồn tại hoặc đã hết hạn." });
            }

            if (totalAmount < voucher.GiaTriDonToiThieu)
            {
                return Json(new { success = false, message = $"Mã này chỉ áp dụng cho đơn hàng từ {voucher.GiaTriDonToiThieu:N0}đ" });
            }

            decimal discountAmount = 0;
            if (voucher.LoaiGiamGia == "PhanTram")
            {
                discountAmount = totalAmount * (voucher.GiaTriGiam / 100);
                if (discountAmount > voucher.GiamToiDa) discountAmount = voucher.GiamToiDa;
            }
            else
            {
                discountAmount = voucher.GiaTriGiam;
            }

            return Json(new { success = true, discount = discountAmount, newTotal = totalAmount - discountAmount });
        }

        [HttpPost("Cart/ApplyVoucher")]
        public async Task<IActionResult> ApplyVoucher(string code, decimal totalAmount, string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return Json(new { success = false, message = "Vui lòng nhập Email giao hàng trước khi áp mã!" });

                var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == code);
                if (voucher == null)
                    return Json(new { success = false, message = "Mã giảm giá không tồn tại!" });

                var lichSu = await _context.VoucherHistories
                    .FirstOrDefaultAsync(vh => vh.Email == email && vh.MaVoucher == voucher.MaVoucher);

                if (lichSu == null)
                    return Json(new { success = false, message = "Email này không sở hữu mã giảm giá này!" });

                if (lichSu.MaDonHang != null)
                    return Json(new { success = false, message = "Bạn đã sử dụng mã này cho một đơn hàng khác rồi!" });

                // LOGIC MỚI: TỰ ĐỘNG HỦY MÃ NẾU HẾT HẠN
                if (DateTime.Now > voucher.NgayHetHan || voucher.TrangThai == false)
                {
                    // Nếu phát hiện quá hạn mà trạng thái vẫn là true, thì tắt nó đi
                    if (voucher.TrangThai == true)
                    {
                        voucher.TrangThai = false;
                        await _context.SaveChangesAsync();
                    }
                    return Json(new { success = false, message = "Rất tiếc, mã giảm giá này đã hết hạn sử dụng và đã bị hệ thống hủy bỏ!" });
                }

                // Tính toán tiền giảm
                decimal discountAmount = 0;
                if (voucher.LoaiGiamGia == "PhanTram")
                {
                    discountAmount = totalAmount * (voucher.GiaTriGiam / 100);
                    if (voucher.GiamToiDa > 0 && discountAmount > voucher.GiamToiDa) discountAmount = voucher.GiamToiDa;
                }
                else if (voucher.LoaiGiamGia == "SoTien")
                {
                    discountAmount = voucher.GiaTriGiam;
                }

                if (discountAmount > totalAmount) discountAmount = totalAmount;

                return Json(new { success = true, message = "Áp dụng mã thành công!", discountAmount = discountAmount, newTotal = totalAmount - discountAmount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpGet("Cart/GetMyVouchers")]
        public async Task<IActionResult> GetMyVouchers(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Json(new { success = false, message = "Vui lòng nhập email." });

            // Tìm tất cả mã của email này mà CHƯA DÙNG (MaDonHang == null)
            var myVouchers = await _context.VoucherHistories
                .Include(vh => vh.VoucherNavigation)
                .Where(vh => vh.Email == email && vh.MaDonHang == null)
                .Select(vh => new
                {
                    code = vh.VoucherNavigation.Code,
                    moTa = vh.VoucherNavigation.MoTa,
                    ngayHetHan = vh.VoucherNavigation.NgayHetHan,
                    // Kiểm tra xem đã quá hạn chưa
                    isExpired = DateTime.Now > vh.VoucherNavigation.NgayHetHan || vh.VoucherNavigation.TrangThai == false
                })
                .ToListAsync();

            // Chỉ lấy những mã còn "Sống"
            var validVouchers = myVouchers.Where(v => !v.isExpired).ToList();

            if (!validVouchers.Any())
                return Json(new { success = false, message = "Không tìm thấy ưu đãi nào khả dụng cho email này. Hoặc mã của bạn đã hết hạn!" });

            return Json(new { success = true, data = validVouchers });
        }


        // =======================================================
        // TIN TỨC
        // =======================================================
        // GET: /Customers/TinTuc
        public async Task<IActionResult> News(int? danhMucId)
        {
            // 1. Lấy danh sách danh mục để làm Menu lọc
            ViewBag.Categories = await _context.DanhMucBaiViets.ToListAsync();
            ViewBag.CurrentCategory = danhMucId;

            // 2. Lấy các bài viết ĐÃ ĐƯỢC DUYỆT
            var query = _context.BaiViets
                .Include(b => b.MaDanhMucBaiVietNavigation)
                .Where(b => b.IsApproved == true);

            // 3. Lọc theo danh mục nếu người dùng có bấm chọn
            if (danhMucId.HasValue)
            {
                query = query.Where(b => b.MaDanhMucBaiViet == danhMucId.Value);
            }

            var danhSachBaiViet = await query.OrderByDescending(b => b.NgayDang).ToListAsync();
            return View(danhSachBaiViet);
        }

        // GET: /Customers/TinTucDetails/5
        public async Task<IActionResult> NewDetail(int? id)
        {
            if (id == null) return NotFound();

            var baiViet = await _context.BaiViets
                .Include(b => b.MaDanhMucBaiVietNavigation)
                .Include(b => b.MaTaiKhoanNavigation) // Để hiện tác giả nếu cần
                .FirstOrDefaultAsync(m => m.MaBaiViet == id && m.IsApproved == true);

            if (baiViet == null) return NotFound();

            // Lấy thêm tin liên quan (cùng danh mục, trừ bài hiện tại)
            ViewBag.RelatedPosts = await _context.BaiViets
                .Where(b => b.MaDanhMucBaiViet == baiViet.MaDanhMucBaiViet && b.MaBaiViet != id && b.IsApproved == true)
                .Take(3)
                .ToListAsync();

            return View(baiViet);
        }

        public IActionResult Guide()
        {
            ViewData["Title"] = "Trung tâm hỗ trợ & Hướng dẫn sử dụng";
            return View();
        }

        // Nếu bạn muốn làm trang hướng dẫn chi tiết theo ID thiết bị
        // GET: Customers/GuideDetail/5
        public IActionResult GuideDetail(int id)
        {
            // Logic lấy dữ liệu hướng dẫn từ DB dựa trên id
            return View();
        }
    }
}