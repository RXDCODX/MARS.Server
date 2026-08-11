using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class ExampleCommand : BaseCommand
{
    public override string CommandName => "example";
    public override string Description => "Пример команды с несколькими параметрами";
    public override bool IsAdminCommand => false;

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "name",
                Description = "Имя пользователя",
                Type = "string",
                Required = true,
            },
            new()
            {
                Name = "age",
                Description = "Возраст",
                Type = "int",
                Required = false,
                DefaultValue = "18",
            },
            new()
            {
                Name = "message",
                Description = "Сообщение",
                Type = "string",
                Required = false,
            },
        ];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platofrm = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var name = parameters["name"].ToString() ?? "Неизвестно";
        var age = Convert.ToInt32(parameters["age"]);
        var message = parameters.TryGetValue("message", out var msgObj)
            ? msgObj.ToString()
            : "Привет!";

        var result = $"""
            Привет, {name}!
            Твой возраст: {age}
            Сообщение: {message}

            Пример использования: /example Иван 25 Привет всем!
            """;

        return Task.FromResult(result);
    }
}
