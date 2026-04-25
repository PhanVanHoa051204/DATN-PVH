using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Controllers
{
    public class AppointmentController : Controller
    {
        private readonly HoaHomeDbContext _context;

        public AppointmentController(HoaHomeDbContext context)
        {
            _context = context;
        }

        // HIỂN THỊ FORM ĐẶT LỊCH 
        [HttpGet]
        public async Task<IActionResult> Booking(int? serviceId)
        {
            // Lấy danh sách dịch vụ ném ra Dropdown
            var services = await _context.ServiceTypes
                .Where(s => s.IsDeleted != true)
                .Select(s => new SelectListItem
                {
                    Value = s.ServiceTypeId.ToString(),
                    Text = s.TypeName
                }).ToListAsync();

            ViewBag.Services = services;

            // TẠO THỜI GIAN MẶC ĐỊNH 
            var tomorrow = DateTime.Now.AddDays(1);
            var cleanDate = new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, tomorrow.Hour, tomorrow.Minute, 0); 

            var model = new Appointment
            {
                ServiceTypeId = serviceId,
                AppointmentDate = cleanDate 
            };

            return View(model);
        }

        // XỬ LÝ LƯU ĐẶT LỊCH XUỐNG DATABASE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Booking(Appointment model)
        {
            if (ModelState.IsValid)
            {
                // lấy ID CỦA KHÁCH ĐANG ĐĂNG NHẬP ĐỂ LƯU VÀO ĐƠN
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (!string.IsNullOrEmpty(userIdString))
                {
                    model.CustomerId = int.Parse(userIdString); // Gắn ID của khách vào lịch hẹn
                }
                else
                {
                    // Trúng trường hợp khách chưa đăng nhập mà mò vào Đặt lịch
                    TempData["LoginError"] = "Vui lòng đăng nhập để đặt lịch dịch vụ!";
                    return RedirectToAction("Login", "Account"); // Chuyển về trang đăng nhập
                }

                model.Status = "Pending"; // Trạng thái "Chờ duyệt"
                model.CreatedAt = DateTime.Now;
                model.UpdatedAt = DateTime.Now;

                _context.Appointments.Add(model);
                await _context.SaveChangesAsync();

                TempData["SuccessMsg"] = "Đặt lịch thành công! Kỹ thuật viên sẽ liên hệ với bạn trong thời gian sớm nhất.";
                return RedirectToAction("Booking");
            }

            // Nếu form có lỗi, nạp lại Dropdown
            ViewBag.Services = await _context.ServiceTypes
                .Where(s => s.IsDeleted != true)
                .Select(s => new SelectListItem { Value = s.ServiceTypeId.ToString(), Text = s.TypeName })
                .ToListAsync();

            return View(model);
        }
        // HỦY LỊCH HẸN CHỈ CHO HỦY KHI CHƯA ĐƯỢC DUYỆT
        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            // Lấy ID khách đang đăng nhập từ Claims
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");
            int customerId = int.Parse(userIdString);

            // Tìm lịch hẹn phải đúng ID và đúng của Khách đó thì mới cho hủy 
            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == id && a.CustomerId == customerId);

            if (appointment != null)
            {
                // Chỉ cho hủy nếu đang ở trạng thái Pending (Chờ duyệt)
                if (appointment.Status == "Pending")
                {
                    appointment.Status = "Cancelled"; // Đổi trạng thái thành Đã hủy
                    appointment.UpdatedAt = DateTime.Now;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMsg"] = "Đã hủy lịch hẹn thành công.";
                }
                else
                {
                    TempData["ErrorMsg"] = "Không thể hủy lịch hẹn đã được xác nhận hoặc hoàn thành.";
                }
            }

            return RedirectToAction("Appointments", "Account");
        }
    }
}