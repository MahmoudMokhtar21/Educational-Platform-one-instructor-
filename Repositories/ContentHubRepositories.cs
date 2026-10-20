using Educatinal_Platform.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace Educatinal_Platform.Repositories;

public sealed record ContentHubQuery(
    string? Search = null,
    string? Category = null,
    string? Tag = null,
    string? Platform = null,
    string? ContentType = null,
    bool? FeaturedOnly = null);

public interface IExternalVideoRepository
{
    Task<List<ExternalVideoContent>> ListAsync(int page, int limit, ContentHubQuery query, bool publishedOnly);
    Task<long> CountAsync(ContentHubQuery query, bool publishedOnly);
    Task<ExternalVideoContent?> GetByIdAsync(string id);
    Task<ExternalVideoContent?> GetBySlugAsync(string slug, bool publishedOnly);
    Task<bool> SlugExistsAsync(string slug, string? exceptId = null);
    Task CreateAsync(ExternalVideoContent video);
    Task ReplaceAsync(ExternalVideoContent video);
    Task DeleteAsync(string id);
}

public interface IKnowledgeArticleRepository
{
    Task<List<KnowledgeArticle>> ListAsync(int page, int limit, ContentHubQuery query, bool publishedOnly);
    Task<long> CountAsync(ContentHubQuery query, bool publishedOnly);
    Task<KnowledgeArticle?> GetByIdAsync(string id);
    Task<KnowledgeArticle?> GetBySlugAsync(string slug, bool publishedOnly);
    Task<bool> SlugExistsAsync(string slug, string? exceptId = null);
    Task CreateAsync(KnowledgeArticle article);
    Task ReplaceAsync(KnowledgeArticle article);
    Task DeleteAsync(string id);
}

