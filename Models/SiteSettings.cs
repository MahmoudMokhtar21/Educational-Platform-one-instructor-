using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Educatinal_Platform.Models;

public class SiteSettings
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = "";
    public string Key { get; set; } = "public-site";
    public string PlatformName { get; set; } = "Northstar Learning";
    public string AboutTitle { get; set; } = "Learning that opens doors";
    public string AboutContent { get; set; } = "Northstar Learning helps ambitious learners build practical skills at their own pace.";
    public string AboutTitleEn { get; set; } = "";
    public string AboutTitleAr { get; set; } = "";
    public string AboutContentEn { get; set; } = "";
    public string AboutContentAr { get; set; } = "";
    public bool IsAboutVisible { get; set; } = true;
    public string WhoWeAreTitleEn { get; set; } = "";
    public string WhoWeAreTitleAr { get; set; } = "";
    public string WhoWeAreContentEn { get; set; } = "";
    public string WhoWeAreContentAr { get; set; } = "";
    public bool IsWhoWeAreVisible { get; set; } = true;
    public string BioTitleEn { get; set; } = "";
    public string BioTitleAr { get; set; } = "";
    public string BioDescriptionEn { get; set; } = "";
    public string BioDescriptionAr { get; set; } = "";
    // Public consultation introduction, authored independently for each locale.
    // These remain part of the single public-site settings document so updating
    // the copy does not introduce a second configuration source.
    public string ConsultationDescriptionEn { get; set; } = "";
    public string ConsultationDescriptionAr { get; set; } = "";
    public List<BioStat> BioStats { get; set; } = BioStatDefaults.Create();
    public Dictionary<string, string> SocialLinks { get; set; } = new();
    public Dictionary<string, bool> SocialLinkVisibility { get; set; } = new();
    public Dictionary<string, string> NavigationLinks { get; set; } = new();
    public Dictionary<string, string> NavigationLabels { get; set; } = new();
}

public static class BioStatDefaults
{
    public static List<BioStat> Create() =>
    [
        new() { Icon = "linkedin", Count = "700K+", LabelEn = "LinkedIn community", LabelAr = "مجتمع لينكدإن", Url = "https://www.linkedin.com" },
        new() { Icon = "youtube", Count = "633K+", LabelEn = "YouTube subscribers", LabelAr = "مشتركو يوتيوب", Url = "https://www.youtube.com" },
        new() { Icon = "linkedin", Count = "286K+", LabelEn = "LinkedIn followers", LabelAr = "متابعو لينكدإن", Url = "https://www.linkedin.com" },
        new() { Icon = "facebook", Count = "526K+", LabelEn = "Facebook followers", LabelAr = "متابعو فيسبوك", Url = "https://www.facebook.com" },
        new() { Icon = "chart", Count = "162K+", LabelEn = "Trainees reached", LabelAr = "متدرب تم الوصول إليهم", Url = "https://example.com" }
    ];
}
