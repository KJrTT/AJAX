using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Services;
namespace MyMvcApp.Controllers;

[ApiController]
[Route("api/tasks/{taskId:int}/versions")]
public class TaskVersionsController : ControllerBase
{
    private readonly ITaskVersionRepository _repo;
    public TaskVersionsController(ITaskVersionRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<IActionResult> GetAll(int taskId, CancellationToken ct)
    {
        var versions = await _repo.GetByTaskIdAsync(taskId, ct);
        return Ok(versions);
    }

    [HttpGet("{versionNumber:int}")]
    public async Task<IActionResult> GetByVersion(int taskId, int versionNumber, CancellationToken ct)
    {
        var v = await _repo.GetByVersionAsync(taskId, versionNumber, ct);
        return v is null ? NotFound() : Ok(v);
    }
}