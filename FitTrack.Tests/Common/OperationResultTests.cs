using System;
using FitTrack.Common;
using FitTrack.Models;
using Xunit;

namespace FitTrack.Tests.Common;

public sealed class OperationResultTests
{
    [Fact]
    public void Success_AllowsANullReferenceValue()
    {
        var result = OperationResult<FitnessGoal?>.Success(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Success_PreservesNonNullValues()
    {
        var result = OperationResult<string>.Success("saved");

        Assert.True(result.IsSuccess);
        Assert.Equal("saved", result.Value);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void FailurePreservesTheMessageAndDefaultValue()
    {
        var result = OperationResult<string>.Failure("The operation failed.");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal("The operation failed.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void FailureRejectsBlankMessages(string message)
    {
        Assert.Throws<ArgumentException>(() => OperationResult<string>.Failure(message));
    }
}
