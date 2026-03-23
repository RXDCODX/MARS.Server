# План покрытия проекта тестами

## Цель
Построить поэтапное и измеримое покрытие тестами для backend, frontend и критичных интеграций (БД, HTTP API, SignalR, внешние сервисы), чтобы снизить регрессии и ускорить безопасные релизы.

## Текущее состояние (baseline)
- [x] Есть серверный тестовый проект: MARS.Projects/MARS.Tests
- [x] Есть базовые unit-тесты в MARS.Tests:
  - [x] Services/Twitch/Synthesizer/SyntheziaVoicerTests.cs
  - [x] Services/Twitch/PuntoSwitcher/PuntoSwitcherServiceTests.cs
  - [x] Services/OperationResultTests.cs
  - [x] Services/MemoryStorageService/MemoryStorageTests.cs
  - [x] Services/MemoryStorageService/MemoryFileTests.cs
  - [x] Exstensions/StringExtensionTests.cs
  - [x] Services/CommandExecutor/BaseCommandTests.cs
  - [x] Services/CommandExecutor/CommandFactoryTests.cs
  - [x] Services/CommandExecutor/CommandExecutorServiceTests.cs
  - [x] Services/CommandExecutor/Commands/TelegramOnlyCommandTests.cs
  - [x] Services/CommandExecutor/Adapters/ApiCommandServiceTests.cs
  - [x] Services/CommandExecutor/PlatformCommandServiceBaseTests.cs
  - [x] Services/CommandExecutor: platform/info/alias execution scenarios в CommandExecutorServiceTests
  - [x] Services/Logs/LogsServiceTests.cs
  - [x] Services/ServiceManager/ManagedServiceBaseTests.cs
  - [x] Services/ServiceManager/ServiceManagerTests.cs
- [x] Есть минимум 1 frontend unit-тест:
  - [x] mars.client/src/shared/Utils/faceUtils.test.ts
- [ ] Нет системного покрытия контроллеров интеграционными тестами
- [ ] Нет обязательных quality-gate по покрытию в CI

## Целевые метрики покрытия
- [ ] Backend unit coverage: не менее 70% по строкам для бизнес-сервисов
- [ ] Backend integration coverage: не менее 60% критичных API endpoint-ов
- [ ] Frontend unit coverage: не менее 60% для shared/business логики
- [ ] Критичные сценарии (Smoke/Happy-path): 100% покрытие интеграционными тестами
- [ ] Негативные сценарии (ошибки валидации/внешних зависимостей): минимум 1 тест на сценарий

## Этап 1. Фундамент тестовой инфраструктуры
- [ ] Утвердить и зафиксировать стратегию тестирования (unit/integration/e2e/smoke)
- [ ] Добавить тестовые фикстуры и фабрики данных для MARS.Tests
- [ ] Вынести общие моки и helper-классы в общий каталог TestInfrastructure
- [x] Настроить единый запуск backend-тестов через dotnet test
- [ ] Настроить единый запуск frontend-тестов через vitest
- [ ] Добавить сбор отчёта покрытия (coverlet + reportgenerator для .NET, vitest coverage для фронта)
- [ ] Подготовить шаблон отчёта покрытия для PR

## Этап 2. Backend unit-тесты (MARS.Server Services)

### 2.1 Критичные домены
- [x] Services/CommandExecutor: парсинг, маршрутизация, ошибки команд
  - Прогресс: добавлены unit-тесты на парсинг параметров, кавычки/экранирование, проверку платформ и видимости, DI-создание команд (CommandFactory), Telegram-only команду, фильтрацию user/admin, alias-резолвинг, получение параметров по alias, platform-фильтрацию списков, методы CommandInfo, success/unknown/unavailable сценарии ExecuteCommandAsync и валидацию обязательных параметров, а также API-адаптер (ApiCommandService: alias execution, platform unavailable, required params, user/admin lists, validate response)
- [x] Services/ServiceManager: старт/стоп/рестарт, ошибки процесса
  - Прогресс: добавлены unit-тесты lifecycle для ManagedServiceBase (start/stop, disabled, already running/stopped, error/exception ветки, LoadStateAsync и snapshot GetServiceInfo), ServiceManager read-only/guard сценарии (status/info/logs/all services, invalid/missing service для start/stop/restart/set-active), успешные managed-сценарии start/stop/restart/set-active c проверкой сохранения состояния в БД, а также mapping display/description через ServiceNameAttribute и mixed managed+hosted список сервисов
