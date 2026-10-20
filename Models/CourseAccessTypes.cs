namespace Educatinal_Platform.Models;

/// <summary>
/// Server-owned course access plans and backwards-compatible price resolution.
/// </summary>
public static class CourseAccessTypes
{
    public const string Sequential = "Sequential";
    public const string Full = "Full";

    public static string Resolve(string? accessType) =>
        string.Equals(accessType, Sequential, StringComparison.OrdinalIgnoreCase)
            ? Sequential
            : Full;

    public static string RequireKnown(string? accessType)
    {
        if (string.IsNullOrWhiteSpace(accessType))
            return Full;

        if (string.Equals(accessType, Sequential, StringComparison.OrdinalIgnoreCase))
            return Sequential;
        if (string.Equals(accessType, Full, StringComparison.OrdinalIgnoreCase))
            return Full;

        throw new ArgumentException("Access type must be Sequential or Full.");
    }

    public static decimal BasicPrice(Course course) => course.BasicPrice ?? course.Price;

    public static decimal FullAccessPrice(Course course) => course.FullAccessPrice ?? course.Price;

    public static decimal PriceFor(Course course, string accessType) =>
        Resolve(accessType) == Sequential ? BasicPrice(course) : FullAccessPrice(course);
}
