#!/bin/bash
#
# Скрипт для ручного форматирования проекта MARS.Server
#

set -e

# Параметры
CHECK=false
VERBOSE=false

# Парсинг аргументов
while [[ $# -gt 0 ]]; do
    case $1 in
        --check)
            CHECK=true
            shift
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        *)
            echo "Неизвестный параметр: $1"
            echo "Использование: $0 [--check] [--verbose]"
            exit 1
            ;;
    esac
done

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_DIR="$SCRIPT_DIR"

echo "🎨 CSharpier Formatter для MARS.Server"
echo "=================================================="

# Проверяем установлен ли CSharpier
echo ""
echo "🔍 Проверка CSharpier..."

if ! dotnet tool list -g | grep -q "csharpier"; then
    echo "❌ CSharpier не установлен глобально"
    echo "Установите: dotnet tool install -g csharpier"
    exit 1
fi

echo "✅ CSharpier найден"

# Определяем режим работы
COMMAND="dotnet csharpier"

if [ "$CHECK" = true ]; then
    echo ""
    echo "🔍 Режим проверки (без изменения файлов)..."
    COMMAND="$COMMAND --check"
else
    echo ""
    echo "🔧 Форматирование проекта..."
fi

if [ "$VERBOSE" = true ]; then
    COMMAND="$COMMAND --verbose"
fi

COMMAND="$COMMAND \"$PROJECT_DIR\""

echo "Команда: $COMMAND"
echo ""

# Выполняем команду
if eval $COMMAND; then
    if [ "$CHECK" = true ]; then
        echo ""
        echo "✅ Проверка завершена: все файлы отформатированы корректно"
    else
        echo ""
        echo "✅ Форматирование завершено успешно!"
    fi
else
    EXIT_CODE=$?
    if [ "$CHECK" = true ]; then
        echo ""
        echo "⚠️  Найдены файлы, требующие форматирования"
        echo "Запустите без параметра --check для форматирования"
    else
        echo ""
        echo "❌ Ошибка форматирования"
    fi
    exit $EXIT_CODE
fi

echo ""
echo "=================================================="
echo "Готово!"
