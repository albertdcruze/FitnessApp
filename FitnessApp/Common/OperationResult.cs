using System;

namespace FitnessApp.Common;

public sealed class OperationResult<T>
{
    private OperationResult(bool isSuccess, T? value, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public T? Value { get; }

    public string? ErrorMessage { get; }

    public static OperationResult<T> Success(T? value)
    {
        return new OperationResult<T>(true, value, null);
    }

    public static OperationResult<T> Failure(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("An error message is required.", nameof(errorMessage));
        }

        return new OperationResult<T>(false, default, errorMessage);
    }
}
