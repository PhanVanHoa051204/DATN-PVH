using System;
using System.Collections.Generic;

namespace WebNoiThatHoaHome.Models;

public partial class ServiceType
{
    public int ServiceTypeId { get; set; }

    public string TypeName { get; set; } = null!;

    public decimal? BasePrice { get; set; }

    public string? Description { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
