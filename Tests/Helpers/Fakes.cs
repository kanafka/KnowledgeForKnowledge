using Domain.Entities;
using Domain.Enums;

namespace Tests.Helpers;

/// <summary>Фабрики тестовых сущностей</summary>
public static class Fakes
{
    public static Account Account(
        Guid? id = null,
        string login = "user@test.com",
        string passwordHash = "hash",
        string? telegramId = null,
        bool isActive = true,
        bool isAdmin = false) => new()
    {
        AccountID = id ?? Guid.NewGuid(),
        Login = login,
        PasswordHash = passwordHash,
        TelegramID = telegramId,
        IsActive = isActive,
        IsAdmin = isAdmin,
        NotificationsEnabled = true,
        CreatedAt = DateTime.UtcNow
    };

    public static UserProfile Profile(Guid accountId, string fullName = "Test User") => new()
    {
        AccountID = accountId,
        FullName = fullName,
        IsActive = true
    };

    public static SkillsCatalog Skill(Guid? id = null, string name = "Python") => new()
    {
        SkillID = id ?? Guid.NewGuid(),
        SkillName = name,
        Epithet = SkillEpithet.IT
    };

    public static SkillOffer Offer(
        Guid? id = null,
        Guid? accountId = null,
        Guid? skillId = null,
        bool isActive = true) => new()
    {
        OfferID = id ?? Guid.NewGuid(),
        AccountID = accountId ?? Guid.NewGuid(),
        SkillID = skillId ?? Guid.NewGuid(),
        Title = "Test offer",
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow
    };

    public static SkillRequest Request(
        Guid? id = null,
        Guid? accountId = null,
        Guid? skillId = null,
        RequestStatus status = RequestStatus.Open) => new()
    {
        RequestID = id ?? Guid.NewGuid(),
        AccountID = accountId ?? Guid.NewGuid(),
        SkillID = skillId ?? Guid.NewGuid(),
        Title = "Test request",
        Status = status,
        CreatedAt = DateTime.UtcNow
    };

    public static Domain.Entities.Application Application(
        Guid? id = null,
        Guid? applicantId = null,
        Guid? offerId = null,
        Guid? requestId = null,
        ApplicationStatus status = ApplicationStatus.Pending) => new()
    {
        ApplicationID = id ?? Guid.NewGuid(),
        ApplicantID = applicantId ?? Guid.NewGuid(),
        OfferID = offerId,
        SkillRequestID = requestId,
        Status = status,
        CreatedAt = DateTime.UtcNow
    };

    public static Deal Deal(
        Guid? id = null,
        Guid? applicationId = null,
        Guid? initiatorId = null,
        Guid? partnerId = null,
        DealStatus status = DealStatus.Active) => new()
    {
        DealID = id ?? Guid.NewGuid(),
        ApplicationID = applicationId ?? Guid.NewGuid(),
        InitiatorID = initiatorId ?? Guid.NewGuid(),
        PartnerID = partnerId ?? Guid.NewGuid(),
        Status = status,
        CreatedAt = DateTime.UtcNow
    };

    public static Review Review(
        Guid? id = null,
        Guid? dealId = null,
        Guid? authorId = null,
        Guid? targetId = null,
        int rating = 5) => new()
    {
        ReviewID = id ?? Guid.NewGuid(),
        DealID = dealId ?? Guid.NewGuid(),
        AuthorID = authorId ?? Guid.NewGuid(),
        TargetID = targetId ?? Guid.NewGuid(),
        Rating = rating,
        CreatedAt = DateTime.UtcNow
    };

    public static Domain.Entities.Education Education(Guid? id = null, Guid? accountId = null) => new()
    {
        EducationID = id ?? Guid.NewGuid(),
        AccountID = accountId ?? Guid.NewGuid(),
        InstitutionName = "НИУ ВШЭ"
    };

    public static Notification Notification(Guid? id = null, Guid? accountId = null) => new()
    {
        NotificationID = id ?? Guid.NewGuid(),
        AccountID = accountId ?? Guid.NewGuid(),
        Type = NotificationType.NewApplication,
        Message = "Test notification",
        IsRead = false,
        CreatedAt = DateTime.UtcNow
    };

    public static UserSkill UserSkill(Guid accountId, Guid skillId, SkillLevel level = SkillLevel.Middle) => new()
    {
        AccountID = accountId,
        SkillID = skillId,
        SkillLevel = level
    };
}
