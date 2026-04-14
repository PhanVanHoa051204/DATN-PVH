using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AppointmentController : Controller
    {
        private readonly HoaHomeDbContext _context;

        public AppointmentController(HoaHomeDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. DANH SÁCH LỊCH HẸN (CHỈ HIỆN ĐƠN CHƯA XÓA)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var appointments = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.ServiceType)
                .Include(a => a.Employee).ThenInclude(e => e.User)
                .Where(a => a.Status != "Deleted") // Lọc bỏ đơn đã xóa mềm
                .OrderByDescending(a => a.CreatedAt) // Hiện đơn mới đặt lên đầu
                .ToListAsync();

            // Chuẩn bị danh sách Nhân viên để điều phối
            ViewBag.Employees = await _context.Employees
                .Include(e => e.User)
                .Where(e => e.IsDeleted != true && e.Status != "OnLeave")
                .Select(e => new SelectListItem
                {
                    Value = e.EmployeeId.ToString(),
                    Text = $"{e.User.FullName} ({e.Specialization})"
                }).ToListAsync();

            return View(appointments);
        }

        // ==========================================
        // 2. CẬP NHẬT NHANH (ĐIỀU PHỐI & TRẠNG THÁI)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> QuickUpdate(int AppointmentId, int? EmployeeId, string Status)
        {
            var appointment = await _context.Appointments.FindAsync(AppointmentId);
            if (appointment == null) return NotFound();

            appointment.EmployeeId = EmployeeId;
            appointment.Status = Status;
            appointment.UpdatedAt = DateTime.Now;

            // Auto-assign: Nếu có thợ thì chuyển sang "Assigned"
            if (EmployeeId.HasValue && (Status == "Pending" || string.IsNullOrEmpty(Status)))
            {
                appointment.Status = "Assigned";
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMsg"] = $"Đã cập nhật lịch hẹn #{AppointmentId} thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 3. LỆNH XÓA MỀM (CHO VÀO THÙNG RÁC)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment != null)
            {
                appointment.Status = "Deleted"; // Đánh dấu xóa
                appointment.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = $"Đã đưa lịch hẹn #{id} vào danh sách lưu trữ!";
            }
            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 4. TRANG THÙNG RÁC (LỊCH HẸN ĐÃ XÓA)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Trash()
        {
            var deletedAppointments = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.ServiceType)
                .Where(a => a.Status == "Deleted")
                .OrderByDescending(a => a.UpdatedAt)
                .ToListAsync();

            return View(deletedAppointments);
        }

        // ==========================================
        // 5. KHÔI PHỤC LỊCH HẸN TỪ THÙNG RÁC
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken] // <--- Nếu có dòng này ở Controller thì View BẮT BUỘC phải có @Html.AntiForgeryToken()
        public async Task<IActionResult> Restore(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment != null)
            {
                // Khôi phục trạng thái
                appointment.Status = appointment.EmployeeId.HasValue ? "Assigned" : "Pending";
                appointment.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = $"Đã khôi phục thành công lịch hẹn #{id}!";
            }
            return RedirectToAction(nameof(Trash));
        }
    }
}