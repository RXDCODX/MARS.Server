using System;
using System.IO;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.Telegram.WTelegram.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Telegram.WTelegram;

/// <summary>
/// Stream-реализация для хранения сессии WTelegram в БД.
/// При пустой БД фоллбэчится на файл по указанному пути.
/// После первого успешного Write в БД — исходный файл удаляется.
/// </summary>
public sealed class WTelegramDbSessionStore : Stream
{
    private readonly ILogger? _logger;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly string _sessionName;
    private readonly byte[] _data;
    private readonly int _dataLen;
    private readonly Lock _writeLock = new();

    public WTelegramDbSessionStore(
        IDbContextFactory<AppDbContext> dbFactory,
        string sessionName,
        ILogger? logger = null
    )
    {
        _dbFactory = dbFactory;
        _sessionName = sessionName;
        _logger = logger;

        byte[]? data = null;

        try
        {
            data = LoadFromDb();
            if (data is not null)
            {
                _logger?.LogInformation(
                    "Сессия WTelegram загружена из БД (name={SessionName}, size={Size})",
                    _sessionName,
                    data.Length
                );
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Не удалось загрузить сессию WTelegram из БД");
        }

        _data = data ?? [];
        _dataLen = _data.Length;
    }

    private byte[]? LoadFromDb()
    {
        using var context = _dbFactory.CreateDbContext();
        var session = context.WTelegramSessions.Find(_sessionName);
        return session?.Data is { Length: > 0 } data ? data : null;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        Array.Copy(_data, 0, buffer, offset, count);
        return count;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var dataToSave = count == buffer.Length ? buffer : buffer[offset..(offset + count)];

        lock (_writeLock)
        {
            using var context = _dbFactory.CreateDbContext();
            var existing = context.WTelegramSessions.Find(_sessionName);

            if (existing is not null)
            {
                existing.Data = dataToSave;
            }
            else
            {
                context.WTelegramSessions.Add(
                    new WTelegramSession { Name = _sessionName, Data = dataToSave }
                );
            }

            context.SaveChanges();
        }

        _logger?.LogDebug(
            "Сессия WTelegram сохранена в БД (name={SessionName}, size={Size})",
            _sessionName,
            dataToSave.Length
        );
    }

    public override long Length => _dataLen;
    public override long Position
    {
        get => 0;
        set { }
    }

    public override bool CanSeek => false;
    public override bool CanRead => true;
    public override bool CanWrite => true;

    public override long Seek(long offset, SeekOrigin origin) => 0;

    public override void SetLength(long value) { }

    public override void Flush() { }
}
