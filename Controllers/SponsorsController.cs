using Educatinal_Platform.DTOs;
using Educatinal_Platform.Exceptions;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Educatinal_Platform.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SponsorsController(ISponsorRepository sponsors, IFileService files) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SponsorDto>>> Active() => Ok((await sponsors.GetAllAsync(true)).Select(Map));

    [Authorize(Roles = "admin")]
    [HttpGet("admin")]
    public async Task<ActionResult<List<SponsorDto>>> All() => Ok((await sponsors.GetAllAsync(false)).Select(Map));

    [Authorize(Roles = "admin")]
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<SponsorDto>> Create([FromForm] SponsorUpsertDto dto)
    {
        if (dto.Logo is null) throw new BadRequestException("A sponsor logo is required.");
        var sponsor = new Sponsor { Name = NameOf(dto.Name), LogoUrl = await Upload(dto.Logo), WebsiteUrl = UrlOf(dto.WebsiteUrl), IsActive = dto.IsActive, DisplayOrder = dto.DisplayOrder };
        await sponsors.CreateAsync(sponsor);
        return Ok(Map(sponsor));
    }

    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<SponsorDto>> Update(string id, [FromForm] SponsorUpsertDto dto)
    {
        var sponsor = await sponsors.GetByIdAsync(id) ?? throw new NotFoundException("Sponsor not found.");
        sponsor.Name = NameOf(dto.Name); sponsor.WebsiteUrl = UrlOf(dto.WebsiteUrl); sponsor.IsActive = dto.IsActive; sponsor.DisplayOrder = dto.DisplayOrder; sponsor.UpdatedAt = DateTime.UtcNow;
        if (dto.Logo is not null) { var old = sponsor.LogoUrl; sponsor.LogoUrl = await Upload(dto.Logo); await files.DeleteAsync(old); }
        await sponsors.ReplaceAsync(sponsor);
        return Ok(Map(sponsor));
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var sponsor = await sponsors.GetByIdAsync(id) ?? throw new NotFoundException("Sponsor not found.");
        await sponsors.DeleteAsync(id); await files.DeleteAsync(sponsor.LogoUrl); return NoContent();
    }

    private Task<string> Upload(IFormFile file) => files.UploadAsync(file, "sponsors", [".png", ".jpg", ".jpeg", ".svg", ".webp"], 2 * 1024 * 1024);
    private static string NameOf(string value) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 100 ? throw new BadRequestException("Sponsor name is required and must be 100 characters or fewer.") : value.Trim();
    private static string? UrlOf(string? value) { if (string.IsNullOrWhiteSpace(value)) return null; return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? uri.ToString() : throw new BadRequestException("Website URL must be an absolute HTTP or HTTPS URL."); }
    private static SponsorDto Map(Sponsor x) => new() { Id = x.Id, Name = x.Name, LogoUrl = x.LogoUrl, WebsiteUrl = x.WebsiteUrl, IsActive = x.IsActive, DisplayOrder = x.DisplayOrder };
}
