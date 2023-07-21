// See https://aka.ms/new-console-template for more information
using CompressBackup;
using Microsoft.Extensions.Configuration;
using Serilog;

Console.WriteLine("Hello, World!");

var logConfig = new LoggerConfiguration();

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .Build();

string LogKey = "log-path";
string SerilogSelfLog = "SerilogSelfLog";
Console.WriteLine(config[LogKey]);
var logfolder = string.IsNullOrEmpty(config[LogKey]) ? Path.Combine(Environment.CurrentDirectory, "logs") : config[LogKey]!.ToString();
bool enableSelfLog = config[SerilogSelfLog] == null ? false : bool.Parse(config[SerilogSelfLog]!);

if (enableSelfLog)
{
    // Create a TextWriter that writes to a file
    var selflog = Path.Combine(logfolder, "Serilog-SelfLog.log");
    var selflogFile = new FileInfo(selflog);
    selflogFile.Directory?.Create();
    var file = File.CreateText(selflog);

    // Enable SelfLog to write to the file
    Serilog.Debugging.SelfLog.Enable(TextWriter.Synchronized(file));
}

Log.Logger = logConfig
    .ReadFrom.Configuration(config)
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(logfolder, "BackupLog-.log"), rollingInterval: RollingInterval.Day)
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithMemoryUsage()
    //.Enrich.FromLogContext()
    .CreateLogger();
Log.Information("Start Logging");
BackupLogProc backupLog = new(config);
backupLog.DoBackup();
Log.Information("Stop Logging");
await Log.CloseAndFlushAsync();
