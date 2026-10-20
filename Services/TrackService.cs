using Educatinal_Platform.DTOs;
using Educatinal_Platform.Exceptions;
using Educatinal_Platform.Models;
using Educatinal_Platform.Repositories;

namespace Educatinal_Platform.Services
{
    public interface ITrackService
    {
        Task<List<Track>> GetByCourseIdAsync(string courseId);
        Task<Track> CreateAsync(string courseId, CreateTrackDto dto);
        Task UpdateAsync(string courseId, string trackId, UpdateTrackDto dto);
        Task DeleteAsync(string courseId, string trackId, string? moveLessonsToTrackId);
        Task ReorderAsync(string courseId, ReorderTracksDto dto);
        Task<Track> ResolveForLessonAsync(string courseId, string? trackId);
    }

    public class TrackService : ITrackService
    {
        private readonly ITrackRepository _trackRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly ILessonRepository _lessonRepository;

        public TrackService(ITrackRepository trackRepository, ICourseRepository courseRepository, ILessonRepository lessonRepository)
        {
            _trackRepository = trackRepository;
            _courseRepository = courseRepository;
            _lessonRepository = lessonRepository;
        }

        public async Task<List<Track>> GetByCourseIdAsync(string courseId)
        {
            await EnsureCourseExistsAsync(courseId);
            await EnsureLegacyLessonsAssignedAsync(courseId);
            return await _trackRepository.GetByCourseIdAsync(courseId);
        }

        public async Task<Track> CreateAsync(string courseId, CreateTrackDto dto)
        {
            await EnsureCourseExistsAsync(courseId);
            await EnsureLegacyLessonsAssignedAsync(courseId);
            var title = dto.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title)) throw new BadRequestException("Track title is required.");
            var track = new Track { CourseId = courseId, Title = title, Description = NormalizeDescription(dto.Description), OrderIndex = await _trackRepository.GetNextOrderIndexAsync(courseId), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            return await _trackRepository.CreateAsync(track);
        }

        public async Task UpdateAsync(string courseId, string trackId, UpdateTrackDto dto)
        {
            var track = await GetTrackForCourseAsync(courseId, trackId);
            if (dto.Title != null)
            {
                var title = dto.Title.Trim();
                if (string.IsNullOrWhiteSpace(title)) throw new BadRequestException("Track title cannot be empty.");
                track.Title = title;
            }
            if (dto.Description != null) track.Description = NormalizeDescription(dto.Description);
            track.UpdatedAt = DateTime.UtcNow;
            await _trackRepository.UpdateAsync(track.Id, track);
        }

        public async Task DeleteAsync(string courseId, string trackId, string? moveLessonsToTrackId)
        {
            var track = await GetTrackForCourseAsync(courseId, trackId);
            var lessons = await _lessonRepository.GetByTrackIdAsync(courseId, track.Id);
            if (lessons.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(moveLessonsToTrackId)) throw new InvalidOperationException("Move the lessons to another track before deletion.");
                var target = await GetTrackForCourseAsync(courseId, moveLessonsToTrackId);
                if (target.Id == track.Id) throw new BadRequestException("Lessons must be moved to a different track.");
                var firstTargetOrder = await _lessonRepository.GetNextOrderIndexAsync(courseId, target.Id);
                await _lessonRepository.MoveToTrackAsync(courseId, lessons.Select(lesson => lesson.Id).ToList(), target.Id, firstTargetOrder);
                await _lessonRepository.NormalizeTrackOrderAsync(courseId, target.Id);
            }
            await _trackRepository.DeleteAsync(track.Id);
        }

        public async Task ReorderAsync(string courseId, ReorderTracksDto dto)
        {
            var tracks = await GetByCourseIdAsync(courseId);
            if (dto.Tracks.Count != tracks.Count) throw new BadRequestException("The submitted ordering must include every track in the course.");
            var submittedIds = dto.Tracks.Select(item => item.TrackId).ToList();
            if (submittedIds.Any(string.IsNullOrWhiteSpace) || submittedIds.Distinct().Count() != submittedIds.Count) throw new BadRequestException("Each track must appear exactly once.");
            if (!submittedIds.OrderBy(id => id).SequenceEqual(tracks.Select(track => track.Id).OrderBy(id => id))) throw new BadRequestException("The submitted tracks do not match this course.");
            var indexes = dto.Tracks.Select(item => item.OrderIndex).OrderBy(index => index).ToList();
            if (!indexes.SequenceEqual(Enumerable.Range(1, tracks.Count))) throw new BadRequestException("Track order indexes must be consecutive and start at 1.");
            await _trackRepository.ReorderAsync(courseId, dto.Tracks.ToDictionary(item => item.TrackId, item => item.OrderIndex));
        }

        public async Task<Track> ResolveForLessonAsync(string courseId, string? trackId)
        {
            if (!string.IsNullOrWhiteSpace(trackId)) return await GetTrackForCourseAsync(courseId, trackId);
            var tracks = await GetByCourseIdAsync(courseId);
            var legacyTrack = tracks.FirstOrDefault(track => track.LegacyKey == TrackRepository.LegacyGeneralKey);
            if (legacyTrack != null) return legacyTrack;
            return await _trackRepository.GetOrCreateLegacyGeneralAsync(courseId, await _trackRepository.GetNextOrderIndexAsync(courseId));
        }

        private async Task<Track> GetTrackForCourseAsync(string courseId, string trackId)
        {
            await EnsureCourseExistsAsync(courseId);
            await EnsureLegacyLessonsAssignedAsync(courseId);
            var track = await _trackRepository.GetByIdAsync(trackId);
            if (track == null || !string.Equals(track.CourseId, courseId, StringComparison.Ordinal)) throw new NotFoundException("Track not found for this course.");
            return track;
        }

        private async Task EnsureCourseExistsAsync(string courseId)
        {
            if (string.IsNullOrWhiteSpace(courseId) || await _courseRepository.GetByIdAsync(courseId) == null) throw new NotFoundException("Course not found.");
        }

        private async Task EnsureLegacyLessonsAssignedAsync(string courseId)
        {
            var lessons = await _lessonRepository.GetByCourseIdAsync(courseId);
            if (lessons.Count == 0) return;
            var tracks = await _trackRepository.GetByCourseIdAsync(courseId);
            var validTrackIds = tracks.Select(track => track.Id).ToHashSet(StringComparer.Ordinal);
            var legacyLessons = lessons.Where(lesson => string.IsNullOrWhiteSpace(lesson.TrackId) || !validTrackIds.Contains(lesson.TrackId)).ToList();
            if (legacyLessons.Count == 0) return;
            var legacyTrack = tracks.FirstOrDefault(track => track.LegacyKey == TrackRepository.LegacyGeneralKey) ?? await _trackRepository.GetOrCreateLegacyGeneralAsync(courseId, tracks.Count == 0 ? 1 : tracks.Max(track => track.OrderIndex) + 1);
            await _lessonRepository.AssignToTrackAsync(courseId, legacyTrack.Id, legacyLessons.Select(lesson => lesson.Id).ToList());
            await _lessonRepository.NormalizeTrackOrderAsync(courseId, legacyTrack.Id);
        }

        private static string? NormalizeDescription(string? description) => string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
