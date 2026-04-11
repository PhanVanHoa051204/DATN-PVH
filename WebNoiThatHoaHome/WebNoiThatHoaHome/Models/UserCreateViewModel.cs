using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace WebNoiThatHoaHome.Models
{
    public class UserCreateViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        public string? Password { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn vai trò")]
        public int RoleId { get; set; }

        public List<SelectListItem>? Roles { get; set; }
    }
}