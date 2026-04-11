using System.ComponentModel.DataAnnotations;

namespace WebNoiThatHoaHome.Models
{
    public class CategoryViewModel
    {
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên danh mục")]
        [StringLength(100, ErrorMessage = "Tên danh mục không được vượt quá 100 ký tự")]
        public string? CategoryName { get; set; }

        public string? Description { get; set; }
    }
}