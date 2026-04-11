using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace WebNoiThatHoaHome.Models
{
    public class ProductCreateViewModel
    {
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

        // BÍ QUYẾT Ở ĐÂY: Hứng danh sách file ảnh tải lên
        public List<IFormFile>? UploadedImages { get; set; }
    }
}