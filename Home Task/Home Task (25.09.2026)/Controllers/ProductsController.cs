using Microsoft.AspNetCore.Mvc;
using Samost.DTO;
using Samost.Services;

namespace Samost.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _service;

    public ProductsController(ProductService service) => _service = service;

    [HttpPost]
    public IActionResult Create([FromBody] CreateProductDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var product = _service.Add(dto);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var product = _service.GetById(id);
        if (product is null) return NotFound();
        var etag = product.GetETag();

        if (Request.Headers.TryGetValue("If-None-Match", out var clientEtag) && clientEtag == etag)
        {

            Response.Headers.ETag = etag;
            return StatusCode(304);
        }

        Response.Headers.ETag = etag;
        return Ok(product);
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(_service.GetAll());

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] CreateProductDto dto)
    {
        var product = _service.GetById(id);
        if (product is null) return NotFound();

        var currentEtag = product.GetETag();

        if (Request.Headers.TryGetValue("If-Match", out var clientEtag) && clientEtag != currentEtag)
        {
            return StatusCode(412, "Precondition Failed: Товар был изменен кем-то другим.");
        }

        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        _service.Update(id, dto);

        Response.Headers.ETag = product.GetETag();
        return Ok(product);
    }
}