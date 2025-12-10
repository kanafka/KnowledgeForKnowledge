namespace Domain.Entities;

public class Education
{
    public Guid EducationID { get; set; }
    public Guid AccountID { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
    public string? DegreeField { get; set; }
    public int? YearCompleted { get; set; }

    // Navigation property
    public Account Account { get; set; } = null!;
}


