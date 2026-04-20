using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebNoiThatHoaHome.Models;
using Microsoft.EntityFrameworkCore;

namespace WebNoiThatHoaHome.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReviewController : Controller
    {
        private readonly HoaHomeDbContext _context;
        public ReviewController(HoaHomeDbContext context) => _context = context;

        // chức năng Lọc và Tìm kiếm
        public async Task<IActionResult> Index(string searchString, int? ratingFilter)
        {
            var reviews = _context.ProductReviews
                .Include(r => r.Product)
                .Include(r => r.User)
                .AsQueryable();
            //Lọc theo chữ (Tên khách, Tên SP, hoặc Nội dung)
            if (!string.IsNullOrEmpty(searchString))
            {
                reviews = reviews.Where(r =>
                    (r.User != null && r.User.FullName.Contains(searchString)) ||
                    (r.Product != null && r.Product.ProductName.Contains(searchString)) ||
                    r.Comment.Contains(searchString));
            }
            // Lọc theo số sao
            if (ratingFilter.HasValue && ratingFilter.Value > 0)
            {
                reviews = reviews.Where(r => r.Rating == ratingFilter.Value);
            }
            // Lưu lại giá trị để hiển thị ra ô input ở View
            ViewBag.SearchString = searchString;
            ViewBag.RatingFilter = ratingFilter;

            var result = await reviews.OrderByDescending(r => r.CreatedAt).ToListAsync();
            return View(result);
        }
        // Duyệt đánh giá 
        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var review = await _context.ProductReviews.FindAsync(id);
            if (review != null)
            {
                review.IsApproved = true;
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = "Đã duyệt đánh giá thành công!"; 
            }
            return RedirectToAction("Index");
        }
        // Admin trả lời đánh giá
        [HttpPost]
        public async Task<IActionResult> Reply(int id, string adminReply)
        {
            var review = await _context.ProductReviews.FindAsync(id);
            if (review != null)
            {
                review.AdminReply = adminReply;
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = "Đã gửi phản hồi cho khách hàng thành công!";
            }
            return RedirectToAction("Index");
        }
    }
}