- [ ] Services/EnvironmentVariable: чтение/валидация/апдейт переменных
  - Прогресс: добавлены unit-тесты EnvironmentVariableController на чтение списка, получение по ключу (включая auto-create), set/create, delete, reload и валидацию пустого ключа
- [x] Services/Logs: фильтрация, пагинация, форматирование
  - Прогресс: добавлены unit-тесты на фильтрацию/сортировку/пагинацию, recent logs и агрегированную статистику
- [x] Services/RandomMem: выборка, фильтры, fallback-сценарии
  - Прогресс: добавлены unit-тесты RandomMemeService на MemeType/MemeOrder CRUD, защиту удаления типа при наличии заказов, count с фильтрами, random no-data и пересортировку order
- [x] Services/SoundRequest: постановка/очередь/валидация
  - Прогресс: добавлены unit-тесты StateManager (инициализация/персистентность, playback transitions, clamp громкости, StateChanged), CommandsService (валидация входа и получение current song) и SoundRequestUserQueue (count/current/next/get-by-id/user filtering)
- [x] Services/CinemaQueue: управление очередью, конфликтные кейсы
  - Прогресс: добавлены unit-тесты CinemaQueueService на чтение/маппинг, create/update, mark-as-next (reset флагов), смену статуса, aggregation статистики и negative-сценарии для отсутствующих сущностей
- [x] Services/Shikimori: rate-limit и обработка ошибок клиента
  - Прогресс: добавлены unit-тесты Shikimori rate limiter (acquire/info/cancel) и ShikimoriService на guard/error-ветки (invalid id, исключения в WaitForSlotAsync, GetRateLimiterInfo)
- [x] Services/PyroAlerts: правила алертов и граничные условия
  - Прогресс: добавлены unit-тесты PyroAlertsHelper/Handler на граничные сценарии (DownloadFile null size/null path, EnsureDirectoryExists), а также правила обработки апдейтов и доступа (not-message, user without access, user with access but no media)
- [x] Services/Scoreboard: расчёты и синхронизация состояния
  - Прогресс: добавлены unit-тесты ScoreboardService на default/get state, debounced update + force process, visibility create/update, update score/final и negative-ветки валидации входа

