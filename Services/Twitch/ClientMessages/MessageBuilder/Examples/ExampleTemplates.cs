using MARS.Server.Services.Twitch.ClientMessages.MessageBuilder.DTOs;

namespace MARS.Server.Services.Twitch.ClientMessages.MessageBuilder.Examples;

/// <summary>
/// Примеры шаблонов сообщений для демонстрации возможностей сервиса
/// </summary>
public static class ExampleTemplates
{
    /// <summary>
    /// Получить примеры шаблонов для инициализации базы данных
    /// </summary>
    public static List<CreateTwitchMessageTemplateDto> GetExampleTemplates()
    {
        return
        [
            new()
            {
                Name = "Приветствие",
                MessageTemplate = "Привет, {user}! Рад тебя видеть в чате! 👋",
                Description = "Автоматическое приветствие новых пользователей",
                TriggerWord = "привет",
                Priority = 1,
                RandomChance = 100,
                CooldownSeconds = 60,
            },
            // Ответ на команду помощи

            new()
            {
                Name = "Команда помощи",
                MessageTemplate =
                    "{user}, вот список доступных команд: !help, !info, !stats, !commands",
                Description = "Помощь по командам",
                TriggerWord = "помощь",
                AuthorColor = "#00FF00",
                AuthorName = "Бот-помощник",
                Priority = 10,
                RandomChance = 100,
                CooldownSeconds = 30,
            },
            // Случайная шутка

            new()
            {
                Name = "Случайная шутка",
                MessageTemplate =
                    "{user}, вот шутка для тебя: Почему программисты путают Рождество и Хэллоуин? Потому что Oct 31 == Dec 25! 😄",
                Description = "Случайные шутки для программистов",
                TriggerWord = "шутка",
                AuthorColor = "#FF00FF",
                AuthorName = "Шутник",
                Priority = 5,
                RandomChance = 30,
                CooldownSeconds = 300,
            },
            // Мотивация

            new()
            {
                Name = "Мотивация",
                MessageTemplate = "{user}, ты молодец! Продолжай в том же духе! 💪",
                Description = "Мотивационные сообщения",
                TriggerWord = "молодец",
                AuthorColor = "#FFFF00",
                AuthorName = "Мотиватор",
                Priority = 3,
                RandomChance = 50,
                CooldownSeconds = 180,
            },
            // Информация о стриме

            new()
            {
                Name = "Информация о стриме",
                MessageTemplate = "{user}, мы играем в Tekken 8! Присоединяйся к игре! 🎮",
                Description = "Информация о текущем стриме",
                TriggerWord = "игра",
                AuthorColor = "#00FFFF",
                AuthorName = "Инфо-бот",
                Priority = 8,
                RandomChance = 100,
                CooldownSeconds = 120,
            },
            // Случайный факт

            new()
            {
                Name = "Случайный факт",
                MessageTemplate = "{user}, знаешь ли ты, что первый компьютер весил 27 тонн? 🤯",
                Description = "Интересные факты о технологиях",
                TriggerWord = "факт",
                AuthorColor = "#FF8000",
                AuthorName = "Факт-бот",
                Priority = 4,
                RandomChance = 25,
                CooldownSeconds = 600,
            },
            // Приветствие новых подписчиков

            new()
            {
                Name = "Приветствие подписчиков",
                MessageTemplate = "Добро пожаловать, {user}! Спасибо за подписку! 🎉",
                Description = "Приветствие новых подписчиков",
                TriggerWord = "подписка",
                AuthorColor = "#FFD700",
                AuthorName = "Система подписок",
                Priority = 15,
                RandomChance = 100,
                CooldownSeconds = 0,
            },
            // Ответ на вопрос о времени

            new()
            {
                Name = "Время стрима",
                MessageTemplate = "{user}, мы стримим уже {streamTime}! Время летит незаметно! ⏰",
                Description = "Информация о времени стрима",
                TriggerWord = "время",
                AuthorColor = "#C0C0C0",
                AuthorName = "Часовой",
                Priority = 6,
                RandomChance = 100,
                CooldownSeconds = 240,
            },
        ];
    }

    /// <summary>
    /// Получить примеры триггер-слов для тестирования
    /// </summary>
    public static List<string> GetExampleTriggerWords()
    {
        return ["привет", "помощь", "шутка", "молодец", "игра", "факт", "подписка", "время"];
    }

    /// <summary>
    /// Получить примеры цветов для авторов
    /// </summary>
    public static List<string> GetExampleColors()
    {
        return
        [
            "#FF0000", // Красный
            "#00FF00", // Зеленый
            "#0000FF", // Синий
            "#FFFF00", // Желтый
            "#FF00FF", // Пурпурный
            "#00FFFF", // Голубой
            "#FF8000", // Оранжевый
            "#FFD700", // Золотой
            "#C0C0C0", // Серебряный
            "#800080",
        ];
    }
}
