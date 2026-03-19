using System.Collections.Generic;

namespace AISmartHome.Models
{
    public class HomeViewModel
    {
        public IEnumerable<SanPham> SanPhams { get; set; }

        public IEnumerable<DanhMucSanPham> DanhMucs { get; set; }
    }
}