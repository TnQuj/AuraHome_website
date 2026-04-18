using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Data;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AISmartHome.ViewComponents
{
    public class NotificationViewComponent : ViewComponent
    {
        private readonly AISmartHomeDbContext _context;

        public NotificationViewComponent(AISmartHomeDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync(bool isForAdmin = false)
        {
            {
                if (isForAdmin)
                {
                    // =======================================================
                    // LOGIC ADMIN CỦA BẠN (GIỮ NGUYÊN)
                    // =======================================================
                    var newOrders = await _context.DonHangs
                        .Include(d => d.MaKhachHangNavigation)
                        .Where(d => d.TrangThaiDonHang == "Chờ xử lý")
                        .OrderByDescending(d => d.NgayDatHang)
                        .Take(5)
                        .Select(d => new NotificationItem
                        {
                            Id = d.MaDonHang,
                            Type = "Order",
                            Title = $"Đơn hàng mới! #{d.MaDonHang}",
                            Description = $"Khách hàng {d.TenKhachHang ?? "ẩn danh"} vừa đặt mua.",
                            Time = d.NgayDatHang ?? DateTime.Now,
                            Link = $"/DonHangs/Details/{d.MaDonHang}"
                        })
                        .ToListAsync();

                    var newRequests = await _context.YeuCauLapDats
                        .Where(y => y.TrangThaiLapDat == "Chờ báo giá")
                        .OrderByDescending(y => y.NgayLap)
                        .Take(5)
                        .Select(y => new NotificationItem
                        {
                            Id = y.MaYeuCauLapDat,
                            Type = "Request",
                            Title = $"Yêu cầu lắp đặt #{y.MaYeuCauLapDat}",
                            Description = "Cần khảo sát và báo giá thi công.",
                            Time = y.NgayLap ?? DateTime.Now,
                            Link = $"/YeuCauLapDats/Edit/{y.MaYeuCauLapDat}"
                        })
                        .ToListAsync();

                    var allNotifications = newOrders.Concat(newRequests)
                        .OrderByDescending(n => n.Time)
                        .Take(10)
                        .ToList();

                    // TRẢ VỀ FILE DEFAULT.CSHTML CHO ADMIN
                    return View("Default", allNotifications);
                }
                else
                {
                    // =======================================================
                    // LOGIC KHÁCH HÀNG 
                    // =======================================================
                    string customerPhone = Request.Cookies["VerifiedPhone"] ?? "";

                    if (string.IsNullOrEmpty(customerPhone) && User.Identity != null && User.Identity.IsAuthenticated)
                    {
                        var user = await _context.KhachHangs.FirstOrDefaultAsync(k => k.Email == User.Identity.Name || k.SoDienThoai == User.Identity.Name);
                        customerPhone = user?.SoDienThoai ?? "";
                    }

                    if (string.IsNullOrEmpty(customerPhone))
                    {
                        return View("Customer", new List<NotificationItem>());
                    }

                    var customerOrders = await _context.DonHangs
                        .Where(d => d.SoDienThoai == customerPhone)
                        .OrderByDescending(d => d.NgayDatHang)
                        .Take(5)
                        .Select(d => new NotificationItem
                        {
                            Id = d.MaDonHang,
                            Type = d.TrangThaiDonHang,
                            Title = $"Đơn hàng #{d.MaDonHang}",
                            Description = $"Trạng thái: {d.TrangThaiDonHang}. Nhấn để xem chi tiết.",
                            Time = d.NgayDatHang ?? DateTime.Now,
                            Link = $"/Customers/OrderHistory?phone={customerPhone}#order-{d.MaDonHang}"
                        })
                        .ToListAsync();

                    // TRẢ VỀ FILE CUSTOMER.CSHTML CHO KHÁCH HÀNG
                    return View("Customer", customerOrders);
                }
            }
        }
    }

    public class NotificationItem
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Time { get; set; }
        public string Link { get; set; }

        public string GetTimeAgo()
        {
            TimeSpan timeSince = DateTime.Now.Subtract(Time);
            if (timeSince.TotalMinutes < 1) return "Vừa xong";
            if (timeSince.TotalMinutes < 60) return $"{(int)timeSince.TotalMinutes} phút trước";
            if (timeSince.TotalHours < 24) return $"{(int)timeSince.TotalHours} giờ trước";
            return $"{(int)timeSince.TotalDays} ngày trước";
        }
    }
}