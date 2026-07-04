# План миграции на TwitchEventValidationService

Список сервисов, в которых нужно внедрить `ITwitchEventValidationService`
для замены inline-валидаций на fluent-вызовы.

Легенда:
- `📝 OnMessageReceived` — подписка на `client.OnMessageReceived +=`
- `🎯 ChannelPointsCustomRewardRedemptionAdd` — подписка на `wsClient.ChannelPointsCustomRewardRedemptionAdd +=`
- `✅` — уже migrated

---

## OnMessageReceived (17 сервисов)

- [x] `MARS.Server.Services.Twitch` — `TwitchUserSyncService` ✅
- [x] `MARS.Server.Services.Twitch.PuntoSwitcher` — `PuntoSwitcherService` ✅
- [x] `MARS.Server.Services.Twitch.StreamManagement` — `TwitchTitleChangeCommand` ✅
- [x] `MARS.Server.Services.Twitch.HelloVideos` — `HelloVideoWorker` ✅
- [x] `MARS.Server.Services.CommandExecutor.Adapters` — `TwitchCommandService` ✅
- [x] `MARS.Server.Services.Twitch.ClientMessages.TwitchAutoHello` — `AutoHello` ✅
- [x] `MARS.Server.Services.Twitch.ClientMessages.AutoMessages` — `AutoMessagesHandler` ✅
- [x] `MARS.Server.Services.Twitch.Rewards` — `WaifuRollCooldownNotificationService` ✅
- [x] `MARS.Server.Services.Twitch.Rewards` — `TwitchMessagesHubAwaker` (2 handlers) ✅
- [x] `MARS.Server.Services.Twitch.Rewards` — `TwitchMediaAlerts` ✅
- [x] `MARS.Server.Services.Twitch.Rewards` — `MiniGamesManager` ✅
- [x] `MARS.Server.Services.Twitch.Rewards` — `HighlitedMessage` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._5_AddWife` — `AddNewWaifu` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._13_FumoFriday` — `FumoFriday_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._1580_MikuBeam` — `TwitchMikuBeamRewardService` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._1702_EmojisReward` — `Emojis_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Synthesizer` — `TtsHubBroadcaster` ✅

## ChannelPointsCustomRewardRedemptionAdd (25 сервисов)

- [x] `MARS.Server.Services.CinemaQueue.Services` — `TwitchCinemaQueueService` ✅
- [x] `MARS.Server.Services.Twitch.Rewards` — `WaifuRollCooldownNotificationService` ✅
- [x] `MARS.Server.Services.Twitch.Rewards` — `TwitchMediaAlerts` ✅
- [x] `MARS.Server.Services.Twitch.Rewards` — `MiniGamesManager` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._1_RandomReward` — `RandomReward_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._2_WaifuMarriage` — `MergeWaifu` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._4_FumoRoll` — `FumoFridayRoll_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._4_SearchWife` — `SearchWife_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._10_RandomSound` — `RandomSound_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._11_RandomMemReward` — `RandomMem_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._13_FumoFriday` — `FumoFriday_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._18_GaoAlert` — `GaoAlert_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._27_RandomArt` — `RandomArt` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._39_MikuMonday` — `TwitchMikuMondayRewardService` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._155_MichaelTime` — `MichaelTime_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._1580_MikuBeam` — `TwitchMikuBeamRewardService` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._160_LegBum` — `LegBumRefundService` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._337_PhonkEdit` — `PhonkEdit_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._353_TikTokEdit` — `TikTokEdit_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._1700_Confetti` — `Confetti_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._1701_Fireworks` — `Fireworks_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._2002_AdhdSuperpower` — `AdhdSuperpower_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._6666_CloseGame` — `CloseGame_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._8005_Credits` — `Credits_TwitchReward` ✅
- [x] `MARS.Server.Services.Twitch.Rewards._99999_AllRefundService` — `AllRefund_TwitchReward` (2 handlers) ✅

---

**Итого:** 42 обработчика в 39 файлах — **ВСЕ МИГРИРОВАНЫ** ✅
