using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UiPath.CodedWorkflows;

namespace HttpClientLogging
{
    public partial class CodedWorkflow : CodedWorkflowBase
    {
        // The Property that exposes the modified client in coded workflows
        public HttpClient CustomHttpClient { get => serviceContainer.Resolve<HttpClient>(); }

        protected override void RegisterServices(ICodedWorkflowsServiceLocator serviceLocator)
        {
            //Initialize the custom logger and add it to the HttpClient
            var logger = new RobotLogger(LogWrapper);

            var httpClientHandler = new HttpClientHandler();
            var loggingHandler = new LoggingHandler(httpClientHandler, logger);
            var client = new HttpClient(loggingHandler);

            //Register the service
            serviceLocator.RegisterInstance<HttpClient>(client);
        }
        
        //Wrapper method to bypass Log default arguments
        private void LogWrapper(string message, UiPath.CodedWorkflows.LogLevel level)
        {
            Log(message, level);
        }
    }

    /// <summary>
    /// Handler used to log additional details by the client
    /// 
    /// https://learn.microsoft.com/en-us/dotnet/api/system.net.http.delegatinghandler?view=net-9.0
    /// </summary>
    public class LoggingHandler : DelegatingHandler
    {
        private readonly ILogger _logger;

        public LoggingHandler(HttpMessageHandler innerHandler, ILogger logger)
            : base(innerHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        //Wrapper for the http call that adds additional logs like duration, exception details & status code
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Sending request: {Method} {RequestUri}", request.Method, request.RequestUri);
            
            HttpResponseMessage response = null;
            
            try
            {
                response = await base.SendAsync(request, cancellationToken);
            }
            catch(Exception ex)
            {
                _logger.LogError(
                    "Failed http call: {StatusCode}. Duration: {Milliseconds}. ExceptionMessage: {ExceptionMessage}", 
                    response?.StatusCode, 
                    (DateTime.UtcNow - startTime).Milliseconds,
                    ex.Message
                );
                throw;
            }
            
            _logger.LogInformation(
                "Received response: {StatusCode}. Duration: {Milliseconds}", 
                response.StatusCode, 
                (DateTime.UtcNow - startTime).Milliseconds
            );
            
            return response;
        }
    }

    /// <summary>
    /// Custom ILogger implementation that forwards the calls to UiPath CodedWorkflow Log method
    /// </summary>
    public class RobotLogger : ILogger
    {
        Action<string, UiPath.CodedWorkflows.LogLevel> _uipathLogger;
        
        public RobotLogger(Action<string, UiPath.CodedWorkflows.LogLevel> uiPathLogger)
        {
            _uipathLogger = uiPathLogger;
        }
        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            throw new NotImplementedException();
        }

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var msg = formatter(state, exception);
            _uipathLogger(msg, ToUiPathLogLevel(logLevel));
        }
        
        private UiPath.CodedWorkflows.LogLevel ToUiPathLogLevel(Microsoft.Extensions.Logging.LogLevel level)
        {
            return level switch
            {
                Microsoft.Extensions.Logging.LogLevel.Trace => UiPath.CodedWorkflows.LogLevel.Trace,
                Microsoft.Extensions.Logging.LogLevel.Information => UiPath.CodedWorkflows.LogLevel.Info,
                Microsoft.Extensions.Logging.LogLevel.Warning => UiPath.CodedWorkflows.LogLevel.Warn,
                Microsoft.Extensions.Logging.LogLevel.Error => UiPath.CodedWorkflows.LogLevel.Error,
                Microsoft.Extensions.Logging.LogLevel.Critical => UiPath.CodedWorkflows.LogLevel.Fatal,
                _ => throw new NotImplementedException()
            };
        }
    }
}
