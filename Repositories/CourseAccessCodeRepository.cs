using Educatinal_Platform.Models;
using MongoDB.Driver;

namespace Educatinal_Platform.Repositories;

public interface ICourseAccessCodeRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
    Task<CourseAccessCode> CreateAsync(CourseAccessCode code, CancellationToken cancellationToken = default);
    Task<List<CourseAccessCode>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CourseAccessCode?> GetByHashAsync(string codeHash, CancellationToken cancellationToken = default);
    Task<CourseAccessCode?> TryClaimAsync(string codeHash, string courseId, string studentId, string claimToken, DateTime now, CancellationToken cancellationToken = default);
    Task<bool> FinalizeRedemptionAsync(string id, string claimToken, string studentId, DateTime now, CancellationToken cancellationToken = default);
    Task ReleaseClaimAsync(string id, string claimToken, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string id, DateTime now, CancellationToken cancellationToken = default);
}

public sealed class CourseAccessCodeRepository : ICourseAccessCodeRepository
{
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(2);
    private readonly IMongoCollection<CourseAccessCode> _codes;
    public CourseAccessCodeRepository(IMongoDatabase database) => _codes = database.GetCollection<CourseAccessCode>("CourseAccessCodes");

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var hash = new CreateIndexModel<CourseAccessCode>(Builders<CourseAccessCode>.IndexKeys.Ascending(item => item.CodeHash), new CreateIndexOptions { Name = "ux_course_access_code_hash", Unique = true });
        var created = new CreateIndexModel<CourseAccessCode>(Builders<CourseAccessCode>.IndexKeys.Descending(item => item.CreatedAt), new CreateIndexOptions { Name = "ix_course_access_code_created" });
        return _codes.Indexes.CreateManyAsync(new[] { hash, created }, cancellationToken);
    }
    public async Task<CourseAccessCode> CreateAsync(CourseAccessCode code, CancellationToken cancellationToken = default) { await _codes.InsertOneAsync(code, cancellationToken: cancellationToken); return code; }
    public Task<List<CourseAccessCode>> GetAllAsync(CancellationToken cancellationToken = default) => _codes.Find(Builders<CourseAccessCode>.Filter.Empty).SortByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
    public async Task<CourseAccessCode?> GetByHashAsync(string codeHash, CancellationToken cancellationToken = default) => await _codes.Find(item => item.CodeHash == codeHash).FirstOrDefaultAsync(cancellationToken);

    public async Task<CourseAccessCode?> TryClaimAsync(string codeHash, string courseId, string studentId, string claimToken, DateTime now, CancellationToken cancellationToken = default)
    {
        var staleBefore = now.Subtract(ClaimLease);
        var filter = Builders<CourseAccessCode>.Filter.And(
            Builders<CourseAccessCode>.Filter.Eq(item => item.CodeHash, codeHash), Builders<CourseAccessCode>.Filter.Eq(item => item.CourseId, courseId),
            Builders<CourseAccessCode>.Filter.Eq(item => item.RevokedAt, null), Builders<CourseAccessCode>.Filter.Eq(item => item.RedeemedAt, null),
            Builders<CourseAccessCode>.Filter.Gt(item => item.ExpiresAt, now),
            Builders<CourseAccessCode>.Filter.Or(Builders<CourseAccessCode>.Filter.Eq(item => item.RedemptionClaimToken, null), Builders<CourseAccessCode>.Filter.Lt(item => item.RedemptionClaimedAt, staleBefore)));
        var update = Builders<CourseAccessCode>.Update.Set(item => item.RedemptionClaimToken, claimToken).Set(item => item.RedemptionClaimedByStudentId, studentId).Set(item => item.RedemptionClaimedAt, now);
        return await _codes.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<CourseAccessCode> { ReturnDocument = ReturnDocument.After }, cancellationToken);
    }
    public async Task<bool> FinalizeRedemptionAsync(string id, string claimToken, string studentId, DateTime now, CancellationToken cancellationToken = default)
    {
        var update = Builders<CourseAccessCode>.Update.Set(item => item.RedeemedAt, now).Set(item => item.RedeemedByStudentId, studentId).Set(item => item.RedemptionClaimToken, null).Set(item => item.RedemptionClaimedByStudentId, null).Set(item => item.RedemptionClaimedAt, null);
        var result = await _codes.UpdateOneAsync(item => item.Id == id && item.RedeemedAt == null && item.RedemptionClaimToken == claimToken, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }
    public Task ReleaseClaimAsync(string id, string claimToken, CancellationToken cancellationToken = default) => _codes.UpdateOneAsync(item => item.Id == id && item.RedeemedAt == null && item.RedemptionClaimToken == claimToken, Builders<CourseAccessCode>.Update.Set(item => item.RedemptionClaimToken, null).Set(item => item.RedemptionClaimedByStudentId, null).Set(item => item.RedemptionClaimedAt, null), cancellationToken: cancellationToken);
    public async Task<bool> RevokeAsync(string id, DateTime now, CancellationToken cancellationToken = default)
    {
        var result = await _codes.UpdateOneAsync(item => item.Id == id && item.RedeemedAt == null && item.RevokedAt == null && item.RedemptionClaimToken == null, Builders<CourseAccessCode>.Update.Set(item => item.RevokedAt, now), cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }
}