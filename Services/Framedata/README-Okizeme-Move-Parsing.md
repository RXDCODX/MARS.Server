# Алгоритм получения ударов персонажей с okizeme.gg

Этот документ описывает текущий рабочий пайплайн, который использует `MARS.Server` для загрузки ударов (moves) из `okizeme.gg`.

## 1) Источники данных

Используются два публичных источника:

1. Список персонажей (slug):
   - `https://okizeme.gg/sitemap-0.xml`
   - Извлекаются URL вида `https://okizeme.gg/database/{character-slug}`

2. Мувлист конкретного персонажа:
   - `https://okizeme.gg/api/{character-slug}`
   - Возвращается JSON-массив объектов мувов

## 2) Пошаговый алгоритм

### Шаг 1. Получить список slug персонажей

- Скачать `sitemap-0.xml`
- Регулярным выражением извлечь все `character-slug` из `/database/{slug}`
- Удалить дубликаты
- Исключить пустые значения

### Шаг 2. Нормализовать имя персонажа

- `slug` приводится к lowercase
- `-` заменяется на пробел
- Спец-кейс: `jack-8 -> jack 8`

### Шаг 3. Сформировать объект `TekkenCharacter`

Для каждого `slug`:

- `Name`: нормализованное имя
- `PageUrl`: `https://okizeme.gg/database/{slug}`
- `LinkToImage`: `https://okizeme.gg/assets/images/{slug}-portrait.png`

### Шаг 4. Получить мувы персонажа

- Выполнить `GET https://okizeme.gg/api/{slug}`
- Десериализовать JSON в список DTO
- Игнорировать записи без `command`

### Шаг 5. Нормализовать команду

- `command` -> `Trim()`
- Привести к lowercase
- `.` заменить на пробел
- Если после нормализации пусто — пропустить

### Шаг 6. Смаппить поля мува в `Move`

Маппинг основных полей:

- `command` -> `Move.Command`
- `hitLevel` -> `Move.HitLevel`
- `damage` -> `Move.Damage`
- `startup` -> `Move.StartUpFrame`
- `block` -> `Move.BlockFrame`
- `hit` -> `Move.HitFrame`
- `counter` -> `Move.CounterHitFrame`

Дополнительно:

- `Move.VideoUrl` строится как deep-link на карточку мува:
  - `{PageUrl}#{Uri.EscapeDataString(sourceCommand)}`
  - пример: `https://okizeme.gg/database/alisa#f%2BF%2B2%2C3`
- `Move.Notes` собирается из:
  - строк поля `notes` (разбивка по `\n`, удаление префикса `*`)
  - `transitions` (добавляется как `transitions: ...`)

### Шаг 7. Вычислить флаги свойств удара

На основе `notes + tags` выставляются:

- `PowerCrush` (`power crush`, `pc`)
- `HeatBurst` (`heat burst`, `hb`)
- `HeatEngage` (`heat engager`, `he`)
- `HeatSmash` (`heat smash`, `hs`)
- `Tornado` (`tornado`, `trn`)
- `Homing` (`homing`, `hom`)
- `RequiresHeat` (если команда начинается с `h`)
- `Throw` (если `hitLevel` содержит `t`/`th`)

### Шаг 8. Определить стойку

- Проверить начало `Move.Command` по словарю `Aliases.Stances`
- Если найдено совпадение:
  - `Move.StanceCode = key`
  - `Move.StanceName = value`

### Шаг 9. Консолидация дублей

- Группировка по `(CharacterName, Command)`
- Для дублей:
  - булевы флаги объединяются через `Any`
  - строковые поля берутся из первого элемента
  - `VideoUrl` берется первый непустой
  - `Notes` объединяются

### Шаг 10. Сохранение

- Если включен staging:
  - сохраняется в `TekkenCharactersPending` и `TekkenMovesPending`
- Иначе:
  - сохраняется напрямую в `TekkenCharacters` и `TekkenMoves`

## 3) Надежность и ограничения

- Если `api/{slug}` вернул пустой массив — персонаж логируется как пустой мувлист
- Ошибки по персонажу не прерывают общий процесс (логируются и парсинг продолжается)
- Между запросами соблюдаются задержки из `FramedataParserOptions`
- В supplement-режиме заполняются только пустые поля (включая `VideoUrl`)

## 4) Быстрая ручная проверка

Примеры запросов:

```bash
curl -s https://okizeme.gg/sitemap-0.xml
curl -s https://okizeme.gg/api/alisa
curl -s https://okizeme.gg/api/jack-8
```

Проверить, что в ответе есть поля:
- `command`
- `startup`
- `block`
- `hit`
- `counter`
- `notes`
- `tags`
- `transitions`

## 5) Где в коде это реализовано

- Парсер: `Services/Framedata/Subservices/HtmlParsers/OkizemeFramedataParser.cs`
- Фабрика: `Services/Framedata/Subservices/HtmlParsers/FramedataParserFactory.cs`
- Базовая консолидация/сохранение: `Services/Framedata/Subservices/HtmlParsers/BaseFramedataParser.cs`
- Staging маппинг: `Services/Framedata/FramedataStagingService.cs`
