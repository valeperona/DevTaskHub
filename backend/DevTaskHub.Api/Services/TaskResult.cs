using DevTaskHub.Api.Models;

namespace DevTaskHub.Api.Services;

public enum TaskResultStatus
{
    Success,
    NotFound,
    Forbid,
    BadRequest
}

public sealed class TaskResult
{
    public TaskResultStatus Status { get; }
    public string? Message { get; }

    private TaskResult(TaskResultStatus status, string? message = null)
    {
        Status = status;
        Message = message;
    }

    public static TaskResult Success() => new(TaskResultStatus.Success);
    public static TaskResult NotFound() => new(TaskResultStatus.NotFound);
    public static TaskResult Forbid() => new(TaskResultStatus.Forbid);
    public static TaskResult BadRequest(string message) => new(TaskResultStatus.BadRequest, message);
}

public sealed class TaskResult<T>
{
    public TaskResultStatus Status { get; }
    public string? Message { get; }
    public T? Value { get; }

    private TaskResult(TaskResultStatus status, T? value = default, string? message = null)
    {
        Status = status;
        Value = value;
        Message = message;
    }

    public static TaskResult<T> Success(T value) => new(TaskResultStatus.Success, value);
    public static TaskResult<T> NotFound() => new(TaskResultStatus.NotFound);
    public static TaskResult<T> Forbid() => new(TaskResultStatus.Forbid);
    public static TaskResult<T> BadRequest(string message) => new(TaskResultStatus.BadRequest, default, message);
}
