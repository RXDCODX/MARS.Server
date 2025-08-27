# Тестирование сервиса резервного копирования базы данных (MemoryStorage)
# PowerShell скрипт

$BaseUrl = "http://localhost:5000/api/DatabaseBackup"

Write-Host "Тестирование сервиса резервного копирования базы данных (MemoryStorage)" -ForegroundColor Green
Write-Host "=====================================================================" -ForegroundColor Green

try {
  # 1. Создание резервной копии базы данных dev
  Write-Host "`n1. Создание резервной копии базы данных dev..." -ForegroundColor Yellow
  $createResponse = Invoke-RestMethod -Uri "$BaseUrl/create?databaseName=dev" -Method POST -ContentType "application/json"
  Write-Host "Результат: $($createResponse.message)" -ForegroundColor Green
  Write-Host "URL для скачивания: $($createResponse.downloadUrl)" -ForegroundColor Cyan
  Write-Host "Имя файла: $($createResponse.fileName)" -ForegroundColor Cyan
    
  # Сохраняем имя файла для дальнейших операций
  $fileName = $createResponse.fileName
    
  # 2. Получение списка резервных копий
  Write-Host "`n2. Получение списка резервных копий..." -ForegroundColor Yellow
  $listResponse = Invoke-RestMethod -Uri "$BaseUrl/list" -Method GET -ContentType "application/json"
  Write-Host "Найдено резервных копий: $($listResponse.totalCount)" -ForegroundColor Green
    
  foreach ($backup in $listResponse.backups) {
    Write-Host "  - $($backup.fileName) ($($backup.sizeMB) МБ, создан: $($backup.created), БД: $($backup.databaseName))" -ForegroundColor White
  }
    
  # 3. Получение статуса резервного копирования
  Write-Host "`n3. Получение статуса резервного копирования..." -ForegroundColor Yellow
  $statusResponse = Invoke-RestMethod -Uri "$BaseUrl/status" -Method GET -ContentType "application/json"
  Write-Host "Общее количество: $($statusResponse.status.totalBackups)" -ForegroundColor Green
  Write-Host "Общий размер: $($statusResponse.status.totalSizeMB) МБ" -ForegroundColor Green
  Write-Host "Хранилище: $($statusResponse.status.storageInfo)" -ForegroundColor Cyan
    
  # 4. Скачивание резервной копии (если она была создана)
  if ($fileName) {
    Write-Host "`n4. Скачивание резервной копии..." -ForegroundColor Yellow
    $downloadUrl = "$BaseUrl/download?fileName=$([System.Web.HttpUtility]::UrlEncode($fileName))"
        
    try {
      Invoke-WebRequest -Uri $downloadUrl -OutFile "downloaded_$fileName"
      Write-Host "Файл скачан как: downloaded_$fileName" -ForegroundColor Green
    }
    catch {
      Write-Host "Ошибка при скачивании: $($_.Exception.Message)" -ForegroundColor Red
    }
        
    # 5. Прямое скачивание через MemoryStorage
    Write-Host "`n5. Прямое скачивание через MemoryStorage..." -ForegroundColor Yellow
    $memoryUrl = "http://localhost:5000/memory/$fileName"
        
    try {
      Invoke-WebRequest -Uri $memoryUrl -OutFile "memory_$fileName"
      Write-Host "Файл скачан через MemoryStorage как: memory_$fileName" -ForegroundColor Green
    }
    catch {
      Write-Host "Ошибка при скачивании через MemoryStorage: $($_.Exception.Message)" -ForegroundColor Red
    }
  }
    
  # 6. Очистка старых резервных копий
  Write-Host "`n6. Очистка старых резервных копий (оставить 5)..." -ForegroundColor Yellow
  $cleanupResponse = Invoke-RestMethod -Uri "$BaseUrl/cleanup?keepCount=5" -Method POST -ContentType "application/json"
  Write-Host "Результат: $($cleanupResponse.message)" -ForegroundColor Green
  Write-Host "Удалено файлов: $($cleanupResponse.deletedCount)" -ForegroundColor Cyan
    
  Write-Host "`nТестирование завершено успешно!" -ForegroundColor Green
  Write-Host "Файлы теперь хранятся в MemoryStorage и доступны по URL /memory/{filename}" -ForegroundColor Cyan
}
catch {
  Write-Host "`nОшибка при тестировании: $($_.Exception.Message)" -ForegroundColor Red
  Write-Host "Убедитесь, что сервер запущен и доступен по адресу: $BaseUrl" -ForegroundColor Yellow
}

Write-Host "`nНажмите любую клавишу для продолжения..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
