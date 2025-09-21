using Microsoft.AspNetCore.Http.HttpResults;

namespace MARS.Server.Services;

/// <summary>
/// Результат некой операции
/// </summary>
public class OperationResult(bool success = false, string? message = null, object? data = null)
{
    /// <summary>
    /// Флаг успеха операции
    /// </summary>
    public bool Success { get; set; } = success;

    /// <summary>
    /// Сообщение о результате операции
    /// </summary>
    public string? Message { get; set; } = message;

    /// <summary>
    /// Хранимый объект данных
    /// </summary>
    public object? Data { get; set; } = data;

    /// <summary>
    /// Успешный результат
    /// </summary>
    /// <param name="message">Сообщение</param>
    /// <param name="data">Данные</param>
    /// <returns></returns>
    public static OperationResult Ok(string? message = null, object? data = null) =>
        new(true, message, data);

    /// <summary>
    /// Негативный результат
    /// </summary>
    /// <param name="message">Сообщение</param>
    /// <param name="data">Данные</param>
    /// <returns></returns>
    public static OperationResult Bad(string? message = null, object? data = null) =>
        new(false, message, data);

    /// <summary>
    /// Неопределенный результат
    /// </summary>
    /// <param name="success">Флаг успеха</param>
    /// <param name="message">Сообщение</param>
    /// <param name="data">Данные</param>
    /// <returns></returns>
    public static OperationResult Some(
        bool success = false,
        string? message = null,
        object? data = null
    ) => new(success, message, data);

    /// <summary>
    /// Замещение оператора отрицания, используестя в логических условиях
    /// </summary>
    /// <param name="operationResult"></param>
    /// <returns></returns>
    public static bool operator !(OperationResult operationResult) => !operationResult.Success;

    /// <summary>
    /// Замещение сравнения с Boolean true
    /// </summary>
    /// <param name="operationResult"></param>
    /// <returns></returns>
    public static bool operator true(OperationResult operationResult) =>
        operationResult.Success == true;

    /// <summary>
    /// Замещение сравнения с Boolean false
    /// </summary>
    /// <param name="operationResult"></param>
    /// <returns></returns>
    public static bool operator false(OperationResult operationResult) =>
        operationResult.Success == false;
}

/// <summary>
/// Результат нейкой операции с данными определенного типа
/// </summary>
/// <typeparam name="TData">Тип хранимых данных</typeparam>
public class OperationResult<TData>(
    bool success = false,
    string? message = null,
    TData data = default!
) : OperationResult(success, message)
{
    /// <summary>
    /// Хранимый объект данных
    /// </summary>
    public new TData Data { get; set; } = data;

    /// <summary>
    /// Успешный результат
    /// </summary>
    /// <typeparam name="TData">Тип хранимых данных</typeparam>
    /// <param name="message">Сообщение</param>
    /// <param name="data">Данные</param>
    /// <returns></returns>
    public static OperationResult<TData> Ok(string? message = null, TData data = default!) =>
        new(true, message, data);

    /// <summary>
    /// Успешный результат
    /// </summary>
    /// <param name="message">Сообщение</param>
    /// <param name="data">Данные</param>
    /// <returns></returns>
    public static OperationResult<TData> Bad(string? message = null, TData data = default!) =>
        new(false, message, data);

    /// <summary>
    /// Неопределенный результат
    /// </summary>
    /// <param name="success">Флаг успеха</param>
    /// <param name="message">Сообщение</param>
    /// <param name="data">Данные</param>
    /// <returns></returns>
    public static OperationResult<TData> Some(
        bool success = false,
        string? message = null,
        TData data = default!
    ) => new(success, message, data);

    /// <summary>
    /// Неявное преобразование в тип данных
    /// </summary>
    /// <param name="operationResult"></param>
    /// <returns></returns>
    public static implicit operator TData(OperationResult<TData> operationResult) =>
        operationResult.Data;
}
