# Entitys - Модели данных

## FollowerInfo

Расширенная модель для хранения информации о фоловерах канала с дополнительными возможностями.

### Основные свойства

- `UserId` - ID пользователя
- `UserName` - Имя пользователя  
- `UserLogin` - Логин пользователя
- `FollowedAt` - Дата подписки на канал
- `LastUpdated` - Дата последнего обновления информации

### Методы

#### Статические методы

- `FromChannelFollower(ChannelFollower)` - Создать FollowerInfo из ChannelFollower
- `ToChannelFollower()` - Преобразовать в ChannelFollower для совместимости

#### Методы экземпляра

- `UpdateFromChannelFollower(ChannelFollower)` - Обновить информацию
- `IsStale(TimeSpan)` - Проверить устарела ли информация
- `ToString()` - Строковое представление
- `Equals(object)` - Сравнение по UserId
- `GetHashCode()` - Хеш-код по UserId

### Преимущества

1. **Дополнительная информация** - время последнего обновления
2. **Проверка актуальности** - метод IsStale для проверки устаревания
3. **Совместимость** - легко преобразуется в ChannelFollower
4. **Безопасность** - потокобезопасные операции
5. **Производительность** - оптимизированные методы сравнения

### Примеры использования

```csharp
// Создание из API данных
var followerInfo = FollowerInfo.FromChannelFollower(apiFollower);

// Проверка актуальности (не старше 1 часа)
if (followerInfo.IsStale(TimeSpan.FromHours(1)))
{
    // Обновить информацию
    followerInfo.UpdateFromChannelFollower(newApiData);
}

// Преобразование для API
var channelFollower = followerInfo.ToChannelFollower();

// Сравнение фоловеров
var isSameFollower = follower1.Equals(follower2);
```

## ChannelUsersResult

Результат объединения всех типов пользователей канала.

### Свойства

- `Followers` - Список фоловеров (исключая модераторов и VIP)
- `ViPs` - Список VIP пользователей
- `Moderators` - Список модераторов

### Особенности

- Фоловеры автоматически исключаются из списка, если они являются модераторами или VIP
- Обеспечивает уникальность пользователей в каждой категории
- Удобно для отображения статистики канала
