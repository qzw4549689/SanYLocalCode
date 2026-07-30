using Microsoft.Extensions.Logging;

namespace Peter.ServiceJobs.CreditRecordExpiration
{
    /// <summary>
    /// 空日志实现，用于未传入 ILogger 时的默认行为。
    /// </summary>
    public class NullCreditRecordExpirationLogger : ILogger<CreditRecordExpirationService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // 空实现
        }
    }
}
