using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models; // Đổi lại đúng namespace của Sếp nhé

namespace WebNoiThatHoaHome.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] // Bắt buộc là Admin
    public class ServiceTypeController : Controller
    {
        private readonly HoaHomeDbContext _context;

        public ServiceTypeController(HoaHomeDbContext context)
        {
            _context = context;
        }

        // 1. HIỂN THỊ DANH SÁCH DỊCH VỤ
        public async Task<IActionResult> Index()
        {
            // BÍ QUYẾT Ở DÒNG WHERE NÀY ĐÂY SẾP: 
            // Chỉ lấy ra những dịch vụ có IsDeleted là false (hoặc null do chưa bị động tới bao giờ)
            var services = await _context.ServiceTypes
                                         .Where(s => s.IsDeleted == false || s.IsDeleted == null)
                                         .ToListAsync();

            return View(services);
        }

        // 2. LƯU DỊCH VỤ MỚI (Xử lý form Thêm)
        [HttpPost]
        public async Task<IActionResult> Create(ServiceType model)
        {
            if (ModelState.IsValid)
            {
                model.IsDeleted = false;
                _context.ServiceTypes.Add(model);
                await _context.SaveChangesAsync();

                TempData["SuccessMsg"] = "Đã thêm loại dịch vụ mới thành công!";
                return RedirectToAction("Index");
            }
            TempData["ErrorMsg"] = "Lỗi: Vui lòng kiểm tra lại thông tin!";
            return RedirectToAction("Index");
        }

        // 3. XÓA MỀM DỊCH VỤ
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var service = await _context.ServiceTypes.FindAsync(id);
            if (service != null)
            {
                service.IsDeleted = true; // Chỉ đánh dấu xóa, không xóa thật trong CSDL
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = "Đã xóa dịch vụ!";
            }
            return RedirectToAction("Index");
        }
        // 4. CẬP NHẬT DỊCH VỤ (Chỉnh sửa)
        [HttpPost]
        public async Task<IActionResult> Edit(ServiceType model)
        {
            if (ModelState.IsValid)
            {
                // Tìm dịch vụ cũ trong Database
                var service = await _context.ServiceTypes.FindAsync(model.ServiceTypeId);
                if (service != null)
                {
                    // Cập nhật thông tin mới
                    service.TypeName = model.TypeName;
                    service.BasePrice = model.BasePrice;
                    service.Description = model.Description;

                    // Lưu ý: Không đụng đến biến IsDeleted ở đây

                    await _context.SaveChangesAsync();
                    TempData["SuccessMsg"] = "Đã cập nhật thông tin dịch vụ thành công!";
                    return RedirectToAction("Index");
                }
            }
            TempData["ErrorMsg"] = "Lỗi: Không thể cập nhật, vui lòng kiểm tra lại!";
            return RedirectToAction("Index");
        }
        // 5. HIỂN THỊ THÙNG RÁC (Chỉ lấy những dịch vụ đã bị xóa mềm)
        public async Task<IActionResult> Trash()
        {
            var trashedServices = await _context.ServiceTypes
                                                .Where(s => s.IsDeleted == true)
                                                .ToListAsync();
            return View(trashedServices);
        }

        // 6. KHÔI PHỤC DỊCH VỤ
        [HttpPost]
        public async Task<IActionResult> Restore(int id)
        {
            var service = await _context.ServiceTypes.FindAsync(id);
            if (service != null && service.IsDeleted == true)
            {
                service.IsDeleted = false; // Đổi trạng thái về lại như cũ
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = "Đã khôi phục dịch vụ thành công!";
            }
            else
            {
                TempData["ErrorMsg"] = "Lỗi: Không tìm thấy dịch vụ để khôi phục!";
            }

            // Khôi phục xong thì cho load lại trang Thùng rác
            return RedirectToAction("Trash");
        }
    }
}