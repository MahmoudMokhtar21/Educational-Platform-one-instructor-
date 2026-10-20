using Educatinal_Platform.DTOs;
using Educatinal_Platform.Repositories;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Educatinal_Platform.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideosController(IContentHubService content) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int limit = 12, [FromQuery] string? search = null, [FromQuery] string? category = null, [FromQuery] string? tag = null, [FromQuery] string? platform = null, [FromQuery] bool featured = false) => Ok(await content.ListVideosAsync(page, limit, new ContentHubQuery(search, category, tag, platform, FeaturedOnly: featured), false));

    [AllowAnonymous]
    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug)
    {
        var video = await content.GetVideoAsync(slug, false);
        return video is null ? NotFound(new { message = "Video not found." }) : Ok(video);
    }

    [Authorize(Roles = "admin")]
    [HttpGet("admin")]
    public Task<IActionResult> AdminList([FromQuery] int page = 1, [FromQuery] int limit = 50, [FromQuery] string? search = null, [FromQuery] string? category = null, [FromQuery] string? tag = null, [FromQuery] string? platform = null, [FromQuery] bool featured = false) => AdminListCore(page, limit, search, category, tag, platform, featured);
    private async Task<IActionResult> AdminListCore(int page, int limit, string? search, string? category, string? tag, string? platform, bool featured) => Ok(await content.ListVideosAsync(page, limit, new ContentHubQuery(search, category, tag, platform, FeaturedOnly: featured), true));

    [Authorize(Roles = "admin")]
    [HttpGet("admin/{slug}")]
    public async Task<IActionResult> AdminDetail(string slug) { var video = await content.GetVideoAsync(slug, true); return video is null ? NotFound(new { message = "Video not found." }) : Ok(video); }
    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] VideoUpsertDto dto) { var video = await content.CreateVideoAsync(dto); return CreatedAtAction(nameof(Detail), new { slug = video.Slug }, video); }
    [Authorize(Roles = "admin")]
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateWithThumbnail([FromForm] VideoUpsertDto dto) { var video = await content.CreateVideoWithThumbnailAsync(dto, dto.Thumbnail); return CreatedAtAction(nameof(Detail), new { slug = video.Slug }, video); }
    [Authorize(Roles = "admin")]
    [HttpPut("{id}/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateWithThumbnail(string id, [FromForm] VideoUpsertDto dto) { await content.UpdateVideoWithThumbnailAsync(id, dto, dto.Thumbnail); return NoContent(); }
    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] VideoUpsertDto dto) { await content.UpdateVideoAsync(id, dto); return NoContent(); }
    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id) { await content.DeleteVideoAsync(id); return NoContent(); }
    [Authorize(Roles = "admin")]
    [HttpPost("{id}/publish")]
    public async Task<IActionResult> Publish(string id) { await content.SetVideoStatusAsync(id, "published"); return Ok(new { message = "Video published." }); }
    [Authorize(Roles = "admin")]
    [HttpPost("{id}/unpublish")]
    public async Task<IActionResult> Unpublish(string id) { await content.SetVideoStatusAsync(id, "unpublished"); return Ok(new { message = "Video unpublished." }); }
}
