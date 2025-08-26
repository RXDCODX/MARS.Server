# PowerShell скрипт для тестирования Twitch Message Builder Service
# Запускать от имени администратора

param(
    [string]$BaseUrl = "http://localhost:5000",
    [switch]$CreateExamples,
    [switch]$TestAll
)

# Цвета для вывода
$Colors = @{
    Success = "Green"
    Error = "Red"
    Info = "Yellow"
    Warning = "Magenta"
}

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Colors[$Color]
}

function Test-Endpoint {
    param(
        [string]$Method,
        [string]$Endpoint,
        [object]$Body = $null,
        [string]$Description
    )
    
    Write-ColorOutput "`n=== $Description ===" "Info"
    Write-ColorOutput "$Method $Endpoint" "Info"
    
    try {
        $headers = @{
            "Content-Type" = "application/json"
        }
        
        $params = @{
            Uri = "$BaseUrl$Endpoint"
            Method = $Method
            Headers = $headers
        }
        
        if ($Body) {
            $params.Body = $Body | ConvertTo-Json -Depth 10
        }
        
        $response = Invoke-RestMethod @params
        
        Write-ColorOutput "✅ Успешно!" "Success"
        if ($response) {
            Write-ColorOutput "Ответ: $($response | ConvertTo-Json -Depth 3)" "Success"
        }
        
        return $response
    }
    catch {
        Write-ColorOutput "❌ Ошибка: $($_.Exception.Message)" "Error"
        if ($_.Exception.Response) {
            $statusCode = $_.Exception.Response.StatusCode
            Write-ColorOutput "HTTP Status: $statusCode" "Error"
        }
        return $null
    }
}

function Create-ExampleTemplates {
    Write-ColorOutput "`n🔧 Создание примеров шаблонов..." "Info"
    
    $examples = @(
        @{
            name = "Приветствие"
            messageTemplate = "Привет, {user}! Рад тебя видеть в чате! 👋"
            description = "Автоматическое приветствие новых пользователей"
            triggerWord = "привет"
            priority = 1
            randomChance = 100
            cooldownSeconds = 60
        },
        @{
            name = "Команда помощи"
            messageTemplate = "{user}, вот список доступных команд: !help, !info, !stats, !commands"
            description = "Помощь по командам"
            triggerWord = "помощь"
            authorColor = "#00FF00"
            authorName = "Бот-помощник"
            priority = 10
            randomChance = 100
            cooldownSeconds = 30
        },
        @{
            name = "Случайная шутка"
            messageTemplate = "{user}, вот шутка для тебя: Почему программисты путают Рождество и Хэллоуин? Потому что Oct 31 == Dec 25! 😄"
            description = "Случайные шутки для программистов"
            triggerWord = "шутка"
            authorColor = "#FF00FF"
            authorName = "Шутник"
            priority = 5
            randomChance = 30
            cooldownSeconds = 300
        }
    )
    
    $createdTemplates = @()
    
    foreach ($example in $examples) {
        $response = Test-Endpoint -Method "POST" -Endpoint "/api/twitch/message-templates" -Body $example -Description "Создание шаблона: $($example.name)"
        if ($response) {
            $createdTemplates += $response
        }
    }
    
    return $createdTemplates
}

function Test-AllEndpoints {
    Write-ColorOutput "`n🧪 Тестирование всех endpoints..." "Info"
    
    # Получить все шаблоны
    $templates = Test-Endpoint -Method "GET" -Endpoint "/api/twitch/message-templates" -Description "Получение всех шаблонов"
    
    if ($templates -and $templates.Count -gt 0) {
        $firstTemplate = $templates[0]
        $templateId = $firstTemplate.id
        
        # Получить шаблон по ID
        Test-Endpoint -Method "GET" -Endpoint "/api/twitch/message-templates/$templateId" -Description "Получение шаблона по ID"
        
        # Обновить шаблон
        $updateData = @{
            priority = 99
            description = "Обновленное описание для тестирования"
        }
        Test-Endpoint -Method "PUT" -Endpoint "/api/twitch/message-templates/$templateId" -Body $updateData -Description "Обновление шаблона"
        
        # Получить шаблоны по триггеру
        $triggerWord = $firstTemplate.triggerWord
        Test-Endpoint -Method "GET" -Endpoint "/api/twitch/message-templates/by-trigger/$triggerWord" -Description "Получение шаблонов по триггеру"
        
        # Получить активные шаблоны
        Test-Endpoint -Method "GET" -Endpoint "/api/twitch/message-templates/active" -Description "Получение активных шаблонов"
        
        # Получить статистику
        Test-Endpoint -Method "GET" -Endpoint "/api/twitch/message-templates/stats" -Description "Получение статистики"
        
        # Удалить тестовый шаблон (если это был созданный нами)
        if ($firstTemplate.name -eq "Тестовый шаблон") {
            Test-Endpoint -Method "DELETE" -Endpoint "/api/twitch/message-templates/$templateId" -Description "Удаление тестового шаблона"
        }
    }
}

# Основная логика
Write-ColorOutput "🚀 Запуск тестирования Twitch Message Builder Service" "Info"
Write-ColorOutput "Base URL: $BaseUrl" "Info"

# Проверка доступности сервиса
Write-ColorOutput "`n🔍 Проверка доступности сервиса..." "Info"
try {
    $healthCheck = Invoke-RestMethod -Uri "$BaseUrl/api/twitch/message-templates" -Method "GET" -TimeoutSec 10
    Write-ColorOutput "✅ Сервис доступен!" "Success"
}
catch {
    Write-ColorOutput "❌ Сервис недоступен: $($_.Exception.Message)" "Error"
    Write-ColorOutput "Убедитесь, что сервис запущен и доступен по адресу: $BaseUrl" "Warning"
    exit 1
}

if ($CreateExamples) {
    $createdTemplates = Create-ExampleTemplates
    Write-ColorOutput "`n📝 Создано шаблонов: $($createdTemplates.Count)" "Success"
}

if ($TestAll) {
    Test-AllEndpoints
}

Write-ColorOutput "`n✨ Тестирование завершено!" "Success"
Write-ColorOutput "Для создания примеров используйте: -CreateExamples" "Info"
Write-ColorOutput "Для полного тестирования используйте: -TestAll" "Info"
