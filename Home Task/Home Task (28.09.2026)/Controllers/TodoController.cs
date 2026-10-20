using Microsoft.AspNetCore.Mvc;

namespace SwaggerHomework.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TodoController : ControllerBase
{
    private static readonly List<TodoItem> _todos = new()
    {
        new TodoItem { Id = 1, Title = "Сделать ДЗ по Swagger", IsCompleted = false }
    };

    [HttpGet]
    public IActionResult GetAll() => Ok(_todos);

    [HttpPost]
    public IActionResult Create([FromBody] TodoItem item)
    {
        item.Id = _todos.Count + 1;
        _todos.Add(item);
        return CreatedAtAction(nameof(GetAll), new { id = item.Id }, item);
    }
}