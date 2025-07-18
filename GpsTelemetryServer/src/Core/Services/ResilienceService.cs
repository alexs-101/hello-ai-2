using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Core.Configuration;

namespace Core.Services;

public class ResilienceService : IDisposable
{
    private readonly ILogger<ResilienceService> _logger;
    private readonly ResilienceOptions _options;
    private readonly ResiliencePipeline _kafkaPublishPipeline;
    private readonly ResiliencePipeline _messageProcessingPipeline;
    private readonly ResiliencePipeline _connectionPipeline;
    
    public ResilienceService(ILogger<ResilienceService> logger, IOptions<ResilienceOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        // Kafka publish resilience pipeline
        _kafkaPublishPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _options.Kafka.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(_options.Kafka.BaseDelayMs),
                MaxDelay = TimeSpan.FromSeconds(_options.Kafka.MaxDelaySeconds),
                OnRetry = args =>
                {
                    _logger.LogWarning("Kafka publish retry attempt {AttemptNumber} for operation. Exception: {Exception}",
                        args.AttemptNumber, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = _options.Kafka.CircuitBreakerFailureRatio,
                SamplingDuration = TimeSpan.FromSeconds(_options.Kafka.CircuitBreakerSamplingDurationSeconds),
                MinimumThroughput = _options.Kafka.CircuitBreakerMinimumThroughput,
                BreakDuration = TimeSpan.FromSeconds(_options.Kafka.CircuitBreakerBreakDurationSeconds),
                OnOpened = args =>
                {
                    _logger.LogError("Kafka circuit breaker opened due to: {Exception}", args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Kafka circuit breaker closed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Kafka circuit breaker half-opened");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(_options.Kafka.TimeoutSeconds))
            .Build();
        
        // Message processing resilience pipeline
        _messageProcessingPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _options.MessageProcessing.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Linear,
                Delay = TimeSpan.FromMilliseconds(_options.MessageProcessing.BaseDelayMs),
                OnRetry = args =>
                {
                    _logger.LogWarning("Message processing retry attempt {AttemptNumber}. Exception: {Exception}",
                        args.AttemptNumber, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(_options.MessageProcessing.TimeoutSeconds))
            .Build();
        
        // Connection resilience pipeline
        _connectionPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _options.Connection.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(_options.Connection.BaseDelayMs),
                MaxDelay = TimeSpan.FromSeconds(_options.Connection.MaxDelaySeconds),
                OnRetry = args =>
                {
                    _logger.LogWarning("Connection retry attempt {AttemptNumber}. Exception: {Exception}",
                        args.AttemptNumber, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = _options.Connection.CircuitBreakerFailureRatio,
                SamplingDuration = TimeSpan.FromSeconds(_options.Connection.CircuitBreakerSamplingDurationSeconds),
                MinimumThroughput = _options.Connection.CircuitBreakerMinimumThroughput,
                BreakDuration = TimeSpan.FromSeconds(_options.Connection.CircuitBreakerBreakDurationSeconds),
                OnOpened = args =>
                {
                    _logger.LogError("Connection circuit breaker opened due to: {Exception}", args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Connection circuit breaker closed");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
        
        _logger.LogInformation("Resilience service initialized with Kafka, MessageProcessing, and Connection pipelines");
    }
    
    public async Task<T> ExecuteKafkaOperationAsync<T>(Func<CancellationToken, ValueTask<T>> operation, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _kafkaPublishPipeline.ExecuteAsync(operation, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka operation failed after all retry attempts");
            throw;
        }
    }
    
    public async Task ExecuteKafkaOperationAsync(Func<CancellationToken, ValueTask> operation, CancellationToken cancellationToken = default)
    {
        try
        {
            await _kafkaPublishPipeline.ExecuteAsync(operation, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka operation failed after all retry attempts");
            throw;
        }
    }
    
    public async Task<T> ExecuteMessageProcessingAsync<T>(Func<CancellationToken, ValueTask<T>> operation, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _messageProcessingPipeline.ExecuteAsync(operation, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message processing operation failed after all retry attempts");
            throw;
        }
    }
    
    public async Task ExecuteMessageProcessingAsync(Func<CancellationToken, ValueTask> operation, CancellationToken cancellationToken = default)
    {
        try
        {
            await _messageProcessingPipeline.ExecuteAsync(operation, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message processing operation failed after all retry attempts");
            throw;
        }
    }
    
    public async Task<T> ExecuteConnectionOperationAsync<T>(Func<CancellationToken, ValueTask<T>> operation, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _connectionPipeline.ExecuteAsync(operation, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection operation failed after all retry attempts");
            throw;
        }
    }
    
    public async Task ExecuteConnectionOperationAsync(Func<CancellationToken, ValueTask> operation, CancellationToken cancellationToken = default)
    {
        try
        {
            await _connectionPipeline.ExecuteAsync(operation, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection operation failed after all retry attempts");
            throw;
        }
    }
    
    public void Dispose()
    {
        // ResiliencePipeline doesn't implement IDisposable in Polly v8
        _logger.LogInformation("Resilience service disposed");
    }
}