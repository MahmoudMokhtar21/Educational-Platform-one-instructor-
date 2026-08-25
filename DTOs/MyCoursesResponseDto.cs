namespace Educatinal_Platform.DTOs
{
    public class MyCoursesResponseDto
    {
        public List<EnrollmentResponseDto> ActiveCourses { get; set; } = new();
        public List<EnrollmentResponseDto> CompletedCourses { get; set; } = new();
        public int TotalActive { get; set; }
        public int TotalCompleted { get; set; }
        public decimal TotalProgress { get; set; } // average of progress for students
    }

  
}
