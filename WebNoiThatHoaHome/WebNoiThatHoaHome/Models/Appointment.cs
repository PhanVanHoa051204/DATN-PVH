using System;
using System.Collections.Generic;

namespace WebNoiThatHoaHome.Models;

public partial class Appointment
{
    public int AppointmentId { get; set; }

    public int? CustomerId { get; set; }

    public int? EmployeeId { get; set; }

    public int? ServiceTypeId { get; set; }

    public DateTime AppointmentDate { get; set; }

    public DateTime? EndTime { get; set; }

    public string? CustomerNote { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User? Customer { get; set; }

    public virtual Employee? Employee { get; set; }

    public virtual ServiceType? ServiceType { get; set; }
}
