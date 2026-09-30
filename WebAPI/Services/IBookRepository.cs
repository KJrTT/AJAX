using WebAPI.Models;

namespace WebAPI.Services;

public interface IBookRepository
{
    IEnumerable<Book> GetAll();
    Book? GetById(int id);
    Book Add(Book book);
    bool Delete(int id);
}