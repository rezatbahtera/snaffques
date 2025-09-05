using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using SnaffCore;
using SnaffCore.Concurrency;
using SnaffCore.Config;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Snaffler
{
    public class SnaffleRunner
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private BlockingMq Mq { get; set; }
        private LogLevel LogLevel { get; set; }
        private Options Options { get; set; }

        public void Run(string[] args)
        {
            PrintBanner();
            BlockingMq.MakeMq();
            Mq = BlockingMq.GetMq();

            try
            {
                Options = Config.Parse(args);
                if (Options == null) return;

                SetupLogging();

                var controller = new SnaffCon(Options);
                controller.Execute();

                HandleOutput();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void HandleOutput()
        {
            foreach (SnafflerMessage message in Mq.Q.GetConsumingEnumerable())
            {
                ProcessMessage(message);

                if (message.Type == SnafflerMessageType.Finish)
                {
                    break;
                }
            }
        }

        private void ProcessMessage(SnafflerMessage message)
        {
            string logMessage = $"{DateTime.Now:u} [{message.Type.ToString().ToUpper()}] {message.Message}";

            switch (message.Type)
            {
                case SnafflerMessageType.Trace:
                    Logger.Trace(logMessage);
                    break;
                case SnafflerMessageType.Degub:
                    Logger.Debug(logMessage);
                    break;
                case SnafflerMessageType.Info:
                    Logger.Info(logMessage);
                    break;
                case SnafflerMessageType.ShareResult:
                    Logger.Warn($"{DateTime.Now:u} [SHARE] {message.ShareResult.SharePath} ({message.ShareResult.ShareComment})");
                    break;
                case SnafflerMessageType.FileResult:
                    Logger.Warn($"{DateTime.Now:u} [FILE] {message.FileResult.FileInfo.FullName} matched {message.FileResult.MatchedRule.RuleName}");
                    break;
                case SnafflerMessageType.Error:
                    Logger.Error(logMessage);
                    break;
                case SnafflerMessageType.Fatal:
                    Logger.Fatal(logMessage);
                    break;
                case SnafflerMessageType.Finish:
                    Logger.Info("Snaffler out.");
                    break;
            }
        }

        private void SetupLogging()
        {
            var nlogConfig = new LoggingConfiguration();
            var logconsole = new ColoredConsoleTarget("logconsole")
            {
                Layout = "${message}"
            };

            switch (Options.LogLevelString.ToLower())
            {
                case "trace": LogLevel = LogLevel.Trace; break;
                case "degub": LogLevel = LogLevel.Debug; break;
                case "debug": LogLevel = LogLevel.Debug; break;
                default: LogLevel = LogLevel.Info; break;
            }

            nlogConfig.AddRule(LogLevel, LogLevel.Fatal, logconsole);

            if (Options.LogToFile)
            {
                var logfile = new FileTarget("logfile") { FileName = Options.LogFilePath, Layout = "${message}" };
                nlogConfig.AddRule(LogLevel, LogLevel.Fatal, logfile);
            }
            LogManager.Configuration = nlogConfig;
        }

        public void PrintBanner()
        {
            Console.WriteLine(@" .::::::.:::.    :::.  :::.    .-:::::'.-:::::':::    .,:::::: :::::::..   ");
            Console.WriteLine(@";;;`    ``;;;;,  `;;;  ;;`;;   ;;;'''' ;;;'''' ;;;    ;;;;'''' ;;;;``;;;;  ");
            Console.WriteLine(@"'[==/[[[[, [[[[[. '[[ ,[[ '[[, [[[,,== [[[,,== [[[     [[cccc   [[[,/[[['  ");
            Console.WriteLine(@"  '''    $ $$$ 'Y$c$$c$$$cc$$$c`$$$'`` `$$$'`` $$'     $$""""   $$$$$$c    ");
            Console.WriteLine(@" 88b    dP 888    Y88 888   888,888     888   o88oo,.__888oo,__ 888b '88bo,");
            Console.WriteLine(@"  'YMmMY'  MMM     YM YMM   ''` 'MM,    'MM,  ''''YUMMM''''YUMMMMMMM   'W' ");
            Console.WriteLine(@"                         by l0ss and Sh3r4 - github.com/SnaffCon/Snaffler v2  ");
            Console.WriteLine();
        }
    }
}

