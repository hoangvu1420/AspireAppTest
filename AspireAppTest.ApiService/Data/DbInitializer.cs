using AspireAppTest.ApiService.Models;
using Microsoft.EntityFrameworkCore;

namespace AspireAppTest.ApiService.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(LibraryDbContext context, ILogger logger, IConfiguration configuration)
    {
        try
        {
            // Get and log the connection string
            var connectionString = configuration.GetConnectionString("LMSdb");
            logger.LogInformation("LMSdb Connection String: {ConnectionString}", connectionString);

            // First, ensure we can connect to the database
            logger.LogInformation("Testing database connection...");
            await context.Database.CanConnectAsync();
            logger.LogInformation("Database connection successful.");

            // Check for pending migrations and apply them
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying {Count} pending database migrations...", pendingMigrations.Count());
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied successfully.");
            }
            else
            {
                logger.LogInformation("Database is up to date.");
            }

            // Seed data if database is empty
            if (!await context.Books.AnyAsync())
            {
                logger.LogInformation("Seeding database with sample books...");
                await SeedBooksAsync(context);
                logger.LogInformation("Database seeding completed.");
            }
            else
            {
                logger.LogInformation("Database already contains book data.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the database.");
            throw;
        }
    }

    private static async Task SeedBooksAsync(LibraryDbContext context)
    {
        var books = new List<Book>
        {
            new() { 
                Title = "The Great Gatsby", 
                Author = "F. Scott Fitzgerald", 
                PublicationYear = 1925 
            },
            new() { 
                Title = "To Kill a Mockingbird", 
                Author = "Harper Lee", 
                PublicationYear = 1960 
            },
            new() { 
                Title = "1984", 
                Author = "George Orwell", 
                PublicationYear = 1949 
            },
            new() { 
                Title = "Pride and Prejudice", 
                Author = "Jane Austen", 
                PublicationYear = 1813 
            },
            new() { 
                Title = "The Catcher in the Rye", 
                Author = "J.D. Salinger", 
                PublicationYear = 1951 
            },
            new() { 
                Title = "Lord of the Flies", 
                Author = "William Golding", 
                PublicationYear = 1954 
            },
            new() { 
                Title = "The Hobbit", 
                Author = "J.R.R. Tolkien", 
                PublicationYear = 1937 
            },
            new() { 
                Title = "Fahrenheit 451", 
                Author = "Ray Bradbury", 
                PublicationYear = 1953 
            },
            new() { 
                Title = "Jane Eyre", 
                Author = "Charlotte Brontë", 
                PublicationYear = 1847 
            },
            new() { 
                Title = "The Lord of the Rings", 
                Author = "J.R.R. Tolkien", 
                PublicationYear = 1954 
            }
        };

        await context.Books.AddRangeAsync(books);
        await context.SaveChangesAsync();
    }
}
