namespace MARS.Server.Services.DatabaseBackup.Entitys;

/// <summary>
/// Модель для создания резервной копии
/// </summary>
public class CreateBackupRequest
{
    /// <summary>
    /// Имя базы данных для резервного копирования
    /// </summary>
    [Required(ErrorMessage = "Имя базы данных обязательно")]
    [RegularExpression(
        "^(dev|prod)$",
        ErrorMessage = "Поддерживаются только базы данных 'dev' и 'prod'"
    )]
    public string DatabaseName { get; set; } = string.Empty;
}
