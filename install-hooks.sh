#!/bin/bash
#
# Bash скрипт для установки Git pre-commit hook для форматирования MARS.Server
#

set -e

echo "🔧 Установка Git pre-commit hook для MARS.Server..."

# Определяем пути
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"
GIT_HOOKS_SOURCE="$SCRIPT_DIR/.githooks"
GIT_DIR="$PROJECT_ROOT/.git"
GIT_HOOKS_DIR="$GIT_DIR/hooks"

# Проверяем, что мы в Git репозитории
if [ ! -d "$GIT_DIR" ]; then
    echo "❌ Ошибка: .git директория не найдена в $PROJECT_ROOT"
    echo "Убедитесь, что вы находитесь в корне Git репозитория MARS.Server"
    exit 1
fi

# Создаем директорию hooks если её нет
if [ ! -d "$GIT_HOOKS_DIR" ]; then
    mkdir -p "$GIT_HOOKS_DIR"
    echo "✅ Создана директория: $GIT_HOOKS_DIR"
fi

# Копируем pre-commit hook
SOURCE_HOOK="$GIT_HOOKS_SOURCE/pre-commit"
TARGET_HOOK="$GIT_HOOKS_DIR/pre-commit"

if [ ! -f "$SOURCE_HOOK" ]; then
    echo "❌ Ошибка: Файл pre-commit не найден в $GIT_HOOKS_SOURCE"
    exit 1
fi

cp "$SOURCE_HOOK" "$TARGET_HOOK"
chmod +x "$TARGET_HOOK"
echo "✅ Pre-commit hook установлен в $TARGET_HOOK"

# Проверяем установлен ли CSharpier
echo ""
echo "🔍 Проверка установки CSharpier..."

if ! dotnet tool list -g | grep -q "csharpier"; then
    echo "⚠️  CSharpier не установлен глобально"
    echo -n "Хотите установить CSharpier сейчас? (y/n): "
    read -r response
    
    if [[ "$response" =~ ^[Yy]$ ]]; then
        echo "📦 Установка CSharpier..."
        dotnet tool install -g csharpier
        
        if [ $? -eq 0 ]; then
            echo "✅ CSharpier успешно установлен"
        else
            echo "❌ Ошибка установки CSharpier"
            exit 1
        fi
    else
        echo "⚠️  Установите CSharpier вручную: dotnet tool install -g csharpier"
    fi
else
    echo "✅ CSharpier уже установлен"
fi

echo ""
echo "✨ Установка завершена!"
echo ""
echo "Теперь при каждом коммите проект MARS.Server будет автоматически форматироваться."
echo "Для ручного форматирования используйте: dotnet csharpier MARS.Projects/MARS.Server"
