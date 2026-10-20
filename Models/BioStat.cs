namespace Educatinal_Platform.Models;

public class BioStat
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Icon { get; set; } = "linkedin";
    public string Count { get; set; } = "";
    public string LabelEn { get; set; } = "";
    public string LabelAr { get; set; } = "";
    public string Url { get; set; } = "";
    public bool IsVisible { get; set; } = true;
}
