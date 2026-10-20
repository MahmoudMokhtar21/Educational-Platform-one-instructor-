using Educatinal_Platform.Models;
using MongoDB.Driver;

namespace Educatinal_Platform.Repositories;

public interface ISiteSettingsRepository { Task<SiteSettings> GetAsync(); Task SaveAsync(SiteSettings settings); }
public class SiteSettingsRepository(IMongoDatabase database) : ISiteSettingsRepository
{
    private readonly IMongoCollection<SiteSettings> _settings = database.GetCollection<SiteSettings>("SiteSettings");
    public async Task<SiteSettings> GetAsync()
    {
        var settings = await _settings.Find(item => item.Key == "public-site").FirstOrDefaultAsync();
        if (settings != null)
        {
            // Older public-site documents do not have the consultation copy
            // fields. Normalize those records once so GET responses and later
            // admin updates have stable empty-string values.
            var normalizedConsultationCopy = false;
            if (settings.ConsultationDescriptionEn is null)
            {
                settings.ConsultationDescriptionEn = string.Empty;
                normalizedConsultationCopy = true;
            }
            if (settings.ConsultationDescriptionAr is null)
            {
                settings.ConsultationDescriptionAr = string.Empty;
                normalizedConsultationCopy = true;
            }
            if (settings.BioStats is null || settings.BioStats.Count == 0)
            {
                settings.BioStats = BioStatDefaults.Create();
                await SaveAsync(settings);
            }
            else if (settings.BioStats.Any(stat => string.IsNullOrWhiteSpace(stat.Id)))
            {
                foreach (var stat in settings.BioStats.Where(stat => string.IsNullOrWhiteSpace(stat.Id))) stat.Id = Guid.NewGuid().ToString("N");
                await SaveAsync(settings);
            }
            else if (normalizedConsultationCopy)
            {
                await SaveAsync(settings);
            }
            return settings;
        }
        settings = new SiteSettings { SocialLinks = new(), NavigationLinks = new() };
        await _settings.InsertOneAsync(settings);
        return settings;
    }
    public Task SaveAsync(SiteSettings settings) => _settings.ReplaceOneAsync(item => item.Id == settings.Id, settings, new ReplaceOptions { IsUpsert = true });
}
