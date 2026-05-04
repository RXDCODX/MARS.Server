# Miku Monday Reward 🎤

## Описание

Временная награда "Miku Monday" - специальная награда канала, доступная **только по понедельникам**. При активации пользователь получает **случайный трек Hatsune Miku** из коллекции. Каждый пользователь может активировать награду **только один раз в понедельник**, и каждый трек выпадает только один раз за понедельник.

## Особенности

- ✅ **Автоматическое управление**: Награда создается и удаляется автоматически в зависимости от дня недели
- 🎨 **Фирменный цвет**: Светло-салатовый/бирюзовый цвет Hatsune Miku (#39C5BB)
- 💰 **Стоимость**: 39 баллов (отсылка к числу Мику - 39, или 3/9 - 9 марта)
- ⏰ **Проверка доступности**: Каждые 5 минут система проверяет день недели
- 🎵 **27 уникальных треков** из файла `miku.json`
- 🔒 **Один раз в понедельник**: Каждый пользователь может активировать награду только один раз
- 🎲 **Уникальные треки**: Треки не повторяются в течение одного понедельника
- 🔄 **Еженедельный сброс**: Список доступных треков сбрасывается каждый новый понедельник

## База данных

### Таблица `MikuTracks`

Хранит информацию о треках Hatsune Miku:
- `Id` - первичный ключ
- `Number` - номер трека (уникальный)
- `Artist` - исполнитель/продюсер
- `Title` - название трека
- `Url` - YouTube URL
- `CreatedAt` - дата создания записи

### Таблица `MikuMondayActivations`

Хранит историю активаций награды:
- `Id` - первичный ключ
- `TwitchUserId` - ID пользователя Twitch
- `DisplayName` - отображаемое имя пользователя
- `MikuTrackId` - ID выпавшего трека (FK на MikuTracks)
- `ActivatedAt` - время активации
- `WeekOfYear` - номер недели в году
- `Year` - год

**Уникальный индекс**: `(TwitchUserId, Year, WeekOfYear)` - гарантирует, что один пользователь может активировать награду только один раз в неделю.

## Сервисы

### `MikuMondayTracksService`

Управляет треками и активациями:

#### Методы:

1. **`InitializeTracksAsync()`**
   - Загружает треки из `miku.json` в базу данных при первом запуске
   - Выполняется автоматически при старте приложения

2. **`GetRandomTrackForUserAsync(twitchUserId, displayName)`**
   - Проверяет, активировал ли пользователь награду в этот понедельник
   - Выбирает случайный трек из доступных
   - Сохраняет активацию в БД
   - Возвращает: `(MikuTrack?, List<MikuTrack>, string?)`

3. **`GetAvailableTracksAsync()`**
   - Возвращает список треков, которые еще не выпали в этот понедельник

### `TwitchMikuMondayRewardService`

Основной сервис награды:

- Наследуется от `TemporaryReward`
- Управляет жизненным циклом награды (создание/удаление)
- Обрабатывает активации награды
- Отправляет данные на фронтенд через SignalR

## Логика работы

### При запуске приложения:
1. `MikuMondayTracksService` загружает треки из `miku.json` в БД (если их еще нет)
2. `TemporaryReward` запускает таймер для проверки дня недели
3. Если понедельник - награда создается в Twitch

### При активации награды:
1. Проверяется, активировал ли пользователь уже награду в этот понедельник
   - **Да** → Отправляется сообщение в чат: "Вы уже активировали Miku Monday в этот понедельник! 🎤"
   
2. Получается список треков, которые еще не выпали в этот понедельник
   - **Список пуст** → "Все треки уже разобраны в этот понедельник! 🎵 Попробуйте в следующий понедельник!"
   
3. Выбирается случайный трек из доступных
4. Сохраняется запись об активации в БД
5. Формируется DTO с данными:
   - Выбранный трек
   - Список оставшихся доступных треков
6. Отправляется SignalR событие `MikuMonday(mikuMondayData)`
7. Отправляется сообщение в чат: "@User получил трек #N: Artist - Title 🎤 Осталось треков: X"

### Каждый новый понедельник:
- Список доступных треков полностью сбрасывается
- Все 27 треков снова становятся доступными

## SignalR интерфейс

### Метод хаба

```csharp
[SignalRMethod]
Task MikuMonday(MikuMondayDto mikuMondayData);
```

### DTO структура

```csharp
public class MikuMondayDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; }
    public MikuTrackDto SelectedTrack { get; set; }
    public List<MikuTrackDto> AvailableTracks { get; set; }
    public bool SkipAvailableTracksUpdate { get; set; }
}

public class MikuTrackDto
{
    public int Number { get; set; }
    public string Artist { get; set; }
    public string Title { get; set; }
    public string Url { get; set; }
}
```

## Примеры треков

Примеры треков из коллекции (всего 27):

1. **wowaka** - Rolling Girl
2. **Kairiki Bear** - Bug
3. **Kanaria** - Yoidoreshirazu
4. **Kikuo** - Ai Wo Sagashite
5. **Utsu-P** - Mikusabbath
6. **PinocchioP** - God-Ish
7. **hachi** - MATRYOSHKA
8. **ryo** - World Is Mine
9. **DECO*27** - Hibana
10. **NayutalieN** - Alien Alien

И многие другие!

## Регистрация в DI

```csharp
// В StartupEstensions.cs
services.AddSingleton<MikuMondayTracksService>();
services.AddSingleton<TwitchMikuMondayRewardService>();
services.AddHostedService(sp => sp.GetRequiredService<TwitchMikuMondayRewardService>());
```

## Миграция базы данных

Миграция `MikuMondayTables` создает таблицы:
- `MikuTracks`
- `MikuMondayActivations`

Применяется автоматически при запуске приложения.

## Фронтенд интеграция

Для обработки события на фронтенде:

```typescript
connection.on('MikuMonday', (data: MikuMondayDto) => {
  console.log(`${data.DisplayName} получил трек:`);
  console.log(`#${data.SelectedTrack.Number}: ${data.SelectedTrack.Artist} - ${data.SelectedTrack.Title}`);
  console.log(`URL: ${data.SelectedTrack.Url}`);
  console.log(`Осталось треков: ${data.AvailableTracks.length}`);
  
  // Показать специальную анимацию
  showMikuMondayEffect(data);
  
  // Можно показать список оставшихся треков
  displayAvailableTracks(data.AvailableTracks);
});
```

## Логирование

Сервис логирует следующие события:
- Загрузка треков из JSON в БД
- Запуск/остановка сервиса
- Создание/удаление награды в Twitch
- Активация награды пользователем
- Получение трека пользователем
- Ошибки (повторная активация, отсутствие свободных треков)

## Примеры логов

```
Загружено 27 треков Miku в базу данных
Запуск временной награды: 🎤 Miku Monday (Cost: 39)
Создание временной награды: 🎤 Miku Monday
Miku Monday награда активирована пользователем rxdcodx за 39 баллов
Пользователь rxdcodx получил трек #8: Mikito P - 39 Music!
Miku Monday эффект активирован для пользователя rxdcodx, трек: #8 Mikito P - 39 Music!
```

## Файл miku.json

Структура JSON файла:

```json
[
  {
    "number": 1,
    "artist": "wowaka",
    "title": "Rolling Girl",
    "url": "https://youtu.be/vnw8zURAxkU?si=5qdh1srGJzLdLLev"
  },
  ...
]
```

Расположение: `{ContentRootPath}/miku.json`

## Особенности реализации

1. **Потокобезопасность**: Инициализация треков использует блокировку для предотвращения дублирования
2. **Номер недели**: Используется `Calendar.GetWeekOfYear()` с правилом `FirstDay` и началом недели в понедельник
3. **Уникальность активаций**: Уникальный индекс на `(TwitchUserId, Year, WeekOfYear)` в БД
4. **Уникальность треков в понедельник**: Фильтрация треков по записям активаций текущей недели
5. **Сброс каждый понедельник**: Автоматический благодаря проверке `WeekOfYear` и `Year`

## Расширение функционала

### Добавление новых треков

1. Добавьте трек в `miku.json`
2. Удалите запись о загруженных треках или очистите таблицу `MikuTracks`
3. Перезапустите приложение

### Изменение ограничений

Чтобы разрешить активацию несколько раз в понедельник:
- Удалите уникальный индекс в миграции
- Измените логику проверки в `GetRandomTrackForUserAsync()`

### Другие временные рамки

Для создания награды на другой день недели, измените `IsRewardEnabled`:

```csharp
public override Func<DateTime, bool> IsRewardEnabled { get; set; } =
    (DateTime date) =>
    {
        var result = date.DayOfWeek == DayOfWeek.Friday; // Fumo Friday
        return result;
    };
```
