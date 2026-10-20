using Educatinal_Platform.Models;
using MongoDB.Driver;
namespace Educatinal_Platform.Repositories;
public interface IStudentReviewRepository { Task<List<StudentReview>> GetAllAsync(); Task<StudentReview?> GetByIdAsync(string id); Task CreateAsync(StudentReview item); Task ReplaceAsync(StudentReview item); Task DeleteAsync(string id); }
public class StudentReviewRepository(IMongoDatabase database) : IStudentReviewRepository
{
    private readonly IMongoCollection<StudentReview> _items = database.GetCollection<StudentReview>("StudentReviews");

    public Task<List<StudentReview>> GetAllAsync() =>
        _items.Find(_ => true).SortByDescending(x => x.CreatedAt).ToListAsync();

    public async Task<StudentReview?> GetByIdAsync(string id) =>
        await _items.Find(item => item.Id == id).FirstOrDefaultAsync();

    public Task CreateAsync(StudentReview item) => _items.InsertOneAsync(item);

    public Task ReplaceAsync(StudentReview item) =>
        _items.ReplaceOneAsync(existing => existing.Id == item.Id, item);

    public Task DeleteAsync(string id) => _items.DeleteOneAsync(item => item.Id == id);
}