### 2.2 Twitch/Telegram/Bridge
- [x] Services/Twitch/*: расширить покрытие кроме существующих тестов
  - Прогресс: добавлены unit-тесты TwitchUserInfoService на guard/edge-сценарии (empty user id и empty user lists), а также фильтрацию пользователей без аватаров
- [ ] Services/TelegramBotService: обработка входящих команд/состояний
- [x] Services/TelegramDiscordBridge: маппинг и маршрутизация сообщений
  - Прогресс: добавлены unit-тесты TelegramDiscordBridgeService на CRUD/валидацию связей и состояние каналов (add invalid/new/duplicate, set enabled, delete empty/not found, get bindings/states)
- [x] Services/WaifuRoll: позитивные и негативные ветки бизнес-логики
  - Прогресс: добавлены unit-тесты WaifuRollService на guard/negative и базовые бизнес-ветки (TelegramRollWaifu: empty name/host not found/host found, AddNewWaifu null, MergeTheWaifu null args, cooldown default/configured)

### 2.3 Качество unit-тестов
- [ ] Для каждого сервиса: happy-path + negative-path + exception-path
- [ ] Проверка сообщений OperationResult (Success/Message/Data)
- [ ] Проверка edge-cases (null/empty/duplicate/overflow)

## Этап 3. Backend интеграционные тесты

### 3.1 API-контроллеры (через TestServer/WebApplicationFactory)
- [ ] Controllers/CommandsController
  - Прогресс: добавлены unit-тесты контроллера на success-контракты OperationResult для user/admin/platform/info/execute и error-ветки для всех основных endpoint-ов при исключении сервиса
- [ ] Controllers/ServiceManagerController
  - Прогресс: добавлены unit-тесты контроллера на success/negative/exception сценарии для endpoint-ов status, service info (found/not found), start/stop/restart, logs, set active и all services
- [ ] Controllers/EnvironmentVariableController
  - Прогресс: добавлены unit-тесты контроллера на success и negative сценарии (пустой ключ, not found, create/update/delete/reload)
- [ ] Controllers/LogsController
  - Прогресс: добавлены unit-тесты контроллера на success-контракт, валидацию пагинации/диапазона/count и error-ветки при исключениях сервиса (by-level/statistics)
- [ ] Controllers/RandomMemeController
  - Прогресс: добавлены unit-тесты контроллера на MemeType/MemeOrder CRUD ветки (success/not found/validation/invalid-operation), random/count, file not found ветки и reorder exception-ветку
- [ ] Controllers/SoundRequestController
  - Прогресс: добавлены unit-тесты на ключевые guard/exception-контракты endpoint-ов state/queue/history/current-song/delete/add-track (включая empty-id и empty-query)
- [ ] Controllers/CinemaQueueController
  - Прогресс: добавлены unit-тесты контроллера на очередь (all/by-id/next), create (validation + metadata enrichment), update model validation, delete/change status, statistics exception и metadata endpoint (empty url/not found)
- [ ] Controllers/TwitchController
  - Прогресс: добавлены unit-тесты на guard-ветку (пустой auth code) и exception-контракт при сбое зависимостей
- [ ] Controllers/TwitchRewardsController
  - Прогресс: добавлены unit-тесты на валидацию ids для update redemption status и exception-контракт при получении наград
- [ ] Controllers/WTelegramController
  - Прогресс: добавлены unit-тесты error-контрактов для relogin/status (HTTP 500 + WTelegramOperationResult failure)

### 3.2 Интеграция с БД
- [ ] Поднять тестовую БД (предпочтительно PostgreSQL в Testcontainers)
- [ ] Прогон миграций в setup тестов
- [ ] Проверить read/write сценарии для AppDbContext
- [ ] Проверить корректность AsNoTracking-операций на read-only запросах
- [ ] Проверить rollback/изоляцию между тестами

### 3.3 SignalR и realtime
- [ ] Интеграционные тесты для Hub-ов (подключение, события, отписка)
- [ ] Проверка контракта сообщений (payload и типы)
- [ ] Нагрузочный smoke для частых realtime-событий

### 3.4 Внешние зависимости
- [ ] Подменить внешние HTTP API через mock server/WireMock
- [ ] Проверить таймауты и retry-поведение
- [ ] Проверить graceful degradation при недоступности внешних сервисов

## Этап 4. Frontend тесты (mars.client)

### 4.1 Unit-тесты утилит и стора
- [x] src/shared/Utils/faceUtils.test.ts (уже есть)
- [ ] Добавить тесты для shared/utils с бизнес-логикой
- [ ] Добавить тесты для Zustand store (селекторы, actions, rollback optimistic)
- [ ] Добавить тесты для API-client adapters и трансформаций данных

### 4.2 Компонентные тесты
- [ ] Ключевые страницы: Commands, Logs, RandomMeme, Framedata
- [ ] OBS компоненты с критичной логикой (например, SoundRequest/Scoreboard)
- [ ] Проверка loading/error/empty/data состояний

### 4.3 Интеграционные frontend-сценарии
- [ ] Интеграция UI + store + api mocks (MSW)
- [ ] Проверка optimistic update + rollback
- [ ] Проверка realtime-апдейтов без перетирания локального optimistic state

## Этап 5. Smoke и регрессионный набор
- [ ] Smoke-набор на каждый релиз (backend + frontend)
- [ ] Регрессионные тест-кейсы на ранее найденные баги
- [ ] Отдельный nightly-прогон расширенного интеграционного набора

## Этап 6. CI/CD quality gates
- [ ] Запуск backend unit + integration в CI
- [ ] Запуск frontend unit/component в CI
- [ ] Публикация отчётов покрытия как артефактов
- [ ] Блокировка merge при падении smoke/critical интеграций
- [ ] Минимальные пороги покрытия по слоям (backend/frontend)

## Приоритезация (рекомендуемый порядок внедрения)
- [ ] P0: CommandExecutor, ServiceManager, SoundRequest, RandomMeme + их контроллеры
- [ ] P0: Интеграция БД и базовые API smoke тесты
- [ ] P1: Twitch/Telegram/Bridge сервисы
- [ ] P1: Frontend store + ключевые страницы
- [ ] P2: Расширенный realtime + нагрузочные интеграционные тесты

## Definition of Done для этапов
- [ ] Для этапа завершены все отмеченные чекбоксы
- [ ] Тесты стабильны (не flaky) минимум в 3 последовательных прогонах
- [ ] Порог покрытия этапа достигнут и зафиксирован в отчёте
- [ ] Документация по запуску тестов обновлена

## Короткий операционный чек-лист на каждую новую фичу
- [ ] Добавлен/обновлён unit-тест бизнес-логики
- [ ] Добавлен/обновлён интеграционный тест API/SignalR при изменении контракта
- [ ] Для UI-изменений добавлен компонентный/интеграционный тест
- [ ] Проверены негативные сценарии и ошибки внешних зависимостей
- [ ] Обновлён регрессионный кейс, если фиксился баг
