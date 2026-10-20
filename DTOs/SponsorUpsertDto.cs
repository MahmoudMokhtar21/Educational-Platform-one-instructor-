using Microsoft.AspNetCore.Http;

namespace Educatinal_Platform.DTOs;

public class SponsorUpsertDto
{
    public string Name { get; set; } = string.Empty;
    public IFormFile? Logo { get; set; }
    public string? WebsiteUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
