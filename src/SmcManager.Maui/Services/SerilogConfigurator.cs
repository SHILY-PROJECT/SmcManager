using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace SmcManager.Maui.Services;

/// <summary>
/// Настройка Serilog для отладочных сборок (logcat + файл в AppData).
/// </summary>
public static class SerilogConfigurator
{
    public static void Configure(MauiAppBuilder builder, string logsDirectory)
    {
        Directory.CreateDirectory(logsDirectory);

        var logFilePath = Path.Combine(logsDirectory, "smcmanager-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("App", "SmcManager")
            .Enrich.WithProperty("Platform", DeviceInfo.Platform.ToString())
            .Enrich.WithProperty("Version", DeviceInfo.VersionString)
            .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);

        Log.Information(
            "Serilog started. OS={OS}, Model={Model}, Logs={LogPath}",
            DeviceInfo.Current.Platform,
            DeviceInfo.Current.Model,
            logFilePath);
    }
}
