using Asp.Versioning;
using WebAPI.Models;
using WebAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/books")]
[Produces("application/json")]
public class BooksController : ControllerBase
{
    private readonly IBookRepository _repo;

    public BooksController(IBookRepository repo) => _repo = repo;

    /// <summary>Получить список книг (v2). Price — объект Money.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BookV2Response>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<BookV2Response>> GetAll()
        => Ok(_repo.GetAll().Select(Map));

    /// <summary>Получить книгу по id (v2).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BookV2Response), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<BookV2Response> GetById(int id)
    {
        var book = _repo.GetById(id);
        return book is null ? NotFound() : Ok(Map(book));
    }

    /// <summary>Создать книгу (v2). В теле — Money вместо decimal.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BookV2Response), StatusCodes.Status201Created)]
    public ActionResult<BookV2Response> Create([FromBody] CreateBookV2Request request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var created = _repo.Add(new Book
        {
            Title = request.Title,
            Author = request.Author,
            Price = request.Price.Amount,
            Isbn = request.Isbn
        });

        return CreatedAtAction(nameof(GetById),
            new { version = "2.0", id = created.Id }, Map(created));
    }

    private static BookV2Response Map(Book book) => new()
    {
        Id = book.Id,
        Title = book.Title,
        Author = book.Author,
        Isbn = book.Isbn,
        Price = new Money { Amount = book.Price, Currency = "RUB" }
    };
}

public class CreateBookV2Request
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public Money Price { get; set; } = new();
    public string? Isbn { get; set; }
}