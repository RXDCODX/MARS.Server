using System;

namespace MARS.Server.Services.CommandExecutor.Entitys;

/// <summary>
/// Флаги для управления видимостью команд в различных контекстах
/// </summary>
[Flags]
public enum CommandVisibility
{
    /// <summary>
    /// Команда не отображается нигде
    /// </summary>
    None = 0,

    /// <summary>
    /// Команда отображается в полном списке команд (!commands)
    /// </summary>
    FullList = 1,

    /// <summary>
    /// Команда отображается в кратком списке команд (!c)
    /// </summary>
    ShortList = 2,

    /// <summary>
    /// Команда отображается в inline-выдаче
    /// </summary>
    Inline = 4,

    /// <summary>
    /// Команда отображается везде (полный и краткий список)
    /// </summary>
    All = FullList | ShortList | Inline,
}
