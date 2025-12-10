using Domain.Enums;

namespace Domain.Entities;

public class VerificationRequest
{
    public Guid RequestID { get; set; }
    public Guid AccountID { get; set; }
    public Guid? ProofID { get; set; }
    public VerificationRequestType RequestType { get; set; }
    public VerificationStatus Status { get; set; }

    // Navigation properties
    public Account Account { get; set; } = null!;
    public Proof? Proof { get; set; }
}


