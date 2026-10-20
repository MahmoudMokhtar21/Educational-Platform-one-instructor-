using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace Educatinal_Platform.Models;
public class StudentReview { [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString(); public string Name { get; set; } = ""; public string Initials { get; set; } = ""; public int Rating { get; set; } public string ReviewText { get; set; } = ""; public DateTime CreatedAt { get; set; } = DateTime.UtcNow; public DateTime UpdatedAt { get; set; } = DateTime.UtcNow; }
