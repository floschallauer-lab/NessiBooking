using CourseBooking.Domain.Common;
using CourseBooking.Domain.Enums;

namespace CourseBooking.Domain.Entities;

public sealed class CourseOffering : EntityBase
{
    public Guid CourseCategoryId { get; set; }
    public CourseCategory? CourseCategory { get; set; }

    public Guid CourseTypeId { get; set; }
    public CourseType? CourseType { get; set; }

    public Guid VenueId { get; set; }
    public Venue? Venue { get; set; }

    public Guid? CourseCycleId { get; set; }
    public CourseCycle? CourseCycle { get; set; }

    public Guid? AgeRuleId { get; set; }
    public AgeRule? AgeRule { get; set; }

    public Guid? CourseInstructorId { get; set; }
    public CourseInstructor? CourseInstructor { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string CustomerNotice { get; set; } = string.Empty;
    public string InternalNotes { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public CourseOfferingStatus Status { get; set; } = CourseOfferingStatus.Published;
    public CourseRegistrationMode RegistrationMode { get; set; } = CourseRegistrationMode.Internal;
    public bool AllowWaitlistWhenFull { get; set; } = true;
    public string? ExternalRegistrationUrl { get; set; }

    public ICollection<RegistrationPriority> RegistrationPriorities { get; set; } = new List<RegistrationPriority>();
    public ICollection<Registration> AssignedRegistrations { get; set; } = new List<Registration>();
    public ICollection<WaitlistEntry> WaitlistEntries { get; set; } = new List<WaitlistEntry>();

    public bool IsExternallyManaged => RegistrationMode == CourseRegistrationMode.External;

    public int SeatsRemaining(int occupiedSeats) => Math.Max(Capacity - occupiedSeats, 0);
}
