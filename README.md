# MARS.Server

Проект MARS.Server с автоматическим форматированием кода.

## 🎨 Форматирование кода

Проект использует [CSharpier](https://csharpier.com/) для автоматического форматирования C# кода.

### Быстрый старт

#### Установка Pre-Commit Hook

**Windows (PowerShell):**
```powershell
cd D:\VS\MARS\MARS.Projects\MARS.Server
.\install-hooks.ps1
```

**Linux/Mac:**
```bash
cd /path/to/MARS/MARS.Projects/MARS.Server
chmod +x install-hooks.sh
./install-hooks.sh
```

После установки, весь код будет автоматически форматироваться при каждом коммите.

#### Ручное форматирование

**Windows (PowerShell):**
```powershell
.\format.ps1              # Форматировать проект
.\format.ps1 -Check       # Только проверить (без изменений)
.\format.ps1 -Verbose     # С подробным выводом
```

**Linux/Mac:**
```bash
chmod +x format.sh
./format.sh               # Форматировать проект
./format.sh --check       # Только проверить (без изменений)
./format.sh --verbose     # С подробным выводом
```

**Напрямую через dotnet:**
```bash
dotnet csharpier .                    # Форматировать весь проект
dotnet csharpier --check .            # Проверить без изменений
dotnet csharpier ./Controllers        # Форматировать конкретную папку
```

### Настройка

Конфигурация CSharpier находится в `.csharpierrc.json`:
```json
{
  "printWidth": 100,
  "useTabs": false,
  "tabWidth": 4,
  "endOfLine": "lf"
}
```

Исключения (файлы которые не форматируются) в `.csharpierignore`.

### Подробная документация

См. [CSHARPIER_SETUP.md](./CSHARPIER_SETUP.md) для полной документации.

---

## 📚 Другая документация

- [WTelegram Setup](./README_WTELEGRAM.md) - Настройка WTelegram с автопереавторизацией
- [WTelegram Quick Start](./QUICKSTART_WTELEGRAM.md) - Быстрый старт с WTelegram
- [WTelegram Migration](./Services/TelegramBotService/WTELEGRAM_MIGRATION.md) - Миграция на новый API

---

## 🚀 Разработка

### Требования

- .NET 10 SDK
- PostgreSQL (для базы данных)
- CSharpier (для форматирования)

### Запуск проекта

```bash
dotnet restore
dotnet build
dotnet run
```

### Стиль кода

Проект следует стандартам форматирования CSharpier. Пожалуйста:
- ✅ Установите pre-commit hook (см. выше)
- ✅ Форматируйте код перед коммитом
- ✅ Используйте `.editorconfig` настройки вашей IDE
- ❌ Не используйте `git commit --no-verify` без необходимости

---

## 🤝 Вклад в проект

1. Установите pre-commit hook (обязательно!)
2. Создайте ветку для вашей фичи
3. Сделайте изменения (код будет автоматически отформатирован)
4. Создайте Pull Request

---

## 📝 Лицензия

Свободная лицензия

## 👥 Авторы

RXDCODX
