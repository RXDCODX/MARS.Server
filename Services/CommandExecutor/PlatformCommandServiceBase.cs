using System.Text;
using MARS.Server.Services.CommandExecutor.Entitys;

namespace MARS.Server.Services.CommandExecutor;

/// <summary>
/// Базовый класс для платформенных сервисов команд
/// </summary>
public abstract class PlatformCommandServiceBase<T>
    where T : notnull
{
    public abstract Platform Platform { get; }

    /// <summary>
    /// Максимальная длина ответа по умолчанию
    /// </summary>
    protected virtual int DefaultMaxResponseLength => 1000;

    /// <summary>
    /// Префиксы команд для данной платформы (например: '/', '!')
    /// </summary>
    public virtual char[] CommandPrefixes => ['/'];

    public abstract IEnumerable<string> UserCommands { get; }
    public abstract IEnumerable<string> AdminCommands { get; }

    public abstract Func<T, bool> IsAdmin { get; }

    /// <summary>
    /// Проверить, является ли пользователь администратором
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <returns>True если пользователь администратор</returns>
    public virtual bool IsUserAdmin(T userId)
    {
        return IsAdmin.Invoke(userId);
    }

    /// <summary>
    /// Получить префиксы команд для данной платформы
    /// </summary>
    /// <returns>Массив символов-префиксов</returns>
    public virtual char[] GetCommandPrefixes()
    {
        return CommandPrefixes;
    }

    /// <summary>
    /// Убрать префикс команды из строки
    /// </summary>
    /// <param name="commandText">Текст с префиксом</param>
    /// <returns>Текст без префикса</returns>
    public virtual string TrimCommandPrefix(string commandText)
    {
        var result = commandText;

        if (!string.IsNullOrWhiteSpace(commandText))
        {
            result = commandText.TrimStart(CommandPrefixes);
        }

        return result;
    }

    /// <summary>
    /// Проверить, начинается ли строка с префикса команды
    /// </summary>
    /// <param name="text">Текст для проверки</param>
    /// <returns>True если текст начинается с префикса команды</returns>
    public virtual bool StartsWithCommandPrefix(string text)
    {
        var result = false;

        if (!string.IsNullOrWhiteSpace(text))
        {
            result = CommandPrefixes.Any(prefix => text.StartsWith(prefix));
        }

        return result;
    }

    /// <summary>
    /// Получить список команд для пользователя
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="includeAdminCommands">Включить админские команды</param>
    /// <returns>Список команд</returns>
    public virtual string GetCommandsList(T userId, bool includeAdminCommands = false)
    {
        var isAdmin = IsUserAdmin(userId);

        if (includeAdminCommands && !isAdmin)
        {
            return "У вас нет прав для просмотра админских команд.";
        }

        var commands = new List<string>();

        // Добавляем пользовательские команды
        commands.AddRange(UserCommands);

        // Добавляем админские команды если запрошено и пользователь админ
        if (includeAdminCommands && isAdmin)
        {
            commands.AddRange(AdminCommands);
        }

        if (commands.Count == 0)
        {
            return "Нет доступных команд для вашей роли.";
        }

        var result =
            includeAdminCommands && isAdmin
                ? "Доступные команды (включая админские):\n"
                : "Доступные команды:\n";

        result += string.Join("\n", commands);

        return result;
    }

    /// <summary>
    /// Получить список команд для пользователя (перегрузка с явным указанием команд)
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="userCommands">Пользовательские команды</param>
    /// <param name="adminCommands">Админские команды</param>
    /// <param name="includeAdminCommands">Включить админские команды</param>
    /// <returns>Список команд</returns>
    public virtual string GetCommandsList(
        T userId,
        IEnumerable<string> userCommands,
        IEnumerable<string> adminCommands,
        bool includeAdminCommands = false
    )
    {
        var isAdmin = IsUserAdmin(userId);

        if (includeAdminCommands && !isAdmin)
        {
            return "У вас нет прав для просмотра админских команд.";
        }

        var commands = new List<string>();

        // Добавляем пользовательские команды
        commands.AddRange(userCommands);

        // Добавляем админские команды если запрошено и пользователь админ
        if (includeAdminCommands && isAdmin)
        {
            commands.AddRange(adminCommands);
        }

        if (commands.Count == 0)
        {
            return "Нет доступных команд для вашей роли.";
        }

        StringBuilder result = new(
            includeAdminCommands && isAdmin
                ? "Доступные команды (включая админские): "
                    + Environment.NewLine
                    + Environment.NewLine
                : "Доступные команды: " + Environment.NewLine + Environment.NewLine
        );

        result.AppendJoin(Environment.NewLine + Environment.NewLine, commands.Order());

        return result.ToString();
    }

    /// <summary>
    /// Валидировать ответ для платформы
    /// </summary>
    /// <param name="response">Ответ команды</param>
    /// <returns>Валидный ответ</returns>
    public virtual string ValidateResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return response;
        }

        var maxLength = GetMaxResponseLength();

        if (response.Length <= maxLength)
        {
            return response;
        }

        // Обрезаем ответ и добавляем индикатор обрезки
        var truncated = response.Substring(0, maxLength - 3);
        return truncated + "...";
    }

    /// <summary>
    /// Получить максимальную длину ответа для платформы
    /// </summary>
    /// <returns>Максимальная длина в символах</returns>
    public virtual int GetMaxResponseLength()
    {
        return DefaultMaxResponseLength;
    }
}
