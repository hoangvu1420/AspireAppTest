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
    private const string AllBooksIdsCacheKey = "all_books_ids";
    private const string BookCacheKeyPrefix = "book_";

    public BookService(LibraryDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<Book>> GetBooksAsync()
    {
        var cachedBookIds = await _cache.GetStringAsync(AllBooksIdsCacheKey);
        List<int> bookIds;

        if (cachedBookIds is not null)
        {
            bookIds = JsonSerializer.Deserialize<List<int>>(cachedBookIds) ?? new List<int>();
        }
        else
        {
            bookIds = await _context.Books.Select(b => b.Id).ToListAsync();
            await _cache.SetStringAsync(AllBooksIdsCacheKey, JsonSerializer.Serialize(bookIds));
        }

        var books = new List<Book>();
        var cachedBooks = new Dictionary<int, Book>();

        foreach (var bookId in bookIds)
        {
            var cachedBook = await _cache.GetStringAsync($"{BookCacheKeyPrefix}{bookId}");
            if (cachedBook is not null)
            {
                cachedBooks[bookId] = JsonSerializer.Deserialize<Book>(cachedBook) ?? new Book();
            }
        }

        var missingBookIds = bookIds.Except(cachedBooks.Keys).ToList();
        if (missingBookIds.Any())
        {
            var fetchedBooks = await _context.Books.Where(b => missingBookIds.Contains(b.Id)).ToListAsync();
            foreach (var book in fetchedBooks)
            {
                await _cache.SetStringAsync($"{BookCacheKeyPrefix}{book.Id}", JsonSerializer.Serialize(book));
                books.Add(book);
            }
        }

        books.AddRange(cachedBooks.Values);
        return books.OrderBy(b => b.Id).ToList();
    }

    public async Task<Book?> GetBookByIdAsync(int id)
    {
        var cachedBook = await _cache.GetStringAsync($"{BookCacheKeyPrefix}{id}");
        if (cachedBook is not null)
        {
            return JsonSerializer.Deserialize<Book>(cachedBook);
        }

        var book = await _context.Books.FindAsync(id);
        if (book is not null)
        {
            await _cache.SetStringAsync($"{BookCacheKeyPrefix}{id}", JsonSerializer.Serialize(book));
        }
        return book;
    }

    public async Task<Book> AddBookAsync(Book book)
    {
        _context.Books.Add(book);
        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(AllBooksIdsCacheKey);
        return book;
    }



    public async Task UpdateBookAsync(Book book)
    {
        _context.Entry(book).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        await _cache.RemoveAsync($"{BookCacheKeyPrefix}{book.Id}");
    }

    public async Task DeleteBookAsync(int id)
    {
        var book = await _context.Books.FindAsync(id);
        if (book is not null)
        {
            _context.Books.Remove(book);
            await _context.SaveChangesAsync();
            await _cache.RemoveAsync(AllBooksIdsCacheKey);
            await _cache.RemoveAsync($"{BookCacheKeyPrefix}{id}");
        }
    }
}
