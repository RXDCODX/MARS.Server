using System.Collections.Concurrent;
using MARS.Server.Services.MemoryStorageService.Entitys;

namespace MARS.Server.Services.MemoryStorageService;

/// <summary>
/// Provides in-memory storage functionality for the application.
/// </summary>
public static class MemoryStorage
{
    // Приватное поле для хранения файлов в памяти (имя файла -> содержимое)
    private static readonly ConcurrentDictionary<string, MemoryFile> FileStorage;

    /// <summary>
    /// Конструктор класса MemoryStorage
    /// </summary>
    static MemoryStorage()
    {
        FileStorage = [];
    }

    /// <summary>
    /// Добавляет файл в хранилище
    /// </summary>
    /// <param name="fileName">Имя файла</param>
    /// <param name="fileContent">Содержимое файла в виде массива байт</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если имя файла или содержимое null</exception>
    /// <exception cref="ArgumentException">Выбрасывается, если файл с таким именем уже существует</exception>
    /// <returns>Relative url for file download</returns>
    public static async Task<string> AddFileAsync(string fileName, byte[] fileContent)
    {
        await Task.Factory.StartNew(() =>
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName), "Имя файла не может быть пустым");
            }

            if (FileExists(fileName))
            {
                return IncrementUseCounterAsync(fileName);
            }

            var extension = Path.GetExtension(fileName);
            var mediaType = extension.GetFileMediaType();

            var content = new MemoryFile
            {
                Exstension = extension,
                MediaType = mediaType,
                FileContent = fileContent,
                FileName = fileName,
                UseCount = 1,
            };

            FileStorage.TryAdd(fileName, content);

            return Task.CompletedTask;
        });

        return "/memory" + fileName;
    }

    private static async Task IncrementUseCounterAsync(string fileName)
    {
        var isFound = FileStorage.TryGetValue(fileName, out var description);

        if (!isFound || description is null)
        {
            throw new NullReferenceException();
        }

        ++description.UseCount;
        while (!FileStorage.TryUpdate(fileName, description, description))
        {
            await Task.Delay(500);
        }
    }

    /// <summary>
    /// Получает содержимое файла из хранилища
    /// </summary>
    /// <param name="fileName">Имя файла</param>
    /// <returns>Массив байт с содержимым файла</returns>
    /// <exception cref="ArgumentNullException">Выбрасывается, если имя файла null</exception>
    /// <exception cref="FileNotFoundException">Выбрасывается, если файл не найден</exception>
    public static Task<(
        MemoryStream description,
        string contentType
    )> GetFileStreamWithContentTypeAsync(string fileName)
    {
        return Task.Factory.StartNew(() =>
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName), "Имя файла не может быть пустым");
            }

            if (!FileStorage.TryGetValue(fileName, out var description))
            {
                throw new FileNotFoundException(
                    $"Файл с именем '{fileName}' не найден в хранилище"
                );
            }

            var stream = new MemoryStream(description.FileContent);

            return (stream, description.GetContentType());
        });
    }

    /// <summary>
    /// Проверяет существование файла в хранилище
    /// </summary>
    /// <param name="fileName">Имя файла</param>
    /// <returns>True, если файл существует, иначе False</returns>
    public static bool FileExists(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName) && FileStorage.ContainsKey(fileName);
    }

    /// <summary>
    /// Удаляет файл из хранилища
    /// </summary>
    /// <param name="fileName">Имя файла</param>
    /// <returns>True, если файл был удален, False если файл не существовал</returns>
    /// <exception cref="ArgumentNullException">Выбрасывается, если имя файла null</exception>
    public static async Task DeleteFileAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentNullException(nameof(fileName), "Имя файла не может быть пустым");
        }

        var description = FileStorage[fileName];

        --description.UseCount;
        if (description.UseCount != 0)
        {
            return;
        }

        while (true)
        {
            var isRemoved = FileStorage.TryRemove(fileName, out var removedDescription);
            if (isRemoved)
            {
                if (removedDescription != null)
                {
                    Array.Clear(removedDescription.FileContent);
                }
                break;
            }
            else
            {
                if (description.UseCount != 0)
                {
                    break;
                }
                else
                {
                    await Task.Delay(500);
                }
            }
        }
    }

    /// <summary>
    /// Возвращает список всех имен файлов в хранилище
    /// </summary>
    /// <returns>Массив имен файлов</returns>
    public static Task<string[]> GetAllFileNamesAsync()
    {
        return Task.Factory.StartNew(() =>
        {
            var fileNames = new string[FileStorage.Count];
            FileStorage.Keys.CopyTo(fileNames, 0);
            return fileNames;
        });
    }

    /// <summary>
    /// Очищает все содержимое хранилища
    /// </summary>
    public static Task ClearStorageAsync()
    {
        return Task.Factory.StartNew(() =>
        {
            FileStorage.Clear();
        });
    }

    public static void ClearStorage()
    {
        FileStorage.Clear();
    }

    /// <summary>
    /// Возвращает количество файлов в хранилище
    /// </summary>
    public static int FileCount => FileStorage.Count;
    public static ulong StorageSize =>
        Convert.ToUInt64(FileStorage.Values.Sum(e => e.FileContent.Length));
}
