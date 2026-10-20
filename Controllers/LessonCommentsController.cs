using Educatinal_Platform.DTOs;
using Educatinal_Platform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Educatinal_Platform.Controllers;

[ApiController]
[Authorize]
[Route("api/lessons/{lessonId}/comments")]
public sealed class LessonCommentsController : ControllerBase
{
    private readonly ILessonCommentService _lessonCommentService;

    public LessonCommentsController(ILessonCommentService lessonCommentService)
    {
        _lessonCommentService = lessonCommentService;
    }

    [HttpGet]
    public async Task<ActionResult<List<LessonCommentResponseDto>>> GetByLesson(string lessonId)
    {
        return Ok(await _lessonCommentService.GetByLessonAsync(User, lessonId));
    }

    [HttpPost]
    public async Task<ActionResult<LessonCommentResponseDto>> Create(
        string lessonId,
        [FromBody] CreateLessonCommentDto dto)
    {
        var created = await _lessonCommentService.CreateAsync(User, lessonId, dto);
        return CreatedAtAction(nameof(GetByLesson), new { lessonId }, created);
    }
}
