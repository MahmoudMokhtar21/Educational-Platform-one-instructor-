using Educatinal_Platform.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Educatinal_Platform.Repositories
{
    public interface ITrackRepository
    {
        Task<List<Track>> GetByCourseIdAsync(string courseId);
        Task<Track?> GetByIdAsync(string id);
        Task<Track> CreateAsync(Track track);
        Task UpdateAsync(string id, Track track);
        Task DeleteAsync(string id);
        Task<int> GetNextOrderIndexAsync(string courseId);
        Task ReorderAsync(string courseId, IReadOnlyDictionary<string, int> orderByTrackId);
        Task<Track> GetOrCreateLegacyGeneralAsync(string courseId, int orderIndex);
        Task EnsureIndexesAsync();
    }

    public class TrackRepository : ITrackRepository
    {
        public const string LegacyGeneralKey = "legacy-general";
        private readonly IMongoCollection<Track> _tracks;

        public TrackRepository(IMongoDatabase database)
        {
            _tracks = database.GetCollection<Track>("Tracks");
        }

        public Task<List<Track>> GetByCourseIdAsync(string courseId) => _tracks
            .Find(track => track.CourseId == courseId)
            .SortBy(track => track.OrderIndex)
            .ToListAsync();

        public async Task<Track?> GetByIdAsync(string id) => await _tracks
            .Find(track => track.Id == id)
            .FirstOrDefaultAsync();

        public async Task<Track> CreateAsync(Track track)
        {
            await _tracks.InsertOneAsync(track);
            return track;
        }

        public Task UpdateAsync(string id, Track track) => _tracks.ReplaceOneAsync(
            existing => existing.Id == id,
            track);

        public Task DeleteAsync(string id) => _tracks.DeleteOneAsync(track => track.Id == id);

        public async Task<int> GetNextOrderIndexAsync(string courseId)
        {
            var last = await _tracks.Find(track => track.CourseId == courseId)
                .SortByDescending(track => track.OrderIndex)
                .FirstOrDefaultAsync();

            return last == null ? 1 : last.OrderIndex + 1;
        }

        public Task ReorderAsync(string courseId, IReadOnlyDictionary<string, int> orderByTrackId)
        {
            if (orderByTrackId.Count == 0)
                return Task.CompletedTask;

            var updates = orderByTrackId.Select(item => new UpdateOneModel<Track>(
                Builders<Track>.Filter.And(
                    Builders<Track>.Filter.Eq(track => track.Id, item.Key),
                    Builders<Track>.Filter.Eq(track => track.CourseId, courseId)),
                Builders<Track>.Update
                    .Set(track => track.OrderIndex, item.Value)
                    .Set(track => track.UpdatedAt, DateTime.UtcNow)));

            return _tracks.BulkWriteAsync(updates);
        }

        public async Task<Track> GetOrCreateLegacyGeneralAsync(string courseId, int orderIndex)
        {
            var filter = Builders<Track>.Filter.And(
                Builders<Track>.Filter.Eq(track => track.CourseId, courseId),
                Builders<Track>.Filter.Eq(track => track.LegacyKey, LegacyGeneralKey));
            var now = DateTime.UtcNow;
            var update = Builders<Track>.Update.Combine(
                Builders<Track>.Update.SetOnInsert(track => track.Id, ObjectId.GenerateNewId().ToString()),
                Builders<Track>.Update.SetOnInsert(track => track.CourseId, courseId),
                Builders<Track>.Update.SetOnInsert(track => track.Title, "General"),
                Builders<Track>.Update.SetOnInsert(track => track.Description, null),
                Builders<Track>.Update.SetOnInsert(track => track.OrderIndex, orderIndex),
                Builders<Track>.Update.SetOnInsert(track => track.LegacyKey, LegacyGeneralKey),
                Builders<Track>.Update.SetOnInsert(track => track.CreatedAt, now),
                Builders<Track>.Update.SetOnInsert(track => track.UpdatedAt, now));

            return await _tracks.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<Track> { IsUpsert = true, ReturnDocument = ReturnDocument.After });
        }

        public Task EnsureIndexesAsync()
        {
            var keys = Builders<Track>.IndexKeys
                .Ascending(track => track.CourseId)
                .Ascending(track => track.LegacyKey);
            var options = new CreateIndexOptions<Track>
            {
                Unique = true,
                PartialFilterExpression = Builders<Track>.Filter.Eq(track => track.LegacyKey, LegacyGeneralKey)
            };
            return _tracks.Indexes.CreateOneAsync(new CreateIndexModel<Track>(keys, options));
        }
    }
}
