using System;
using System.Collections.Generic;

namespace WebNoiThatHoaHome.Models;

public partial class Product
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public int? CategoryId { get; set; }

    public decimal Price { get; set; }

    public int? StockQuantity { get; set; }

    public string? Dimensions { get; set; }

    public string? Material { get; set; }

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual Category? Category { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
    
    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
}
