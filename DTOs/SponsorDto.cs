namespace Educatinal_Platform.DTOs;

public class SponsorDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string? WebsiteUrl { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}
