using Educatinal_Platform.DTOs;
using Educatinal_Platform.Repositories;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Educatinal_Platform.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArticlesController(IContentHubService content) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int limit = 12, [FromQuery] string? search = null, [FromQuery] string? category = null, [FromQuery] string? tag = null, [FromQuery] string? contentType = null, [FromQuery] bool featured = false) => Ok(await content.ListArticlesAsync(page, limit, new ContentHubQuery(search, category, tag, ContentType: contentType, FeaturedOnly: featured), false));
    [AllowAnonymous]
    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug) { var article = await content.GetArticleAsync(slug, false); return article is null ? NotFound(new { message = "Article not found." }) : Ok(article); }
    [Authorize(Roles = "admin")]
    [HttpGet("admin")]
    public async Task<IActionResult> AdminList([FromQuery] int page = 1, [FromQuery] int limit = 50, [FromQuery] string? search = null, [FromQuery] string? category = null, [FromQuery] string? tag = null, [FromQuery] string? contentType = null, [FromQuery] bool featured = false) => Ok(await content.ListArticlesAsync(page, limit, new ContentHubQuery(search, category, tag, ContentType: contentType, FeaturedOnly: featured), true));
    [Authorize(Roles = "admin")]
    [HttpGet("admin/{slug}")]
    public async Task<IActionResult> AdminDetail(string slug) { var article = await content.GetArticleAsync(slug, true); return article is null ? NotFound(new { message = "Article not found." }) : Ok(article); }
    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ArticleUpsertDto dto) { var article = await content.CreateArticleAsync(dto); return CreatedAtAction(nameof(Detail), new { slug = article.Slug }, article); }
    [Authorize(Roles = "admin")]
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateWithCoverImage([FromForm] ArticleUpsertDto dto) { var article = await content.CreateArticleWithCoverImageAsync(dto, dto.CoverImage); return CreatedAtAction(nameof(Detail), new { slug = article.Slug }, article); }
    [Authorize(Roles = "admin")]
    [HttpPut("{id}/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateWithCoverImage(string id, [FromForm] ArticleUpsertDto dto) { await content.UpdateArticleWithCoverImageAsync(id, dto, dto.CoverImage); return NoContent(); }
    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] ArticleUpsertDto dto) { await content.UpdateArticleAsync(id, dto); return NoContent(); }
    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id) { await content.DeleteArticleAsync(id); return NoContent(); }
    [Authorize(Roles = "admin")]
    [HttpPost("{id}/publish")]
    public async Task<IActionResult> Publish(string id) { await content.SetArticleStatusAsync(id, "published"); return Ok(new { message = "Article published." }); }
    [Authorize(Roles = "admin")]
    [HttpPost("{id}/unpublish")]
    public async Task<IActionResult> Unpublish(string id) { await content.SetArticleStatusAsync(id, "unpublished"); return Ok(new { message = "Article unpublished." }); }
}
