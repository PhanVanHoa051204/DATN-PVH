using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Controllers
{
    public class WishlistController : Controller
    {
        private readonly HoaHomeDbContext _context;

        public WishlistController(HoaHomeDbContext context)
        {
            _context = context;
        }
        // 1. Hàm xử lý AJAX (Thêm/Bỏ yêu thích)
        [HttpPost]
        public async Task<IActionResult> ToggleWishlist(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
            {
                return Json(new { success = false, message = "Not logged in" });
            }

            int userId = int.Parse(userIdString);
            // Tìm xem tim này đã có trong DB chưa
            var existingItem = await _context.Wishlists.FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == id);

            // 👇 THÊM BIẾN ĐỂ LƯU TỔNG SỐ LƯỢNG TIM HIỆN TẠI
            int totalWishlistCount = 0;

            if (existingItem != null)
            {
                // NẾU CÓ Bấm vào là Xóa tim
                _context.Wishlists.Remove(existingItem);
                await _context.SaveChangesAsync();

                // 👇 ĐẾM LẠI TỔNG SỐ SAU KHI XÓA
                totalWishlistCount = await _context.Wishlists.CountAsync(w => w.UserId == userId);

                // 👇 Báo về cho JS: Thành công BỎ TIM và TRẢ VỀ CON SỐ MỚI (totalCount)
                return Json(new { success = true, isAdded = false, totalCount = totalWishlistCount });
            }
            else
            {
                // NẾU CHƯA CÓ -> Thêm tim mới
                var newWishlist = new Wishlist { UserId = userId, ProductId = id };
                _context.Wishlists.Add(newWishlist);
                await _context.SaveChangesAsync();

                // 👇 ĐẾM LẠI TỔNG SỐ SAU KHI THÊM
                totalWishlistCount = await _context.Wishlists.CountAsync(w => w.UserId == userId);

                // 👇 Báo về cho JS: Thành công THÊM TIM và TRẢ VỀ CON SỐ MỚI (totalCount)
                return Json(new { success = true, isAdded = true, totalCount = totalWishlistCount });
            }
        }
        // 2. Trang hiển thị danh sách yêu thích của khách hàng
        public async Task<IActionResult> Index()
        {
            // ĐÃ SỬA: Đồng bộ cách lấy ID giống hệt hàm ở trên để không bị lỗi phiên làm việc (Session)
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");

            int userId = int.Parse(userIdString);

            var myWishlist = await _context.Wishlists
                .Include(w => w.Product)
                .ThenInclude(p => p.ProductImages)
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.AddedDate)
                .Select(w => new ProductListViewModel
                {
                    ProductId = w.Product.ProductId,
                    ProductName = w.Product.ProductName,
                    Price = w.Product.Price,
                    // Tối ưu lại cấu trúc lấy ảnh để chống lỗi văng app khi sản phẩm chưa có ảnh nào
                    MainImageUrl = w.Product.ProductImages.Where(i => i.IsMain == true).Select(i => i.ImageUrl).FirstOrDefault() ?? "/images/no-image.png"
                })
                .ToListAsync();

            ViewData["CategoryName"] = "Danh sách yêu thích của tôi";
            // Tái sử dụng giao diện Category.cshtml siêu đẹp của bạn để hiển thị
            return View("~/Views/Product/Category.cshtml", myWishlist);
        }
        [HttpGet]
        public async Task<IActionResult> GetWishlistCount()
        {
            // Kiểm tra xem khách đã đăng nhập chưa
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
            {
                return Json(0); // Chưa đăng nhập thì mặc định là 0
            }

            int userId = int.Parse(userIdString);

            // Đếm tổng số sản phẩm yêu thích của khách này
            int count = await _context.Wishlists.CountAsync(w => w.UserId == userId);

            return Json(count); // Trả con số về cho giao diện
        }
    }
}