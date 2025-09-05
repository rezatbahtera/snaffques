using Nett;
using SnaffCore.Config;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Snaffler
{
    public partial class Config
    {
        public static Options Parse(string[] args)
        {
            Options tmpOptions = null;
            // Simplified logic to find config file first
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "-c" || args[i] == "--configfile") && i + 1 < args.Length)
                {
                    if (File.Exists(args[i + 1]))
                    {
                        tmpOptions = Toml.ReadFile<Options>(args[i + 1]);
                    }
                }
            }

            if (tmpOptions == null && File.Exists(".\\snaffler.toml"))
            {
                tmpOptions = Toml.ReadFile<Options>(".\\snaffler.toml");
            }

            if (tmpOptions == null)
            {
                tmpOptions = new Options();
            }

            // Manual parsing of arguments to override config file or defaults
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--discover-only":
                        tmpOptions.Mode = SnafflerMode.DiscoverOnly;
                        break;
                    case "--enumerate-only":
                        tmpOptions.Mode = SnafflerMode.EnumerateOnly;
                        break;
                    case "--scan-only":
                        tmpOptions.Mode = SnafflerMode.ScanOnly;
                        break;
                    case "-s":
                        if (i + 1 < args.Length) tmpOptions.ComputerTargets = args[++i].Split(',');
                        break;
                    case "-a":
                        if (i + 1 < args.Length) tmpOptions.ActiveComputerDays = int.Parse(args[++i]);
                        break;
                        // Add other command-line arguments parsing here
                }
            }


            Options.MyOptions = tmpOptions;

            // Set defaults if not provided
            if (tmpOptions.ShareThreads == 0) tmpOptions.ShareThreads = 20;
            if (tmpOptions.TreeThreads == 0) tmpOptions.TreeThreads = 20;
            if (tmpOptions.FileThreads == 0) tmpOptions.FileThreads = 20;
            if (string.IsNullOrEmpty(Options.MyOptions.RuleDir))
            {
                Options.MyOptions.RuleDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\SnaffRules\\";
            }

            return Options.MyOptions;
        }
    }
}

