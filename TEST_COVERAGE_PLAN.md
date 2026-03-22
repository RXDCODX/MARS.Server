# План покрытия проекта тестами

## Цель
Построить поэтапное и измеримое покрытие тестами для backend, frontend и критичных интеграций (БД, HTTP API, SignalR, внешние сервисы), чтобы снизить регрессии и ускорить безопасные релизы.

## Текущее состояние (baseline)
- [x] Есть серверный тестовый проект: MARS.Projects/MARS.Tests
- [x] Есть базовые unit-тесты в MARS.Tests:
  - [x] Services/Twitch/Synthesizer/SyntheziaVoicerTests.cs
  - [x] Services/Twitch/PuntoSwitcher/PuntoSwitcherServiceTests.cs
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
- [ ] Настроить единый запуск backend-тестов через dotnet test
- [ ] Настроить единый запуск frontend-тестов через vitest
- [ ] Добавить сбор отчёта покрытия (coverlet + reportgenerator для .NET, vitest coverage для фронта)
- [ ] Подготовить шаблон отчёта покрытия для PR

## Этап 2. Backend unit-тесты (MARS.Server Services)

### 2.1 Критичные домены
- [ ] Services/CommandExecutor: парсинг, маршрутизация, ошибки команд
- [ ] Services/ServiceManager: старт/стоп/рестарт, ошибки процесса
- [ ] Services/EnvironmentVariable: чтение/валидация/апдейт переменных
- [ ] Services/Logs: фильтрация, пагинация, форматирование
- [ ] Services/RandomMem: выборка, фильтры, fallback-сценарии
- [ ] Services/SoundRequest: постановка/очередь/валидация
- [ ] Services/CinemaQueue: управление очередью, конфликтные кейсы
- [ ] Services/Shikimori: rate-limit и обработка ошибок клиента
- [ ] Services/PyroAlerts: правила алертов и граничные условия
- [ ] Services/Scoreboard: расчёты и синхронизация состояния

### 2.2 Twitch/Telegram/Bridge
- [ ] Services/Twitch/*: расширить покрытие кроме существующих тестов
- [ ] Services/TelegramBotService: обработка входящих команд/состояний
- [ ] Services/TelegramDiscordBridge: маппинг и маршрутизация сообщений
- [ ] Services/WaifuRoll: позитивные и негативные ветки бизнес-логики

### 2.3 Качество unit-тестов
- [ ] Для каждого сервиса: happy-path + negative-path + exception-path
- [ ] Проверка сообщений OperationResult (Success/Message/Data)
- [ ] Проверка edge-cases (null/empty/duplicate/overflow)

## Этап 3. Backend интеграционные тесты

### 3.1 API-контроллеры (через TestServer/WebApplicationFactory)
- [ ] Controllers/CommandsController
- [ ] Controllers/ServiceManagerController
- [ ] Controllers/EnvironmentVariableController
- [ ] Controllers/LogsController
- [ ] Controllers/RandomMemeController
- [ ] Controllers/SoundRequestController
- [ ] Controllers/CinemaQueueController
- [ ] Controllers/TwitchController
- [ ] Controllers/TwitchRewardsController
- [ ] Controllers/WTelegramController

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
