namespace WebNoiThatHoaHome.Models
{
    public class ProductListViewModel
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? CategoryName { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }

        // Đường dẫn của ảnh có IsMain = 1
        public string? MainImageUrl { get; set; }

        public bool IsActive { get; set; }
    }
}