using CourseBooking.Domain.Common;
using CourseBooking.Domain.Enums;

namespace CourseBooking.Domain.Entities;

public sealed class Registration : EntityBase
{
    public Guid GuardianId { get; set; }
    public Guardian? Guardian { get; set; }

    public Guid ChildParticipantId { get; set; }
    public ChildParticipant? ChildParticipant { get; set; }

    public Guid? PreferredCourseCycleId { get; set; }
    public CourseCycle? PreferredCourseCycle { get; set; }

    public Guid? AssignedCourseOfferingId { get; set; }
    public CourseOffering? AssignedCourseOffering { get; set; }

    public RegistrationStatus Status { get; set; } = RegistrationStatus.Received;
    public RegistrationSource Source { get; set; } = RegistrationSource.PublicForm;
    public bool TermsAccepted { get; set; }
    public bool PrivacyAccepted { get; set; }
    public string Note { get; set; } = string.Empty;
    public string AdminNotes { get; set; } = string.Empty;
    public string AssignmentProtocol { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastStatusChangedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<RegistrationPriority> Priorities { get; set; } = new List<RegistrationPriority>();
    public ICollection<WaitlistEntry> WaitlistEntries { get; set; } = new List<WaitlistEntry>();
}
