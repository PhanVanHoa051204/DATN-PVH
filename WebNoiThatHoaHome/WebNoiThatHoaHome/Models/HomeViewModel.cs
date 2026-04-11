namespace WebNoiThatHoaHome.Models
{
    public class HomeViewModel
    {
        // Chứa danh sách các phòng (Phòng khách, Phòng ngủ...)
        public List<Category>? Categories { get; set; }

        // Chứa danh sách sản phẩm mới nhất
        public List<ProductListViewModel>? NewProducts { get; set; }
    }
}