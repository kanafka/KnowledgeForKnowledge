using System.Security.Claims;
using API.Helpers;
using Application.Common.Interfaces;
using Application.Features.Proofs.Commands.CreateProof;
using Application.Features.Proofs.Queries.GetProofs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/proofs")]
[Authorize]
public class ProofsController : BaseController
{
    private const int MaxProofsPerSkill = 3;

    private readonly IWebHostEnvironment _env;
    private readonly IApplicationDbContext _context;

    public ProofsController(IWebHostEnvironment env, IApplicationDbContext context)
    {
        _env = env;
        _context = context;
    }

    /// <summary>Список подтверждений пользователя.</summary>
    [HttpGet("{accountId:guid}")]
    public async Task<IActionResult> GetProofs(Guid accountId)
    {
        var result = await Mediator.Send(new GetProofsQuery(accountId));
        return Ok(result);
    }

    /// <summary>Загрузить подтверждающий файл: JPEG, PNG, WebP или PDF, максимум 10 МБ.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadProof([FromForm] UploadProofRequest request)
    {
        var accountId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (request.File == null || request.File.Length == 0)
            return BadRequest(new { message = "Файл не указан." });

        var existingCount = await _context.Proofs.CountAsync(p => p.AccountID == accountId);
        if (existingCount >= FileValidator.GetMaxProofsPerUser())
            return BadRequest(new { message = $"Достигнут лимит в {FileValidator.GetMaxProofsPerUser()} файлов." });

        if (request.SkillID.HasValue)
        {
            var existingSkillProofCount = await _context.Proofs.CountAsync(
                p => p.AccountID == accountId && p.SkillID == request.SkillID.Value);

            if (existingSkillProofCount >= MaxProofsPerSkill)
                return BadRequest(new { message = $"К одному навыку можно прикрепить не более {MaxProofsPerSkill} файлов." });
        }

        var error = await FileValidator.ValidateProofAsync(request.File);
        if (error != null)
            return BadRequest(new { message = error });

        var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        var fileName = $"{accountId}_{Guid.NewGuid():N}{ext}";
        var uploadsDir = Path.Combine(_env.ContentRootPath, "uploads", "proofs");
        Directory.CreateDirectory(uploadsDir);

        var filePath = Path.Combine(uploadsDir, fileName);
        await using (var stream = System.IO.File.Create(filePath))
            await request.File.CopyToAsync(stream);

        var fileUrl = $"/uploads/proofs/{fileName}";
        var id = await Mediator.Send(new CreateProofCommand(accountId, request.SkillID, fileUrl));

        return Created($"/api/proofs/{accountId}", new { id, fileUrl });
    }
}

public class UploadProofRequest
{
    public IFormFile? File { get; set; }
    public Guid? SkillID { get; set; }
}
