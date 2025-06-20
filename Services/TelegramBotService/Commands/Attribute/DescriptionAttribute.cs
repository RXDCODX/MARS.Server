using NJsonSchema.Annotations;

namespace MARS.Server.Services.TelegramBotService.Commands.Attribute;

public class DescriptionAttribute([NotNull] string description) : System.Attribute
{
    public string Description { get; init; } = description;
}
