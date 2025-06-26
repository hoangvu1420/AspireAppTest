using AspireAppTest.ApiService.Models;
using AspireAppTest.ApiService.Services;

namespace AspireAppTest.ApiService.Endpoints;

public static class BookEndpoints
{
    public static void MapBookEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/books");

        group.MapGet("/", async (IBookService bookService) =>
        {
            return await bookService.GetBooksAsync();
        });

        group.MapGet("/{id}", async (int id, IBookService bookService) =>
        {
            var book = await bookService.GetBookByIdAsync(id);
            return book is not null ? Results.Ok(book) : Results.NotFound();
        });

        group.MapPost("/", async (Book book, IBookService bookService) =>
        {
            var newBook = await bookService.AddBookAsync(book);
            return Results.Created($"/books/{newBook.Id}", newBook);
        });

        group.MapPut("/{id}", async (int id, Book book, IBookService bookService) =>
        {
            if (id != book.Id)
            {
                return Results.BadRequest();
            }

            await bookService.UpdateBookAsync(book);
            return Results.NoContent();
        });

        group.MapDelete("/{id}", async (int id, IBookService bookService) =>
        {
            await bookService.DeleteBookAsync(id);
            return Results.NoContent();
        });
    }
}