using Domain.Enums;

namespace Domain.Entities;

public class SkillsCatalog
{
    public Guid SkillID { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public SkillEpithet Epithet { get; set; }

    // Navigation properties
    public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
    public ICollection<SkillOffer> SkillOffers { get; set; } = new List<SkillOffer>();
    public ICollection<SkillRequest> SkillRequests { get; set; } = new List<SkillRequest>();
}


