using System;
using System.Collections.Generic;

namespace WebNoiThatHoaHome.Models;

public partial class PaymentLog
{
    public int LogId { get; set; }

    public int? OrderId { get; set; }

    public string? Method { get; set; }

    public decimal? Amount { get; set; }

    public string? ResponseCode { get; set; }

    public string? Message { get; set; }

    public string? RawData { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Order? Order { get; set; }
}
