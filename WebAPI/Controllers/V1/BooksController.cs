using Asp.Versioning;
using WebAPI.Models;
using WebAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;

namespace WebAPI.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/books")]
[Route("api/books")] // fallback для клиентов без версии
[Produces("application/json")]
public class BooksController : ControllerBase
{
    private readonly IBookRepository _repo;
    private readonly IFeatureManager _features;
    private readonly ILogger<BooksController> _logger;

    public BooksController(
        IBookRepository repo,
        IFeatureManager features,
        ILogger<BooksController> logger)
    {
        _repo = repo;
        _features = features;
        _logger = logger;
    }

    /// <summary>Получить список книг (v1).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BookV1Response>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        // Feature flag: постепенно включаем поле fullName, не удаляя author
        var useFullName = await _features.IsEnabledAsync("UseFullNameField");

        var result = _repo.GetAll().Select(b => new
        {
            b.Id,
            b.Title,
            b.Author,                                      // остаётся для совместимости
            FullName = useFullName ? b.Author : null,      // новое поле под флагом
            b.Price,
            b.Isbn
        });

        return Ok(result);
    }

    /// <summary>Получить книгу по id (v1).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BookV1Response), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<BookV1Response> GetById(int id)
    {
        var book = _repo.GetById(id);
        if (book is null)
        {
            _logger.LogInformation("Book {Id} not found", id);
            return NotFound(new { message = $"Book {id} not found" });
        }

        return Ok(new BookV1Response
        {
            Id = book.Id,
            Title = book.Title,
            Author = book.Author,
            Price = book.Price,
            Isbn = book.Isbn
        });
    }

    /// <summary>Создать книгу (v1).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BookV1Response), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<BookV1Response> Create([FromBody] CreateBookRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var created = _repo.Add(new Book
        {
            Title = request.Title,
            Author = request.Author,
            Price = request.Price,
            Isbn = request.Isbn
        });

        var response = new BookV1Response
        {
            Id = created.Id,
            Title = created.Title,
            Author = created.Author,
            Price = created.Price,
            Isbn = created.Isbn
        };

        return CreatedAtAction(nameof(GetById),
            new { version = "1.0", id = created.Id }, response);
    }

    /// <summary>Удалить книгу (v1).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(int id)
        => _repo.Delete(id) ? NoContent() : NotFound();
}