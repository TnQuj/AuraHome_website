using CodeHollow.FeedReader;
using AISmartHome.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AISmartHome.Services
{
    public class TaoTinTuDong
    {
        public async Task<List<BaiViet>> LayTinTuRssAsync(string url)
        {
            var danhSach = new List<BaiViet>();

            // Đọc dữ liệu từ đường dẫn RSS
            var feed = await FeedReader.ReadAsync(url);

            foreach (var item in feed.Items)
            {
                danhSach.Add(new BaiViet
                {
                    TieuDe = item.Title,
                    // Description thường chứa nội dung tóm tắt của bài báo
                    NoiDung = item.Description,
                    NgayDang = item.PublishingDate ?? DateTime.Now,
                    HinhAnh = "news-default.jpg", // Tạm thời để ảnh mặc định
                    IsApproved = false // Chờ Admin duyệt mới hiện lên trang chủ
                });
            }

            return danhSach;
        }
    }
}