public sealed class ExternalVideoRepository(IMongoDatabase database) : IExternalVideoRepository
{
    private readonly IMongoCollection<ExternalVideoContent> _items = database.GetCollection<ExternalVideoContent>("ExternalVideos");
    public Task<List<ExternalVideoContent>> ListAsync(int page, int limit, ContentHubQuery query, bool publishedOnly) => _items.Find(BuildFilter(query, publishedOnly)).SortByDescending(x => x.IsFeatured).ThenByDescending(x => x.PublishedAt).ThenByDescending(x => x.CreatedAt).Skip((page - 1) * limit).Limit(limit).ToListAsync();
    public Task<long> CountAsync(ContentHubQuery query, bool publishedOnly) => _items.CountDocumentsAsync(BuildFilter(query, publishedOnly));
    public async Task<ExternalVideoContent?> GetByIdAsync(string id) => await _items.Find(x => x.Id == id).FirstOrDefaultAsync();
    public async Task<ExternalVideoContent?> GetBySlugAsync(string slug, bool publishedOnly) => await _items.Find(Builders<ExternalVideoContent>.Filter.And(Builders<ExternalVideoContent>.Filter.Eq(x => x.Slug, slug), publishedOnly ? Builders<ExternalVideoContent>.Filter.Eq(x => x.Status, "published") : Builders<ExternalVideoContent>.Filter.Empty)).FirstOrDefaultAsync();
    public Task<bool> SlugExistsAsync(string slug, string? exceptId = null) => _items.Find(Builders<ExternalVideoContent>.Filter.And(Builders<ExternalVideoContent>.Filter.Eq(x => x.Slug, slug), string.IsNullOrWhiteSpace(exceptId) ? Builders<ExternalVideoContent>.Filter.Empty : Builders<ExternalVideoContent>.Filter.Ne(x => x.Id, exceptId))).AnyAsync();
    public Task CreateAsync(ExternalVideoContent video) => _items.InsertOneAsync(video);
    public Task ReplaceAsync(ExternalVideoContent video) => _items.ReplaceOneAsync(x => x.Id == video.Id, video);
    public Task DeleteAsync(string id) => _items.DeleteOneAsync(x => x.Id == id);
    private static FilterDefinition<ExternalVideoContent> BuildFilter(ContentHubQuery query, bool publishedOnly)
    {
        var f = Builders<ExternalVideoContent>.Filter; var filters = new List<FilterDefinition<ExternalVideoContent>>();
        if (publishedOnly) filters.Add(f.Eq(x => x.Status, "published"));
        if (!string.IsNullOrWhiteSpace(query.Search)) { var value = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i"); filters.Add(f.Or(f.Regex(x => x.Title, value), f.Regex(x => x.Description, value))); }
        if (!string.IsNullOrWhiteSpace(query.Category)) filters.Add(f.Eq(x => x.CategoryId, query.Category.Trim()));
        if (!string.IsNullOrWhiteSpace(query.Tag)) filters.Add(f.AnyEq(x => x.Tags, query.Tag.Trim()));
        if (!string.IsNullOrWhiteSpace(query.Platform)) filters.Add(f.Eq(x => x.Platform, query.Platform.Trim()));
        if (query.FeaturedOnly == true) filters.Add(f.Eq(x => x.IsFeatured, true));
        return filters.Count == 0 ? f.Empty : f.And(filters);
    }
}

public sealed class KnowledgeArticleRepository(IMongoDatabase database) : IKnowledgeArticleRepository
{
    private readonly IMongoCollection<KnowledgeArticle> _items = database.GetCollection<KnowledgeArticle>("KnowledgeArticles");
    public Task<List<KnowledgeArticle>> ListAsync(int page, int limit, ContentHubQuery query, bool publishedOnly) => _items.Find(BuildFilter(query, publishedOnly)).SortByDescending(x => x.IsFeatured).ThenByDescending(x => x.PublishedAt).ThenByDescending(x => x.CreatedAt).Skip((page - 1) * limit).Limit(limit).ToListAsync();
    public Task<long> CountAsync(ContentHubQuery query, bool publishedOnly) => _items.CountDocumentsAsync(BuildFilter(query, publishedOnly));
    public async Task<KnowledgeArticle?> GetByIdAsync(string id) => await _items.Find(x => x.Id == id).FirstOrDefaultAsync();
    public async Task<KnowledgeArticle?> GetBySlugAsync(string slug, bool publishedOnly) => await _items.Find(Builders<KnowledgeArticle>.Filter.And(Builders<KnowledgeArticle>.Filter.Eq(x => x.Slug, slug), publishedOnly ? Builders<KnowledgeArticle>.Filter.Eq(x => x.Status, "published") : Builders<KnowledgeArticle>.Filter.Empty)).FirstOrDefaultAsync();
    public Task<bool> SlugExistsAsync(string slug, string? exceptId = null) => _items.Find(Builders<KnowledgeArticle>.Filter.And(Builders<KnowledgeArticle>.Filter.Eq(x => x.Slug, slug), string.IsNullOrWhiteSpace(exceptId) ? Builders<KnowledgeArticle>.Filter.Empty : Builders<KnowledgeArticle>.Filter.Ne(x => x.Id, exceptId))).AnyAsync();
    public Task CreateAsync(KnowledgeArticle article) => _items.InsertOneAsync(article);
    public Task ReplaceAsync(KnowledgeArticle article) => _items.ReplaceOneAsync(x => x.Id == article.Id, article);
    public Task DeleteAsync(string id) => _items.DeleteOneAsync(x => x.Id == id);
    private static FilterDefinition<KnowledgeArticle> BuildFilter(ContentHubQuery query, bool publishedOnly)
    {
        var f = Builders<KnowledgeArticle>.Filter; var filters = new List<FilterDefinition<KnowledgeArticle>>();
        if (publishedOnly) filters.Add(f.Eq(x => x.Status, "published"));
        if (!string.IsNullOrWhiteSpace(query.Search)) { var value = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i"); filters.Add(f.Or(f.Regex(x => x.Title, value), f.Regex(x => x.Excerpt, value), f.Regex(x => x.Subtitle, value))); }
        if (!string.IsNullOrWhiteSpace(query.Category)) filters.Add(f.Eq(x => x.CategoryId, query.Category.Trim()));
        if (!string.IsNullOrWhiteSpace(query.Tag)) filters.Add(f.AnyEq(x => x.Tags, query.Tag.Trim()));
        if (!string.IsNullOrWhiteSpace(query.ContentType)) filters.Add(f.Eq(x => x.ContentType, query.ContentType.Trim()));
        if (query.FeaturedOnly == true) filters.Add(f.Eq(x => x.IsFeatured, true));
        return filters.Count == 0 ? f.Empty : f.And(filters);
    }
}
