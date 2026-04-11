using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace WebNoiThatHoaHome.Models
{
    public class UserDetailViewModel
    {
        public int UserId { get; set; } // Khớp với DB

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string? FullName { get; set; }

        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }

        public int? RoleId { get; set; } // Khớp với DB
        public List<SelectListItem>? Roles { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? NewPassword { get; set; }

      
    }
}