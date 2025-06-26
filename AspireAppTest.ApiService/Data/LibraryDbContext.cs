using AspireAppTest.ApiService.Models;
using Microsoft.EntityFrameworkCore;

namespace AspireAppTest.ApiService.Data;

public class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    public DbSet<Book> Books { get; set; }
}