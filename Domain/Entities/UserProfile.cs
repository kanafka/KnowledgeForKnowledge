namespace Domain.Entities;

public class UserProfile
{
    public Guid AccountID { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string? PhotoURL { get; set; }
    public string? ContactInfo { get; set; }
    public DateTime? LastSeenOnline { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property
    public Account Account { get; set; } = null!;
}


