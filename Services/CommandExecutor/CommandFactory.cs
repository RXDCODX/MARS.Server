using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor;

/// <summary>
/// Фабрика для создания команд с поддержкой DI
/// </summary>
public class CommandFactory(IServiceProvider serviceProvider)
{
    /// <summary>
    /// Создает все команды, наследующие от BaseCommand, автоматически внедряя зависимости
    /// </summary>
    /// <returns>Словарь команд</returns>
    public Dictionary<string, BaseCommand> CreateAllCommands()
    {
        var commands = new Dictionary<string, BaseCommand>(StringComparer.OrdinalIgnoreCase);

        // Получаем все типы команд из сборки
        var commandTypes = GetCommandTypes();

        foreach (var commandType in commandTypes)
        {
            try
            {
                var command = CreateCommand(commandType);
                if (command != null)
                {
                    commands[command.CommandName] = command;
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку создания команды, но продолжаем работу
                Console.WriteLine($"Ошибка создания команды {commandType.Name}: {ex.Message}");
            }
        }

        return commands;
    }

    /// <summary>
    /// Создает конкретную команду с внедрением зависимостей
    /// </summary>
    /// <param name="commandType">Тип команды</param>
    /// <returns>Экземпляр команды</returns>
    public BaseCommand? CreateCommand(Type commandType)
    {
        if (!typeof(BaseCommand).IsAssignableFrom(commandType) || commandType.IsAbstract)
        {
            return null;
        }

        // Получаем конструктор с наибольшим количеством параметров
        var constructors = commandType.GetConstructors();
        if (constructors.Length == 0)
        {
            // Если нет конструкторов, используем конструктор по умолчанию
            return (BaseCommand)Activator.CreateInstance(commandType)!;
        }

        var bestConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();

        var parameters = bestConstructor.GetParameters();
        var resolvedParameters = new object[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            try
            {
                resolvedParameters[i] = serviceProvider.GetRequiredService(parameter.ParameterType);
            }
            catch (InvalidOperationException)
            {
                // Если сервис не зарегистрирован, пытаемся создать экземпляр по умолчанию
                if (parameter.HasDefaultValue)
                {
                    resolvedParameters[i] = parameter.DefaultValue!;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Не удалось разрешить зависимость {parameter.ParameterType.Name} для команды {commandType.Name}"
                    );
                }
            }
        }

        return (BaseCommand)bestConstructor.Invoke(resolvedParameters);
    }

    /// <summary>
    /// Получает все типы команд из сборки
    /// </summary>
    /// <returns>Список типов команд</returns>
    private static IEnumerable<Type> GetCommandTypes()
    {
        var assembly = typeof(BaseCommand).Assembly;
        return assembly
            .GetTypes()
            .Where(t =>
                typeof(BaseCommand).IsAssignableFrom(t) && !t.IsAbstract && t != typeof(BaseCommand)
            );
    }
}
