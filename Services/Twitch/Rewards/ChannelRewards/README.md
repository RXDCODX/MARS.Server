# Настройки временных наград (TwitchRewards)

Этот файл описывает конфигурацию включения/отключения временных наград через `appsettings`.

Путь в конфиге: `AppSettings:TwitchRewards:EnabledByCost`.

Формат:

- `EnabledByCost` — объект (словарь), где ключ — цена награды (целое число, представленное строкой в JSON), значение — `true|false`.

Пример (JSON):

```json
"AppSettings": {
  "TwitchRewards": {
    "EnabledByCost": {
      "38": true,
      "170": false
    }
  }
}
```

Семантика и порядок принятия решения:

- Для каждой временной награды сначала вызывается её кастомная логика `IsRewardEnabled()`.
- После этого система проверяет, есть ли в `EnabledByCost` запись для этой `Cost`.
  - Если запись есть — итоговый результат = `IsRewardEnabled() && EnabledByCost[Cost]`.
  - Если записи нет — итоговый результат = `IsRewardEnabled()`.

Примечания:

- Конфигурация читается через `IOptionsMonitor<TwitchRewardsOptions>`, поэтому изменения в `appsettings` (или в источнике конфигурации) применяются динамически без перезапуска приложения.
- Ключи в JSON обязаны быть строками (например, "38"), но в коде они соответствуют целочисленной цене награды.

Где смотреть/изменять:

- Пример в `appsettings.Development.json` и `appsettings.json` в корне репозитория.
- Код, реализующий поведение: `ChannelRewardsService.GetEnabledOverrideForCost(int cost)` и `TemporaryReward`.
# Channel Rewards Management and Refunds

## Создание/Удаление наград канала

Используйте `ChannelRewardsService` для управления наградами канала через `ITwitchAPI`.

Пример регистрации сервиса в DI:

```csharp
// Program.cs / StartupEstensions.cs
services.AddSingleton<ChannelRewardsService>();
services.AddHostedService(sp => sp.GetRequiredService<ChannelRewardsService>());
```

Пример создания награды:

```csharp
var request = new CreateCustomRewardsRequest
{
    Title = "Моя награда",
    Cost = 123,
    IsEnabled = true,
    Prompt = "Введите текст (по желанию)",
    BackgroundColor = "#9146FF",
    IsUserInputRequired = false
};

var rewardId = await channelRewardsService.CreateRewardAsync(request);
```

Пример удаления награды:

```csharp
var ok = await channelRewardsService.DeleteRewardAsync(rewardId);
```

## Возврат использованных баллов через EventSub

Ниже описан подход, как через `EventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd` выполнить возврат баллов за награду (смотрите также рабочий пример в `TwitchRefundService`).

1. Подпишитесь на событие при старте приложения:

```csharp
lifetime.ApplicationStarted.Register(() =>
{
    wsClient.ChannelPointsCustomRewardRedemptionAdd += OnRedemption;
});
```

2. В обработчике проверьте условия и верните баллы:

```csharp
private async Task OnRedemption(object? sender, ChannelPointsCustomRewardRedemptionArgs args)
{
    var ev = args.Payload.Event;

    // Пример условия: возврат, если пользователь ввёл ключевое слово
    if (ev.BroadcasterUserLogin.Equals(TwitchExstension.Channel, StringComparison.OrdinalIgnoreCase)
        && ev.Reward.Cost == 160
        && (ev.UserInput?.Contains("asp", StringComparison.OrdinalIgnoreCase) ?? false))
    {
        await api.Helix.ChannelPoints.UpdateRedemptionStatusAsync(
            TwitchExstension.ChannelId,
            ev.Reward.Id, // RewardId
            new[] { args.Notification.Metadata.MessageId },
            new UpdateCustomRewardRedemptionStatusRequest
            {
                Status = CustomRewardRedemptionStatus.CANCELED
            },
            tokenService.tokenService.Token!.AccessToken
        );

        await client.SendMessageToMainTwitchAsync(
            $"@{ev.UserName}, твои {ev.Reward.Cost} баллов были возвращены!",
            logger
        );
    }
}
```

Ключевые моменты:

- Используйте `args.Notification.Metadata.MessageId` для идентификации выкупа.
- Перед обновлением статуса убедитесь, что токен валиден (`TokenService`).
- Статус для возврата — `CANCELED`.
