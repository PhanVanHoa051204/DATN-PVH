using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace WebNoiThatHoaHome.Models
{
    public class ProductEditViewModel
    {
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
        public string? ProductName { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn danh mục")]
        public int CategoryId { get; set; }
        public List<SelectListItem>? Categories { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá bán")]
        public decimal Price { get; set; }

        public int StockQuantity { get; set; }
        public string? Dimensions { get; set; }
        public string? Material { get; set; }
        public string? Description { get; set; }

        // Thêm biến này để bật/tắt trạng thái Đang bán - Tạm ẩn
        public bool IsActive { get; set; }
    }
}