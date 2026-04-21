using Domain.Enums;

namespace Domain.Entities;

public class UserSkill
{
    public Guid AccountID { get; set; }
    public Guid SkillID { get; set; }
    public SkillLevel SkillLevel { get; set; }
    public string? Description { get; set; }
    public string? LearnedAt { get; set; }
    public bool IsVerified { get; set; }

    // Navigation properties
    public Account Account { get; set; } = null!;
    public SkillsCatalog SkillsCatalog { get; set; } = null!;
}

