using AspireAppTest.ApiService.Data;
using AspireAppTest.ApiService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace AspireAppTest.ApiService.Services;

public class BookService : IBookService
{
    private readonly LibraryDbContext _context;
    private readonly IDistributedCache _cache;
    private const string AllBooksCacheKey = "all_books";

    public BookService(LibraryDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<Book>> GetBooksAsync()
    {
        var cachedBooks = await _cache.GetStringAsync(AllBooksCacheKey);
        if (cachedBooks is not null)
        {
            return JsonSerializer.Deserialize<List<Book>>(cachedBooks) ?? new List<Book>();
        }

        var books = await _context.Books.ToListAsync();
        await _cache.SetStringAsync(AllBooksCacheKey, JsonSerializer.Serialize(books));
        return books;
    }

    public async Task<Book?> GetBookByIdAsync(int id)
    {
        return await _context.Books.FindAsync(id);
    }

    public async Task<Book> AddBookAsync(Book book)
    {
        _context.Books.Add(book);
        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(AllBooksCacheKey);
        return book;
    }

    public async Task UpdateBookAsync(Book book)
    {
        _context.Entry(book).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(AllBooksCacheKey);
    }

    public async Task DeleteBookAsync(int id)
    {
        var book = await _context.Books.FindAsync(id);
        if (book is not null)
        {
            _context.Books.Remove(book);
            await _context.SaveChangesAsync();
            await _cache.RemoveAsync(AllBooksCacheKey);
        }
    }
}