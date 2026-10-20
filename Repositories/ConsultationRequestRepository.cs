using Educatinal_Platform.Models;
using MongoDB.Driver;

namespace Educatinal_Platform.Repositories;

public interface IConsultationRequestRepository
{
    Task<List<ConsultationRequest>> GetAllAsync(ConsultationRequestStatus? status = null);
    Task<ConsultationRequest?> GetByIdAsync(string id);
    Task CreateAsync(ConsultationRequest request);
    Task ReplaceAsync(ConsultationRequest request);
}

public class ConsultationRequestRepository(IMongoDatabase database) : IConsultationRequestRepository
{
    private readonly IMongoCollection<ConsultationRequest> _requests =
        database.GetCollection<ConsultationRequest>("ConsultationRequests");

    public Task<List<ConsultationRequest>> GetAllAsync(ConsultationRequestStatus? status = null) =>
        _requests.Find(status is null ? _ => true : request => request.Status == status.Value)
            .SortByDescending(request => request.UpdatedAt)
            .ThenByDescending(request => request.SubmittedAt)
            .ToListAsync();

    public async Task<ConsultationRequest?> GetByIdAsync(string id) =>
        await _requests.Find(request => request.Id == id).FirstOrDefaultAsync();

    public Task CreateAsync(ConsultationRequest request) => _requests.InsertOneAsync(request);

    public Task ReplaceAsync(ConsultationRequest request) =>
        _requests.ReplaceOneAsync(item => item.Id == request.Id, request);
}
