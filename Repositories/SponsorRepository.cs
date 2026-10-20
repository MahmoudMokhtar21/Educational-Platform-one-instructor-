using Educatinal_Platform.Models;
using MongoDB.Driver;

namespace Educatinal_Platform.Repositories;

public interface ISponsorRepository
{
    Task<List<Sponsor>> GetAllAsync(bool activeOnly);
    Task<Sponsor?> GetByIdAsync(string id);
    Task CreateAsync(Sponsor sponsor);
    Task ReplaceAsync(Sponsor sponsor);
    Task DeleteAsync(string id);
}

public class SponsorRepository(IMongoDatabase database) : ISponsorRepository
{
    private readonly IMongoCollection<Sponsor> _sponsors = database.GetCollection<Sponsor>("Sponsors");
    public Task<List<Sponsor>> GetAllAsync(bool activeOnly) => _sponsors.Find(activeOnly ? x => x.IsActive : _ => true).SortBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync();
    public async Task<Sponsor?> GetByIdAsync(string id) => await _sponsors.Find(x => x.Id == id).FirstOrDefaultAsync();
    public Task CreateAsync(Sponsor sponsor) => _sponsors.InsertOneAsync(sponsor);
    public Task ReplaceAsync(Sponsor sponsor) => _sponsors.ReplaceOneAsync(x => x.Id == sponsor.Id, sponsor);
    public Task DeleteAsync(string id) => _sponsors.DeleteOneAsync(x => x.Id == id);
}
