using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;
using EduPlatformAPI.Repositories;

namespace Educatinal_Platform.Services;

public sealed record LessonAccessDecision(
    bool IsAllowed,
    bool IsLocked,
    bool IsCurrent,
    bool IsCompleted,
    string? DenialMessage = null);

/// <summary>
/// Single server-side authority for the ordered lesson entitlement of an
/// enrollment. React may display these states, but protected media calls always
/// re-evaluate this service before issuing or serving a URL.
/// </summary>
public interface ICourseLessonAccessService
{
    Task<List<Lesson>> GetOrderedLessonsAsync(string courseId);
    Task<IReadOnlyDictionary<string, LessonAccessDecision>> GetAccessStatesAsync(
        Enrollment enrollment,
        IReadOnlyList<Lesson> orderedLessons);
    Task<LessonAccessDecision> CanAccessAsync(string studentId, string courseId, string lessonId);
}

public sealed class CourseLessonAccessService : ICourseLessonAccessService
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly ILessonRepository _lessonRepository;
    private readonly ITrackService _trackService;

    public CourseLessonAccessService(
        IEnrollmentRepository enrollmentRepository,
        ILessonRepository lessonRepository,
        ITrackService trackService)
    {
        _enrollmentRepository = enrollmentRepository;
        _lessonRepository = lessonRepository;
        _trackService = trackService;
    }

    public async Task<List<Lesson>> GetOrderedLessonsAsync(string courseId)
    {
        // TrackService performs the existing lazy legacy-track migration. This
        // keeps the course sequence stable for both new and pre-track courses.
        var tracks = await _trackService.GetByCourseIdAsync(courseId);
        var lessons = await _lessonRepository.GetByCourseIdAsync(courseId);
        var trackOrder = tracks.ToDictionary(track => track.Id, track => track.OrderIndex, StringComparer.Ordinal);

        return lessons
            .OrderBy(lesson => lesson.TrackId is not null && trackOrder.TryGetValue(lesson.TrackId, out var order)
                ? order
                : int.MaxValue)
            .ThenBy(lesson => lesson.OrderIndex)
            .ThenBy(lesson => lesson.CreatedAt)
            .ToList();
    }

    public Task<IReadOnlyDictionary<string, LessonAccessDecision>> GetAccessStatesAsync(
        Enrollment enrollment,
        IReadOnlyList<Lesson> orderedLessons)
    {
        var completedIds = enrollment.WatchedLessons
            .Where(item => item.IsCompleted && !string.IsNullOrWhiteSpace(item.LessonId))
            .Select(item => item.LessonId)
            .ToHashSet(StringComparer.Ordinal);

        var states = new Dictionary<string, LessonAccessDecision>(StringComparer.Ordinal);
        if (CourseAccessTypes.Resolve(enrollment.AccessType) == CourseAccessTypes.Full)
        {
            foreach (var lesson in orderedLessons)
            {
                var isCompleted = completedIds.Contains(lesson.Id);
                states[lesson.Id] = new LessonAccessDecision(true, false, false, isCompleted);
            }

            return Task.FromResult<IReadOnlyDictionary<string, LessonAccessDecision>>(states);
        }

        var currentIndex = -1;
        for (var index = 0; index < orderedLessons.Count; index++)
        {
            if (!completedIds.Contains(orderedLessons[index].Id))
            {
                currentIndex = index;
                break;
            }
        }

        for (var index = 0; index < orderedLessons.Count; index++)
        {
            var lesson = orderedLessons[index];
            var isCompleted = completedIds.Contains(lesson.Id);
            var isCurrent = !isCompleted && index == currentIndex;
            var isAllowed = isCompleted || isCurrent;
            states[lesson.Id] = new LessonAccessDecision(
                isAllowed,
                !isAllowed,
                isCurrent,
                isCompleted,
                isAllowed ? null : "This lesson is locked. Complete the previous lesson to continue.");
        }

        return Task.FromResult<IReadOnlyDictionary<string, LessonAccessDecision>>(states);
    }

    public async Task<LessonAccessDecision> CanAccessAsync(string studentId, string courseId, string lessonId)
    {
        var enrollment = await _enrollmentRepository.GetByStudentAndCourseAsync(studentId, courseId);
        if (enrollment is null ||
            (!string.Equals(enrollment.Status, "active", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(enrollment.Status, "completed", StringComparison.OrdinalIgnoreCase)))
        {
            return new LessonAccessDecision(false, false, false, false, "An active enrollment is required.");
        }

        var lessons = await GetOrderedLessonsAsync(courseId);
        if (!lessons.Any(lesson => string.Equals(lesson.Id, lessonId, StringComparison.Ordinal)))
            return new LessonAccessDecision(false, false, false, false, "Lesson not found in this course.");

        var states = await GetAccessStatesAsync(enrollment, lessons);
        return states.TryGetValue(lessonId, out var decision)
            ? decision
            : new LessonAccessDecision(false, false, false, false, "Lesson access could not be resolved.");
    }
}
