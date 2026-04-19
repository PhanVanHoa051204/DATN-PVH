using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Controllers;

public class VoucherController : Controller
{
    private readonly HoaHomeDbContext _context;

    public VoucherController(HoaHomeDbContext context) => _context = context;

    // Hiển thị danh sách mã cho khách lấy
    public async Task<IActionResult> Index()
    {
        // Điều kiện: Đang Active + Chưa hết hạn + (Không giới hạn lượt HOẶC Số lượt dùng < Giới hạn)
        var activeVouchers = await _context.Vouchers
            .Where(v => v.IsActive
                     && v.EndDate > DateTime.Now
                     && (v.UsageLimit == 0 || v.UsedCount < v.UsageLimit))
            .OrderBy(v => v.EndDate) // Mã nào sắp hết hạn thì cho lên đầu
            .ToListAsync();

        return View(activeVouchers);
    }
}