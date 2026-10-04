using CNC.Core.Common;

namespace CNC.Tests.Core;

public sealed class ResultTests
{
    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_CarriesError()
    {
        var result = Result.Failure("E-Stop active");

        Assert.True(result.IsFailure);
        Assert.Equal("E-Stop active", result.Error);
    }

    [Fact]
    public void Failure_RequiresMessage()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(" "));
    }

    [Fact]
    public void GenericSuccess_ExposesValue()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void GenericFailure_ThrowsOnValueAccess()
    {
        var result = Result<int>.Failure("Parse error");

        var ex = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("Parse error", ex.Message, StringComparison.Ordinal);
    }
}
