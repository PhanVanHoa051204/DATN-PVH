using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Controllers // Chú ý: Không có chữ .Areas.Admin
{
    public class ServiceController : Controller
    {
        private readonly HoaHomeDbContext _context;

        public ServiceController(HoaHomeDbContext context)
        {
            _context = context;
        }

        // Hiện chi tiết một Dịch vụ
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            // 1. Lấy dịch vụ hiện tại (để hiển thị nội dung bên phải)
            var service = await _context.ServiceTypes
                .FirstOrDefaultAsync(s => s.ServiceTypeId == id && s.IsDeleted != true);

            if (service == null)
            {
                return NotFound("Dịch vụ này không tồn tại hoặc đã bị gỡ bỏ.");
            }

            // 2. LẤY TẤT CẢ DỊCH VỤ (để làm Menu dọc bên trái)
            ViewBag.AllServices = await _context.ServiceTypes
                .Where(s => s.IsDeleted != true)
                .ToListAsync();

            return View(service);
        }
    }
}