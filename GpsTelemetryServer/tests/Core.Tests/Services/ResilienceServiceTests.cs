using Core.Services;
using Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Core.Tests.Services;

public class ResilienceServiceTests : IDisposable
{
    private readonly Mock<ILogger<ResilienceService>> _mockLogger;
    private readonly Mock<IOptions<ResilienceOptions>> _mockOptions;
    private readonly ResilienceService _resilienceService;

    public ResilienceServiceTests()
    {
        _mockLogger = new Mock<ILogger<ResilienceService>>();
        _mockOptions = new Mock<IOptions<ResilienceOptions>>();
        
        var resilienceOptions = new ResilienceOptions();
        _mockOptions.Setup(x => x.Value).Returns(resilienceOptions);
        
        _resilienceService = new ResilienceService(_mockLogger.Object, _mockOptions.Object);
    }

    [Fact]
    public async Task ExecuteKafkaOperationAsync_SuccessfulOperation_CompletesSuccessfully()
    {
        // Arrange
        var expectedResult = "success";
        var operation = new Func<CancellationToken, ValueTask<string>>(ct => ValueTask.FromResult(expectedResult));

        // Act
        var result = await _resilienceService.ExecuteKafkaOperationAsync(operation);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExecuteKafkaOperationAsync_VoidOperation_CompletesSuccessfully()
    {
        // Arrange
        var executed = false;
        var operation = new Func<CancellationToken, ValueTask>(ct =>
        {
            executed = true;
            return ValueTask.CompletedTask;
        });

        // Act
        await _resilienceService.ExecuteKafkaOperationAsync(operation);

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public async Task ExecuteMessageProcessingAsync_SuccessfulOperation_CompletesSuccessfully()
    {
        // Arrange
        var expectedResult = 42;
        var operation = new Func<CancellationToken, ValueTask<int>>(ct => ValueTask.FromResult(expectedResult));

        // Act
        var result = await _resilienceService.ExecuteMessageProcessingAsync(operation);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExecuteConnectionOperationAsync_SuccessfulOperation_CompletesSuccessfully()
    {
        // Arrange
        var expectedResult = true;
        var operation = new Func<CancellationToken, ValueTask<bool>>(ct => ValueTask.FromResult(expectedResult));

        // Act
        var result = await _resilienceService.ExecuteConnectionOperationAsync(operation);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExecuteKafkaOperationAsync_WithException_RetriesAndThrows()
    {
        // Arrange
        var attemptCount = 0;
        var operation = new Func<CancellationToken, ValueTask<string>>(ct =>
        {
            attemptCount++;
            throw new InvalidOperationException("Test exception");
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _resilienceService.ExecuteKafkaOperationAsync(operation));
        
        Assert.Equal("Test exception", exception.Message);
        Assert.True(attemptCount > 1, "Should have retried at least once");
    }

    [Fact]
    public async Task ExecuteMessageProcessingAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        var operation = new Func<CancellationToken, ValueTask<int>>(ct =>
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(42);
        });

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _resilienceService.ExecuteMessageProcessingAsync(operation, cts.Token));
    }

    public void Dispose()
    {
        _resilienceService?.Dispose();
    }
}