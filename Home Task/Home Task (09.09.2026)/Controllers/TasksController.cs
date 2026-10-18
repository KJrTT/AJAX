using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Services;
namespace MyMvcApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskRepository _repo;
    public TasksController(ITaskRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, CancellationToken ct)
    {
        var items = string.IsNullOrWhiteSpace(status)
            ? await _repo.GetAllAsync(ct)
            : await _repo.GetByStatusAsync(status, ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var task = await _repo.GetByIdAsync(id, ct);
        return task is null ? NotFound() : Ok(task);
    }
}