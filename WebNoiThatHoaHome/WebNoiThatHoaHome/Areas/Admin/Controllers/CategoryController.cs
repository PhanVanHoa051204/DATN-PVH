using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CategoryController : Controller
    {
        private readonly HoaHomeDbContext _context;

        public CategoryController(HoaHomeDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. DANH SÁCH DANH MỤC
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentSearch"] = searchString;

            // Lấy các danh mục chưa bị xóa (IsDeleted == false hoặc null)
            var query = _context.Categories.Where(c => c.IsDeleted == false || c.IsDeleted == null).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                // Áp dụng bài học tìm kiếm: Cắt khoảng trắng và đưa về chữ thường
                string keyword = searchString.Trim().ToLower();
                query = query.Where(c => c.CategoryName.ToLower().Contains(keyword));
            }

            var categories = await query.OrderBy(c => c.CategoryId).ToListAsync();
            return View(categories);
        }

        // ==========================================
        // 2. THÊM MỚI
        // ==========================================
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(CategoryViewModel model)
        {
            if (ModelState.IsValid)
            {
                var category = new Category
                {
                    CategoryName = model.CategoryName,
                    Description = model.Description,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsDeleted = false
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                TempData["SuccessMsg"] = "Đã thêm danh mục mới thành công!";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // ==========================================
        // 3. CHỈNH SỬA
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null || category.IsDeleted == true) return NotFound();

            var model = new CategoryViewModel
            {
                CategoryId = category.CategoryId,
                CategoryName = category.CategoryName,
                Description = category.Description
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CategoryViewModel model)
        {
            if (ModelState.IsValid)
            {
                var category = await _context.Categories.FindAsync(model.CategoryId);
                if (category == null) return NotFound();

                category.CategoryName = model.CategoryName;
                category.Description = model.Description;
                category.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = "Cập nhật danh mục thành công!";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // ==========================================
        // 4. XÓA MỀM (Chuyển vào Thùng rác)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                category.IsDeleted = true; // Đánh dấu đã xóa
                category.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                
                TempData["SuccessMsg"] = $"Đã chuyển danh mục [{category.CategoryName}] vào thùng rác!";
            }
            return RedirectToAction("Index");
        }

        // ==========================================
        // 5. TRANG THÙNG RÁC
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Trash()
        {
            // Chỉ lấy những danh mục ĐÃ BỊ XÓA (IsDeleted == true)
            var deletedCategories = await _context.Categories
                                        .Where(c => c.IsDeleted == true)
                                        .OrderByDescending(c => c.UpdatedAt) // Xóa gần nhất xếp lên đầu
                                        .ToListAsync();
            return View(deletedCategories);
        }

        // ==========================================
        // 6. KHÔI PHỤC TỪ THÙNG RÁC
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Restore(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                category.IsDeleted = false; // Bỏ đánh dấu xóa
                category.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = $"Đã khôi phục danh mục [{category.CategoryName}] thành công!";
            }
            // Sau khi khôi phục xong thì tải lại trang Thùng rác
            return RedirectToAction("Trash");
        }
    }
}