@echo off
echo Тестирование сервиса резервного копирования базы данных (MemoryStorage)
echo =====================================================================

set BASE_URL=http://localhost:5000/api/DatabaseBackup

echo.
echo 1. Создание резервной копии базы данных dev...
curl -X POST "%BASE_URL%/create?databaseName=dev" -H "Content-Type: application/json"

echo.
echo.
echo 2. Получение списка резервных копий...
curl -X GET "%BASE_URL%/list" -H "Content-Type: application/json"

echo.
echo.
echo 3. Получение статуса резервного копирования...
curl -X GET "%BASE_URL%/status" -H "Content-Type: application/json"

echo.
echo.
echo 4. Очистка старых резервных копий (оставить 5)...
curl -X POST "%BASE_URL%/cleanup?keepCount=5" -H "Content-Type: application/json"

echo.
echo.
echo Тестирование завершено!
echo.
echo Примечание: Файлы теперь хранятся в MemoryStorage и доступны по URL /memory/{filename}
pause
