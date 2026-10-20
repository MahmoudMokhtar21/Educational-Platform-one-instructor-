namespace Educatinal_Platform.DTOs;
public class StudentReviewUpsertDto { public string Name { get; set; } = ""; public string Initials { get; set; } = ""; public int Rating { get; set; } public string ReviewText { get; set; } = ""; }
public class StudentReviewDto : StudentReviewUpsertDto { public string Id { get; set; } = ""; public DateTime CreatedAt { get; set; } public DateTime UpdatedAt { get; set; } }
