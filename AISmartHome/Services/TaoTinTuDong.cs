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

            var feed = await FeedReader.ReadAsync(url);

            foreach (var item in feed.Items)
            {
                danhSach.Add(new BaiViet
                {
                    TieuDe = item.Title,
                    
                    NoiDung = System.Text.RegularExpressions.Regex.Replace(item.Description, "<.*?>", string.Empty),
                    NgayDang = item.PublishingDate ?? DateTime.Now,
                    HinhAnh = "news-default.jpg",
                    IsApproved = false, 
                    NguonTin = item.Link, 
                    MaDanhMucBaiViet = 1, 
                    MaTaiKhoan = 1       
                });
            }

            return danhSach;
        }
    }
}