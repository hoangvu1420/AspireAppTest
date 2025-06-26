using System.ComponentModel.DataAnnotations;

namespace AspireAppTest.Web.Models;

public class Book
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string? Title { get; set; }
    
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? Author { get; set; }
    
    [Range(1, 9999)]
    public int PublicationYear { get; set; }
}