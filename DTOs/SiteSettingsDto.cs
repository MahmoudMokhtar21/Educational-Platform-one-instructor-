namespace Educatinal_Platform.DTOs;

public class SiteSettingsDto
{
    public string PlatformName { get; set; } = "";
    public string AboutTitle { get; set; } = "";
    public string AboutContent { get; set; } = "";
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
    public string ConsultationDescriptionEn { get; set; } = "";
    public string ConsultationDescriptionAr { get; set; } = "";
    public List<BioStatDto> BioStats { get; set; } = [];
    public Dictionary<string, string> SocialLinks { get; set; } = new();
    public Dictionary<string, bool> SocialLinkVisibility { get; set; } = new();
    public Dictionary<string, string> NavigationLinks { get; set; } = new();
    public Dictionary<string, string> NavigationLabels { get; set; } = new();
}

public class BioStatDto { public string Id { get; set; } = ""; public string Icon { get; set; } = ""; public string Count { get; set; } = ""; public string LabelEn { get; set; } = ""; public string LabelAr { get; set; } = ""; public string Url { get; set; } = ""; public bool IsVisible { get; set; } = true; }
