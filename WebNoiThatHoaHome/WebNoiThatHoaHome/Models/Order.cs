using System;
using System.Collections.Generic;

namespace WebNoiThatHoaHome.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public int? UserId { get; set; }

    public DateTime? OrderDate { get; set; }

    public decimal? TotalAmount { get; set; }

    public decimal? ShippingFee { get; set; }

    public string? ShippingAddress { get; set; }

    public string? CustomerNote { get; set; }

    public string? PaymentMethod { get; set; }

    public string? PaymentStatus { get; set; }

    public string? OrderStatus { get; set; }

    public string? VnpayTranNo { get; set; }

    public string? VnpayOrderCode { get; set; }

    public DateTime? PayDate { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<PaymentLog> PaymentLogs { get; set; } = new List<PaymentLog>();

    public virtual User? User { get; set; }
}
