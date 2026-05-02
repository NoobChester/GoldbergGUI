using Microsoft.Extensions.Logging;
using MvvmCross.Platforms.Wpf.Core;
using Serilog;
using Serilog.Extensions.Logging;
using System.IO;

namespace GoldbergGUI.WPF
{
    public class Setup : MvxWpfSetup<Core.App>
    {
        protected override ILoggerProvider CreateLogProvider()
        {
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "goldberg_.log");

            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Logger = serilogLogger;

            return new SerilogLoggerProvider(serilogLogger);
        }

        protected override ILoggerFactory CreateLogFactory()
        {
            return new LoggerFactory();
        }
    }
}
