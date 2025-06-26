using AspireAppTest.ApiService.Models;

namespace AspireAppTest.ApiService.Services;

public interface IBookService
{
    Task<List<Book>> GetBooksAsync();
    Task<Book?> GetBookByIdAsync(int id);
    Task<Book> AddBookAsync(Book book);
    Task UpdateBookAsync(Book book);
    Task DeleteBookAsync(int id);
}