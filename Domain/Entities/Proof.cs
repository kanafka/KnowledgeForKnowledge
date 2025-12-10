namespace Domain.Entities;

public class Proof
{
    public Guid ProofID { get; set; }
    public Guid AccountID { get; set; }
    public Guid? SkillID { get; set; }
    public string FileURL { get; set; } = string.Empty;
    public bool IsVerified { get; set; }

    // Navigation properties
    public Account Account { get; set; } = null!;
    public SkillsCatalog? SkillsCatalog { get; set; }
    public ICollection<VerificationRequest> VerificationRequests { get; set; } = new List<VerificationRequest>();
}


