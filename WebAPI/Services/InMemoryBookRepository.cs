using WebAPI.Models;

namespace WebAPI.Services;

public class InMemoryBookRepository : IBookRepository
{
    private readonly List<Book> _books = new()
    {
        new Book { Id = 1, Title = "CLR via C#",            Author = "Jeffrey Richter", Price = 3500m, Isbn = "978-0735667457" },
        new Book { Id = 2, Title = "ASP.NET Core in Action", Author = "Andrew Lock",     Price = 4200m, Isbn = "978-1633438620" },
        new Book { Id = 3, Title = "Domain-Driven Design",   Author = "Eric Evans",      Price = 5100m, Isbn = "978-0321125217" }
    };

    private readonly object _lock = new();
    private int _nextId = 4;

    public IEnumerable<Book> GetAll() => _books.ToList();

    public Book? GetById(int id) => _books.FirstOrDefault(b => b.Id == id);

    public Book Add(Book book)
    {
        lock (_lock)
        {
            book.Id = _nextId++;
            _books.Add(book);
            return book;
        }
    }

    public bool Delete(int id)
    {
        lock (_lock)
        {
            var book = _books.FirstOrDefault(b => b.Id == id);
            return book is not null && _books.Remove(book);
        }
    }
}