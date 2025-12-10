namespace Domain.Entities;

public class Account
{
    public Guid AccountID { get; set; }
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? TelegramID { get; set; }
    public bool IsAdmin { get; set; } = false;
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public UserProfile? UserProfile { get; set; }
    public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
    public ICollection<Education> Educations { get; set; } = new List<Education>();
    public ICollection<Proof> Proofs { get; set; } = new List<Proof>();
    public ICollection<SkillOffer> SkillOffers { get; set; } = new List<SkillOffer>();
    public ICollection<SkillRequest> SkillRequests { get; set; } = new List<SkillRequest>();
    public ICollection<VerificationRequest> VerificationRequests { get; set; } = new List<VerificationRequest>();
}


