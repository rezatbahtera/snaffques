using SnaffCore.ActiveDirectory;
using SnaffCore.Classifiers;
using SnaffCore.Concurrency;
using SnaffCore.Config;
using SnaffCore.Queue; // Correct reference
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Management;
using System.Threading;
using static SnaffCore.Config.Options;

namespace SnaffCore.ShareFind
{
    public class ShareFinder
    {
        private readonly BlockingMq _mq = BlockingMq.GetMq();
        private readonly Options _options;
        private readonly QueueManager _queueManager;

        public ShareFinder(QueueManager queueManager)
        {
            _options = MyOptions;
            _queueManager = queueManager;
        }

        public void FindShares()
        {
            List<string> computers;
            if (_options.ComputerTargets != null && _options.ComputerTargets.Length > 0)
            {
                computers = new List<string>(_options.ComputerTargets);
            }
            else if (_options.PathTargets.Count > 0)
            {
                return;
            }
            else
            {
                _mq.Info("Getting computers from AD.");
                DirectorySearcher searcher = new DirectorySearcher();
                computers = searcher.GetComputers(_options.ComputerTargetsLdapFilter);
                _mq.Info($"Got {computers.Count} computers from AD.");
            }

            foreach (string computer in computers)
            {
                if (_options.ComputerExclusions.Contains(computer, System.StringComparer.CurrentCultureIgnoreCase))
                {
                    _mq.Trace($"Skipping {computer} because it's in the exclusion list.");
                    continue;
                }

                SnaffCore.SnaffCon.GetShareTaskScheduler().New(() =>
                {
                    GetShares(computer);
                });

                if (_options.ShareFinderThrottleMs > 0)
                {
                    Thread.Sleep(_options.ShareFinderThrottleMs);
                }
            }
        }

        private void GetShares(string computer)
        {
            try
            {
                var scope = new ManagementScope($"\\\\{computer}\\root\\cimv2");
                var connectOptions = new ConnectionOptions { Timeout = new System.TimeSpan(0, 0, _options.TimeOut) };
                scope.Options = connectOptions;
                scope.Connect();

                var query = new ObjectQuery("SELECT * FROM Win32_Share");
                var searcher = new ManagementObjectSearcher(scope, query);

                foreach (ManagementObject share in searcher.Get())
                {
                    string sharePath = share["Path"]?.ToString();
                    string shareName = share["Name"]?.ToString();
                    string uncPath = $"\\\\{computer}\\{shareName}";
                    string shareComment = share["Description"]?.ToString();

                    if (string.IsNullOrEmpty(sharePath) || sharePath.Contains(':') == false)
                    {
                        _mq.Trace($"Skipping non-filesystem share {uncPath}");
                        continue;
                    }

                    ShareResult shareResult = new ShareResult(uncPath, shareComment);
                    if (shareResult.CheckRules())
                    {
                        _mq.ShareResult(shareResult);
                        if (shareResult.Triage >= _options.InterestLevel)
                        {
                            _queueManager.AddShareToQueue(uncPath);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                _mq.Trace($"Couldn't get shares from {computer}: {e.Message}");
            }
        }
    }
}

