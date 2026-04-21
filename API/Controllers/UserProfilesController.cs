using System.Security.Claims;
using API.Helpers;
using Application.Common.Interfaces;
using Application.Features.UserProfiles.Commands.UpsertUserProfile;
using Application.Features.UserProfiles.Queries.GetUserProfile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/userprofiles")]
[Authorize]
public class UserProfilesController : BaseController
{
    private readonly IApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public UserProfilesController(IApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    private Guid CurrentAccountId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Получить профиль пользователя</summary>
    [HttpGet("{accountId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProfile(Guid accountId)
    {
        Guid? requestingId = User.Identity?.IsAuthenticated == true
            ? Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : null;
        var profile = await Mediator.Send(new GetUserProfileQuery(accountId, requestingId));
        return Ok(profile);
    }

    /// <summary>Создать или обновить профиль текущего пользователя</summary>
    [HttpPut]
    public async Task<IActionResult> UpsertProfile([FromBody] UpsertProfileRequest request)
    {
        await Mediator.Send(new UpsertUserProfileCommand(
            CurrentAccountId, request.FullName, request.DateOfBirth,
            request.PhotoURL, request.ContactInfo, request.Description));
        return NoContent();
    }

    /// <summary>
    /// Загрузить фото профиля (multipart/form-data, поле "photo").
    /// Форматы: JPEG, PNG, WebP. Максимум 5 МБ.
    /// </summary>
    [HttpPost("photo")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPhoto(IFormFile photo)
    {
        if (photo == null || photo.Length == 0)
            return BadRequest(new { message = "Файл не выбран." });

        var error = await FileValidator.ValidateImageAsync(photo);
        if (error != null)
            return BadRequest(new { message = error });

        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
        var fileName = $"{CurrentAccountId}_{Guid.NewGuid():N}{ext}";
        var webRootPath = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
        var uploadPath = Path.Combine(webRootPath, "uploads", "photos");
        Directory.CreateDirectory(uploadPath);

        var filePath = Path.Combine(uploadPath, fileName);
        await using (var stream = new FileStream(filePath, FileMode.Create))
            await photo.CopyToAsync(stream);

        var photoUrl = $"/uploads/photos/{fileName}";

        var profile = await _context.UserProfiles.FindAsync(CurrentAccountId);
        if (profile is null)
        {
            profile = new Domain.Entities.UserProfile
            {
                AccountID = CurrentAccountId,
                FullName = string.Empty,
                PhotoURL = photoUrl
            };
            _context.UserProfiles.Add(profile);
        }

        profile.PhotoURL = photoUrl;
        await _context.SaveChangesAsync();

        return Ok(new { photoUrl });
    }
}

public class UpsertProfileRequest
{
    public string FullName { get; init; } = string.Empty;
    public DateTime? DateOfBirth { get; init; }
    public string? PhotoURL { get; init; }
    public string? ContactInfo { get; init; }
    public string? Description { get; init; }
}
