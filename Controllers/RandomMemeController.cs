using MARS.Server.Services.RandomMem;
using MARS.Server.Services.RandomMem.DTOs;
using MARS.Server.Services.RandomMem.Entity;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RandomMemeController(
    IRandomMemeService randomMemeService,
    ILogger<RandomMemeController> logger
) : ControllerBase
{
    #region MemeType Endpoints

    /// <summary>
    /// Get all meme types
    /// </summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(IEnumerable<MemeTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<MemeTypeDto>>> GetAllMemeTypes(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var memeTypes = await randomMemeService.GetAllMemeTypesAsync(cancellationToken);
            var dtos = memeTypes.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting all meme types");
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Get meme type by ID
    /// </summary>
    [HttpGet("types/{id:int}")]
    [ProducesResponseType(typeof(MemeTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MemeTypeDto>> GetMemeTypeById(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var memeType = await randomMemeService.GetMemeTypeByIdAsync(id, cancellationToken);
            return memeType == null
                ? NotFound($"MemeType with ID {id} not found")
                : Ok(MapToDto(memeType));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting meme type with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Create new meme type
    /// </summary>
    [HttpPost("types")]
    [ProducesResponseType(typeof(MemeTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MemeTypeDto>> CreateMemeType(
        CreateMemeTypeDto createDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var memeType = new MemeType
            {
                Name = createDto.Name,
                FolderPath = createDto.FolderPath,
            };

            var created = await randomMemeService.CreateMemeTypeAsync(memeType, cancellationToken);
            var dto = MapToDto(created);

            return CreatedAtAction(nameof(GetMemeTypeById), new { id = created.Id }, dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while creating meme type");
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Update meme type
    /// </summary>
    [HttpPut("types/{id:int}")]
    [ProducesResponseType(typeof(MemeTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MemeTypeDto>> UpdateMemeType(
        int id,
        UpdateMemeTypeDto updateDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var memeType = new MemeType
            {
                Id = id,
                Name = updateDto.Name,
                FolderPath = updateDto.FolderPath,
            };

            var updated = await randomMemeService.UpdateMemeTypeAsync(memeType, cancellationToken);
            return Ok(MapToDto(updated));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while updating meme type with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Delete meme type
    /// </summary>
    [HttpDelete("types/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteMemeType(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await randomMemeService.DeleteMemeTypeAsync(id, cancellationToken);
            return !result ? NotFound($"MemeType with ID {id} not found") : NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while deleting meme type with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    #endregion

    #region MemeOrder Endpoints

    /// <summary>
    /// Get all meme orders
    /// </summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(IEnumerable<MemeOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<MemeOrderDto>>> GetAllMemeOrders(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var memeOrders = await randomMemeService.GetAllMemeOrdersAsync(cancellationToken);
            var dtos = memeOrders.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting all meme orders");
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Get meme orders by type
    /// </summary>
    [HttpGet("orders/type/{typeId:int}")]
    [ProducesResponseType(typeof(IEnumerable<MemeOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<MemeOrderDto>>> GetMemeOrdersByType(
        int typeId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var memeOrders = await randomMemeService.GetMemeOrdersByTypeAsync(
                typeId,
                cancellationToken
            );
            var dtos = memeOrders.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error occurred while getting meme orders for type {TypeId}",
                typeId
            );
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Get meme order by ID
    /// </summary>
    [HttpGet("orders/{id:guid}")]
    [ProducesResponseType(typeof(MemeOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MemeOrderDto>> GetMemeOrderById(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var memeOrder = await randomMemeService.GetMemeOrderByIdAsync(id, cancellationToken);
            return memeOrder == null
                ? NotFound($"MemeOrder with ID {id} not found")
                : Ok(MapToDto(memeOrder));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting meme order with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Create new meme order
    /// </summary>
    [HttpPost("orders")]
    [ProducesResponseType(typeof(MemeOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MemeOrderDto>> CreateMemeOrder(
        CreateMemeOrderDto createDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var memeOrder = new MemeOrder
            {
                FilePath = createDto.FilePath,
                MemeTypeId = createDto.MemeTypeId,
            };

            var created = await randomMemeService.CreateMemeOrderAsync(
                memeOrder,
                cancellationToken
            );
            var dto = MapToDto(created);

            return CreatedAtAction(nameof(GetMemeOrderById), new { id = created.Id }, dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while creating meme order");
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Update meme order
    /// </summary>
    [HttpPut("orders/{id:guid}")]
    [ProducesResponseType(typeof(MemeOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MemeOrderDto>> UpdateMemeOrder(
        Guid id,
        UpdateMemeOrderDto updateDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var memeOrder = new MemeOrder
            {
                Id = id,
                FilePath = updateDto.FilePath,
                MemeTypeId = updateDto.MemeTypeId,
                Order = updateDto.Order,
            };

            var updated = await randomMemeService.UpdateMemeOrderAsync(
                memeOrder,
                cancellationToken
            );
            return Ok(MapToDto(updated));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while updating meme order with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Delete meme order
    /// </summary>
    [HttpDelete("orders/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteMemeOrder(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await randomMemeService.DeleteMemeOrderAsync(id, cancellationToken);
            return !result ? NotFound($"MemeOrder with ID {id} not found") : NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while deleting meme order with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    #endregion

    #region Additional Endpoints

    /// <summary>
    /// Get random meme
    /// </summary>
    [HttpGet("random")]
    [ProducesResponseType(typeof(MemeOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MemeOrderDto>> GetRandomMeme(
        [FromQuery] int? typeId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var randomMeme = await randomMemeService.GetRandomMemeAsync(typeId, cancellationToken);
            return randomMeme == null ? NotFound("No memes found") : Ok(MapToDto(randomMeme));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting random meme");
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Get meme count
    /// </summary>
    [HttpGet("count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<int>> GetMemeCount(
        [FromQuery] int? typeId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var count = await randomMemeService.GetMemeOrderCountAsync(typeId, cancellationToken);
            return Ok(count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting meme count");
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Get meme file by ID
    /// </summary>
    [HttpGet("file/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMemeFile(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var memeOrder = await randomMemeService.GetMemeOrderByIdAsync(id, cancellationToken);
            if (memeOrder == null)
            {
                return NotFound($"MemeOrder with ID {id} not found");
            }

            if (!memeOrder.MemeTypeId.HasValue)
            {
                return NotFound($"MemeOrder with ID {id} has no associated MemeType");
            }

            var memeType = await randomMemeService.GetMemeTypeByIdAsync(
                memeOrder.MemeTypeId.Value,
                cancellationToken
            );
            if (memeType == null)
            {
                return NotFound($"MemeType with ID {memeOrder.MemeTypeId} not found");
            }

            var fullFilePath = Path.Combine(memeType.FolderPath, memeOrder.FilePath);
            if (!System.IO.File.Exists(fullFilePath))
            {
                return NotFound($"File not found at path: {fullFilePath}");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullFilePath, cancellationToken);
            var fileName = Path.GetFileName(memeOrder.FilePath);
            var contentType = GetContentType(fileName);

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting meme file with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Get random meme file
    /// </summary>
    [HttpGet("file/random")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRandomMemeFile(
        [FromQuery] int? typeId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var randomMeme = await randomMemeService.GetRandomMemeAsync(typeId, cancellationToken);
            if (randomMeme == null)
            {
                return NotFound("No memes found");
            }

            if (!randomMeme.MemeTypeId.HasValue)
            {
                return NotFound("Random meme has no associated MemeType");
            }

            var memeType = await randomMemeService.GetMemeTypeByIdAsync(
                randomMeme.MemeTypeId.Value,
                cancellationToken
            );
            if (memeType == null)
            {
                return NotFound($"MemeType with ID {randomMeme.MemeTypeId} not found");
            }

            var fullFilePath = Path.Combine(memeType.FolderPath, randomMeme.FilePath);
            if (!System.IO.File.Exists(fullFilePath))
            {
                return NotFound($"File not found at path: {fullFilePath}");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullFilePath, cancellationToken);
            var fileName = Path.GetFileName(randomMeme.FilePath);
            var contentType = GetContentType(fileName);

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting random meme file");
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Reorder meme orders for a specific type
    /// </summary>
    [HttpPost("orders/reorder/{typeId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string>> ReorderMemeOrders(
        int typeId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await randomMemeService.ReorderMemeOrdersAsync(typeId, cancellationToken);
            return Ok($"Successfully reordered meme orders for type {typeId}");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error occurred while reordering meme orders for type {TypeId}",
                typeId
            );
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    #endregion

    #region Helper Methods

    private static MemeTypeDto MapToDto(MemeType memeType)
    {
        return new MemeTypeDto
        {
            Id = memeType.Id,
            Name = memeType.Name,
            FolderPath = memeType.FolderPath,
        };
    }

    private static MemeOrderDto MapToDto(MemeOrder memeOrder)
    {
        return new MemeOrderDto
        {
            Id = memeOrder.Id,
            Order = memeOrder.Order,
            FilePath = memeOrder.FilePath,
            MemeTypeId = memeOrder.MemeTypeId,
            Type = memeOrder.Type != null ? MapToDto(memeOrder.Type) : null,
        };
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".wmv" => "video/x-ms-wmv",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream",
        };
    }

    #endregion
}
