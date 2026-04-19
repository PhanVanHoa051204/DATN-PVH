using System;
using System.ComponentModel.DataAnnotations;

namespace WebNoiThatHoaHome.Models;

public class ProductReview
{
    [Key] 
    public int ReviewId { get; set; }

    public int ProductId { get; set; }
    public int UserId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; }
    public string? AdminReply { get; set; }
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Product? Product { get; set; }
    public virtual User? User { get; set; }
}