using SnaffCore.ActiveDirectory;
using SnaffCore.Classifiers;
using SnaffCore.Concurrency;
using SnaffCore.Config;
using SnaffCore.FileScan;
using SnaffCore.ShareFind;
using SnaffCore.TreeWalk;
using SnaffCore.Queue;
using System;
using System.Linq;
using System.Threading;

namespace SnaffCore
{
    public class SnaffCon
    {
        private Options Options { get; set; }
        private BlockingMq Mq { get; set; }
        public static BlockingStaticTaskScheduler ShareTaskScheduler { get; set; }
        public static BlockingStaticTaskScheduler TreeTaskScheduler { get; set; }
        public static BlockingStaticTaskScheduler FileTaskScheduler { get; set; }
        public static FileScanner FileScanner { get; set; }
        public static AdData AdData { get; set; }

        public SnaffCon(Options options)
        {
            Options = options;
            Mq = BlockingMq.GetMq();

            Options.PrepareClassifiers();

            ShareTaskScheduler = new BlockingStaticTaskScheduler(Options.ShareThreads, Options.MaxShareQueue);
            TreeTaskScheduler = new BlockingStaticTaskScheduler(Options.TreeThreads, Options.MaxTreeQueue);
            FileTaskScheduler = new BlockingStaticTaskScheduler(Options.FileThreads, Options.MaxFileQueue);
            FileScanner = new FileScanner();
            AdData = new AdData(); // Corrected constructor call
        }

        public void Execute()
        {
            try
            {
                Mq.Info("Snaffler starting in " + Options.Mode.ToString() + " mode.");

                switch (Options.Mode)
                {
                    case SnafflerMode.DiscoverOnly:
                        Discover();
                        break;
                    case SnafflerMode.EnumerateOnly:
                        Enumerate();
                        break;
                    case SnafflerMode.ScanOnly:
                        Scan();
                        break;
                    case SnafflerMode.Full:
                        Discover(true);
                        break;
                }

                Mq.Info("All tasks for the current mode have been queued.");
            }
            catch (Exception e)
            {
                Mq.Error(e.ToString());
            }
            finally
            {
                while (ShareTaskScheduler.IsRunning() || TreeTaskScheduler.IsRunning() || FileTaskScheduler.IsRunning())
                {
                    Thread.Sleep(1000);
                    ReportStats();
                }

                Mq.Info("All tasks have completed.");
                ReportStats();
                Mq.Finish();
            }
        }

        private void Discover(bool chain = false)
        {
            using (var queueManager = new QueueManager(Options.QueueDbPath))
            {
                ShareFinder shareFinder = new ShareFinder(queueManager);
                shareFinder.FindShares();

                while (ShareTaskScheduler.IsRunning()) { Thread.Sleep(1000); }
                Mq.Info($"Discovery phase complete. Found {queueManager.GetShareQueueCount()} shares.");

                if (chain)
                {
                    Enumerate(queueManager, chain);
                }
            }
        }

        private void Enumerate(QueueManager queueManager = null, bool chain = false)
        {
            bool ownQm = queueManager == null;
            if (ownQm)
            {
                queueManager = new QueueManager(Options.QueueDbPath);
            }

            try
            {
                TreeWalker treeWalker = new TreeWalker(queueManager);
                var shares = queueManager.GetSharesToEnumerate().ToList();
                Mq.Info($"Starting enumeration of {shares.Count} shares.");

                foreach (var share in shares)
                {
                    TreeTaskScheduler.New(() => treeWalker.WalkTree(share.SharePath));
                }

                while (TreeTaskScheduler.IsRunning()) { Thread.Sleep(1000); }
                Mq.Info($"Enumeration phase complete. Found {queueManager.GetFileQueueCount()} files to scan.");

                if (chain)
                {
                    Scan(queueManager);
                }
            }
            finally
            {
                if (ownQm) queueManager.Dispose();
            }
        }

        private void Scan(QueueManager queueManager = null)
        {
            bool ownQm = queueManager == null;
            if (ownQm)
            {
                queueManager = new QueueManager(Options.QueueDbPath);
            }
            try
            {
                var filesToScan = queueManager.GetFilesToScan().ToList();
                Mq.Info($"Starting scan of {filesToScan.Count} files.");

                foreach (var fileFinding in filesToScan)
                {
                    FileTaskScheduler.New(() =>
                    {
                        FileScanner.ScanFile(fileFinding.FilePath);
                        queueManager.MarkFileAsScanned(fileFinding.Id);
                    });
                }
            }
            finally
            {
                if (ownQm) queueManager.Dispose();
            }
        }

        private void ReportStats()
        {
            Mq.Info(
                $"ShareFinder Tasks: C:{ShareTaskScheduler.CompletedTaskCount}, Q:{ShareTaskScheduler.WorkQueueCount}, R:{ShareTaskScheduler.RunningTaskCount} | " +
                $"TreeWalker Tasks: C:{TreeTaskScheduler.CompletedTaskCount}, Q:{TreeTaskScheduler.WorkQueueCount}, R:{TreeTaskScheduler.RunningTaskCount} | " +
                $"FileScanner Tasks: C:{FileTaskScheduler.CompletedTaskCount}, Q:{FileTaskScheduler.WorkQueueCount}, R:{FileTaskScheduler.RunningTaskCount}");

            Mq.Info($"{GC.GetTotalMemory(false) / 1024 / 1024}MB RAM in use.");
        }
    }
}

