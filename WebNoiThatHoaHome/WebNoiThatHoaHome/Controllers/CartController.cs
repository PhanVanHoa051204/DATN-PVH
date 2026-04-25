using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Controllers
{
    public class CartController : Controller
    {
        private readonly HoaHomeDbContext _context;

        public CartController(HoaHomeDbContext context)
        {
            _context = context;
        }

        // Hàm xử lý AJAX thêm sản phẩm vào giỏ
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity)
        {
            // Kiểm tra xem khách đã đăng nhập chưa bằng Identity
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
            {
                return Json(new { success = false, message = "Not logged in" });
            }
            int userId = int.Parse(userIdString);

            // Đảm bảo số lượng gửi lên phải lớn hơn 0
            if (quantity <= 0)
            {
                return Json(new { success = false, message = "Số lượng không hợp lệ!" });
            }

            // Kiểm tra sản phẩm có thật sự tồn tại trong DB không
            var product = await _context.Products.FindAsync(productId);
            if (product == null || product.IsDeleted == true || product.IsActive == false)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại hoặc đã ngừng kinh doanh." });
            }

            // Tìm Giỏ hàng của người dùng này. Nếu chưa có thì tạo mới.
            var cart = await _context.Carts
                .Include(c => c.CartItems) 
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync(); 
            }

            // Kiểm tra xem sản phẩm khách muốn mua đã có trong giỏ chưa
            var cartItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);

            if (cartItem != null)
            {
                //  Kiểm tra xem tổng số lượng mua có lố kho không nếu dã có trong giỏ
                if (product.StockQuantity.HasValue && (cartItem.Quantity + quantity) > product.StockQuantity.Value)
                {
                    return Json(new { success = false, message = $"Bạn đã có {cartItem.Quantity} sản phẩm này trong giỏ. Kho chỉ còn tổng {product.StockQuantity.Value} sản phẩm!" });
                }
                cartItem.Quantity += quantity;
            }
            else
            {
                //  Kiểm tra số lượng mua mới có lố kho không nếu chưa có trong giỏ
                if (product.StockQuantity.HasValue && quantity > product.StockQuantity.Value)
                {
                    return Json(new { success = false, message = $"Kho hiện tại chỉ còn {product.StockQuantity.Value} sản phẩm!" });
                }
                var newCartItem = new CartItem
                {
                    CartId = cart.CartId,
                    ProductId = productId,
                    Quantity = quantity
                };
                _context.CartItems.Add(newCartItem);
            }

            // Cập nhật lại thời gian giỏ hàng có biến động
            cart.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // Hàm hiển thị trang Giỏ hàng
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Kiểm tra đăng nhập
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");
            int userId = int.Parse(userIdString);

            // Kéo dữ liệu Giỏ hàng của người dùng
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.ProductImages)
                .FirstOrDefaultAsync(c => c.UserId == userId);
            return View(cart ?? new Cart { CartItems = new List<CartItem>() });
        }

        // 2. Hàm Cập nhật số lượng 
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
        {
            var cartItem = await _context.CartItems.Include(ci => ci.Product).FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId);
            if (cartItem != null)
            {
                // Kiểm tra kho (nếu số lượng tăng lên)
                if (quantity > cartItem.Quantity && cartItem.Product.StockQuantity.HasValue && quantity > cartItem.Product.StockQuantity.Value)
                {
                    return Json(new { success = false, message = $"Kho chỉ còn {cartItem.Product.StockQuantity.Value} sản phẩm!" });
                }

                if (quantity > 0)
                {
                    cartItem.Quantity = quantity;
                    await _context.SaveChangesAsync();
                }
            }
            return Json(new { success = true });
        }

        // Hàm Xóa sản phẩm khỏi giỏ
        [HttpPost]
        public async Task<IActionResult> RemoveItem(int cartItemId)
        {
            var cartItem = await _context.CartItems.FindAsync(cartItemId);
            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Xóa sản phẩm thành công khỏi giỏ hàng!" });
            }
            return Json(new { success = false, message = "Lỗi: Không tìm thấy sản phẩm!" });
        }
    }
}