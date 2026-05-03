using MARS.Server.Services;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service.DTOs;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service.Entity;
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
    [ProducesResponseType(typeof(OperationResult<List<MemeTypeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult<List<MemeTypeDto>>>> GetAllMemeTypes(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<MemeTypeDto>>> result = null!;

        try
        {
            var memeTypes = await randomMemeService.GetAllMemeTypesAsync(cancellationToken);
            var dtos = memeTypes.Select(MapToDto).ToList();
            result = Ok(OperationResult<List<MemeTypeDto>>.Ok("Получены все типы мемов", dtos));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting all meme types");
            result = Ok(
                OperationResult<List<MemeTypeDto>>.Bad("Ошибка при получении типов мемов", [])
            );
        }

        return result;
    }

    /// <summary>
    /// Get meme type by ID
    /// </summary>
    [HttpGet("types/{id:int}")]
    [ProducesResponseType(typeof(OperationResult<MemeTypeDto?>), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult<MemeTypeDto?>>> GetMemeTypeById(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<MemeTypeDto?>> result;
        try
        {
            var memeType = await randomMemeService.GetMemeTypeByIdAsync(id, cancellationToken);

            if (memeType != null)
            {
                result = Ok(
                    OperationResult<MemeTypeDto?>.Ok("Тип мема найден", MapToDto(memeType))
                );
            }
            else
            {
                result = Ok(
                    OperationResult<MemeTypeDto?>.Bad($"MemeType with ID {id} not found", null)
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting meme type with ID {Id}", id);
            result = Ok(OperationResult<MemeTypeDto?>.Bad("Ошибка при получении типа мема", null));
        }

        return result;
    }

    /// <summary>
    /// Create new meme type
    /// </summary>
    [HttpPost("types")]
    [ProducesResponseType(typeof(OperationResult<MemeTypeDto?>), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult<MemeTypeDto?>>> CreateMemeType(
        CreateMemeTypeDto createDto,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<MemeTypeDto?>> result;
        try
        {
            if (!ModelState.IsValid)
            {
                result = Ok(OperationResult<MemeTypeDto?>.Bad("Некорректные данные модели", null));
            }
            else
            {
                var memeType = new MemeType
                {
                    Name = createDto.Name,
                    FolderPath = createDto.FolderPath,
                };

                var created = await randomMemeService.CreateMemeTypeAsync(
                    memeType,
                    cancellationToken
                );
                var dto = MapToDto(created);

                result = Ok(OperationResult<MemeTypeDto?>.Ok("Тип мема успешно создан", dto));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while creating meme type");
            result = Ok(OperationResult<MemeTypeDto?>.Bad("Ошибка при создании типа мема", null));
        }

        return result;
    }

    /// <summary>
    /// Update meme type
    /// </summary>
    [HttpPut("types/{id:int}")]
    [ProducesResponseType(typeof(OperationResult<MemeTypeDto?>), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult<MemeTypeDto?>>> UpdateMemeType(
        int id,
        UpdateMemeTypeDto updateDto,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<MemeTypeDto?>> result;
        try
        {
            if (!ModelState.IsValid)
            {
                result = Ok(OperationResult<MemeTypeDto?>.Bad("Некорректные данные модели", null));
            }
            else
            {
                var memeType = new MemeType
                {
                    Id = id,
                    Name = updateDto.Name,
                    FolderPath = updateDto.FolderPath,
                };

                var updated = await randomMemeService.UpdateMemeTypeAsync(
                    memeType,
                    cancellationToken
                );
                result = Ok(
                    OperationResult<MemeTypeDto?>.Ok("Тип мема успешно обновлен", MapToDto(updated))
                );
            }
        }
        catch (InvalidOperationException ex)
        {
            result = Ok(OperationResult<MemeTypeDto?>.Bad(ex.Message, null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while updating meme type with ID {Id}", id);
            result = Ok(OperationResult<MemeTypeDto?>.Bad("Ошибка при обновлении типа мема", null));
        }

        return result;
    }

    /// <summary>
    /// Delete meme type
    /// </summary>
    [HttpDelete("types/{id:int}")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> DeleteMemeType(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            var deleteResult = await randomMemeService.DeleteMemeTypeAsync(id, cancellationToken);

            if (deleteResult)
            {
                result = Ok(OperationResult.Ok("Тип мема успешно удален"));
            }
            else
            {
                result = Ok(OperationResult.Bad($"MemeType with ID {id} not found"));
            }
        }
        catch (InvalidOperationException ex)
        {
            result = Ok(OperationResult.Bad(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while deleting meme type with ID {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка при удалении типа мема"));
        }

        return result;
    }

    #endregion

    #region MemeOrder Endpoints

    /// <summary>
    /// Get all meme orders
    /// </summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(OperationResult<List<MemeOrderDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult<List<MemeOrderDto>>>> GetAllMemeOrders(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<MemeOrderDto>>> result = null!;

        try
        {
            var memeOrders = await randomMemeService.GetAllMemeOrdersAsync(cancellationToken);
            var dtos = memeOrders.Select(MapToDto).ToList();
            result = Ok(OperationResult<List<MemeOrderDto>>.Ok("Получены все мемы", dtos));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting all meme orders");
            result = Ok(OperationResult<List<MemeOrderDto>>.Bad("Ошибка при получении мемов", []));
        }

        return result;
    }

    /// <summary>
    /// Get meme orders by type
    /// </summary>
    [HttpGet("orders/type/{typeId:int}")]
    [ProducesResponseType(typeof(OperationResult<List<MemeOrderDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult<List<MemeOrderDto>>>> GetMemeOrdersByType(
        int typeId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<List<MemeOrderDto>>> result = null!;

        try
        {
            var memeOrders = await randomMemeService.GetMemeOrdersByTypeAsync(
                typeId,
                cancellationToken
            );
            var dtos = memeOrders.Select(MapToDto).ToList();
            result = Ok(OperationResult<List<MemeOrderDto>>.Ok("Получены мемы по типу", dtos));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error occurred while getting meme orders for type {TypeId}",
                typeId
            );
            result = Ok(
                OperationResult<List<MemeOrderDto>>.Bad("Ошибка при получении мемов по типу", [])
            );
        }

        return result;
    }

    /// <summary>
    /// Get meme order by ID
    /// </summary>
    [HttpGet("orders/{id:guid}")]
    [ProducesResponseType(typeof(OperationResult<MemeOrderDto?>), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult<MemeOrderDto?>>> GetMemeOrderById(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<MemeOrderDto?>> result;
        try
        {
            var memeOrder = await randomMemeService.GetMemeOrderByIdAsync(id, cancellationToken);

            if (memeOrder != null)
            {
                result = Ok(OperationResult<MemeOrderDto?>.Ok("Мем найден", MapToDto(memeOrder)));
            }
            else
            {
                result = Ok(
                    OperationResult<MemeOrderDto?>.Bad($"MemeOrder with ID {id} not found", null)
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting meme order with ID {Id}", id);
            result = Ok(OperationResult<MemeOrderDto?>.Bad("Ошибка при получении мема", null));
        }

        return result;
    }

    /// <summary>
    /// Create new meme order
    /// </summary>
    [HttpPost("orders")]
    [ProducesResponseType(typeof(OperationResult<MemeOrderDto?>), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult<MemeOrderDto?>>> CreateMemeOrder(
        CreateMemeOrderDto createDto,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<MemeOrderDto?>> result;
        try
        {
            if (!ModelState.IsValid)
            {
                result = Ok(OperationResult<MemeOrderDto?>.Bad("Некорректные данные модели", null));
            }
            else
            {
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

                result = Ok(OperationResult<MemeOrderDto?>.Ok("Мем успешно создан", dto));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while creating meme order");
            result = Ok(OperationResult<MemeOrderDto?>.Bad("Ошибка при создании мема", null));
        }

        return result;
    }

    /// <summary>
    /// Update meme order
    /// </summary>
    [HttpPut("orders/{id:guid}")]
    [ProducesResponseType(typeof(OperationResult<MemeOrderDto?>), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult<MemeOrderDto?>>> UpdateMemeOrder(
        Guid id,
        UpdateMemeOrderDto updateDto,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<MemeOrderDto?>> result;
        try
        {
            if (!ModelState.IsValid)
            {
                result = Ok(OperationResult<MemeOrderDto?>.Bad("Некорректные данные модели", null));
            }
            else
            {
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
                result = Ok(
                    OperationResult<MemeOrderDto?>.Ok("Мем успешно обновлен", MapToDto(updated))
                );
            }
        }
        catch (InvalidOperationException ex)
        {
            result = Ok(OperationResult<MemeOrderDto?>.Bad(ex.Message, null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while updating meme order with ID {Id}", id);
            result = Ok(OperationResult<MemeOrderDto?>.Bad("Ошибка при обновлении мема", null));
        }

        return result;
    }

    /// <summary>
    /// Delete meme order
    /// </summary>
    [HttpDelete("orders/{id:guid}")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> DeleteMemeOrder(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            var deleteResult = await randomMemeService.DeleteMemeOrderAsync(id, cancellationToken);

            if (deleteResult)
            {
                result = Ok(OperationResult.Ok("Мем успешно удален"));
            }
            else
            {
                result = Ok(OperationResult.Bad($"MemeOrder with ID {id} not found"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while deleting meme order with ID {Id}", id);
            result = Ok(OperationResult.Bad("Ошибка при удалении мема"));
        }

        return result;
    }

    #endregion

    #region Additional Endpoints

    /// <summary>
    /// Get random meme
    /// </summary>
    [HttpGet("random")]
    [ProducesResponseType(typeof(OperationResult<MemeOrderDto?>), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult<MemeOrderDto?>>> GetRandomMeme(
        [FromQuery] int? typeId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<MemeOrderDto?>> result;
        try
        {
            var randomMeme = await randomMemeService.GetRandomMemeAsync(typeId, cancellationToken);

            if (randomMeme != null)
            {
                result = Ok(
                    OperationResult<MemeOrderDto?>.Ok("Случайный мем получен", MapToDto(randomMeme))
                );
            }
            else
            {
                result = Ok(OperationResult<MemeOrderDto?>.Bad("No memes found", null));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting random meme");
            result = Ok(
                OperationResult<MemeOrderDto?>.Bad("Ошибка при получении случайного мема", null)
            );
        }

        return result;
    }

    /// <summary>
    /// Get meme count
    /// </summary>
    [HttpGet("count")]
    [ProducesResponseType(typeof(OperationResult<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult<int>>> GetMemeCount(
        [FromQuery] int? typeId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<int>> result;
        try
        {
            var count = await randomMemeService.GetMemeOrderCountAsync(typeId, cancellationToken);
            result = Ok(OperationResult<int>.Ok("Получено количество мемов", count));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting meme count");
            result = Ok(OperationResult<int>.Bad("Ошибка при подсчете мемов", 0));
        }

        return result;
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
        ActionResult result;
        try
        {
            var memeOrder = await randomMemeService.GetMemeOrderByIdAsync(id, cancellationToken);
            if (memeOrder == null)
            {
                result = NotFound($"MemeOrder with ID {id} not found");
            }
            else if (!memeOrder.MemeTypeId.HasValue)
            {
                result = NotFound($"MemeOrder with ID {id} has no associated MemeType");
            }
            else
            {
                var memeType = await randomMemeService.GetMemeTypeByIdAsync(
                    memeOrder.MemeTypeId.Value,
                    cancellationToken
                );
                if (memeType == null)
                {
                    result = NotFound($"MemeType with ID {memeOrder.MemeTypeId} not found");
                }
                else
                {
                    var fullFilePath = Path.Combine(memeType.FolderPath, memeOrder.FilePath);
                    if (!System.IO.File.Exists(fullFilePath))
                    {
                        result = NotFound($"File not found at path: {fullFilePath}");
                    }
                    else
                    {
                        var fileBytes = await System.IO.File.ReadAllBytesAsync(
                            fullFilePath,
                            cancellationToken
                        );
                        var fileName = Path.GetFileName(memeOrder.FilePath);
                        var contentType = GetContentType(fileName);

                        result = File(fileBytes, contentType, fileName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting meme file with ID {Id}", id);
            result = StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }

        return result;
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
        ActionResult result;
        try
        {
            var randomMeme = await randomMemeService.GetRandomMemeAsync(typeId, cancellationToken);
            if (randomMeme == null)
            {
                result = NotFound("No memes found");
            }
            else if (!randomMeme.MemeTypeId.HasValue)
            {
                result = NotFound("Random meme has no associated MemeType");
            }
            else
            {
                var memeType = await randomMemeService.GetMemeTypeByIdAsync(
                    randomMeme.MemeTypeId.Value,
                    cancellationToken
                );
                if (memeType == null)
                {
                    result = NotFound($"MemeType with ID {randomMeme.MemeTypeId} not found");
                }
                else
                {
                    var fullFilePath = Path.Combine(memeType.FolderPath, randomMeme.FilePath);
                    if (!System.IO.File.Exists(fullFilePath))
                    {
                        result = NotFound($"File not found at path: {fullFilePath}");
                    }
                    else
                    {
                        var fileBytes = await System.IO.File.ReadAllBytesAsync(
                            fullFilePath,
                            cancellationToken
                        );
                        var fileName = Path.GetFileName(randomMeme.FilePath);
                        var contentType = GetContentType(fileName);

                        result = File(fileBytes, contentType, fileName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting random meme file");
            result = StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }

        return result;
    }

    /// <summary>
    /// Reorder meme orders for a specific type
    /// </summary>
    [HttpPost("orders/reorder/{typeId:int}")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> ReorderMemeOrders(
        int typeId,
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult> result;
        try
        {
            await randomMemeService.ReorderMemeOrdersAsync(typeId, cancellationToken);
            result = Ok(
                OperationResult.Ok($"Successfully reordered meme orders for type {typeId}")
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error occurred while reordering meme orders for type {TypeId}",
                typeId
            );
            result = Ok(OperationResult.Bad("Ошибка при пересортировке мемов"));
        }

        return result;
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
