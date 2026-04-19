using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Models;
public class Voucher
{
    [Key]
    public int VoucherId { get; set; }
    public string Code { get; set; }
    public string DiscountType { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountValue { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal MinOrderValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; }

    public virtual ICollection<Order>? Orders { get; set; }
}