using SnaffCore.Classifiers;
using SnaffCore.Concurrency;
using SnaffCore.Config;
using SnaffCore.Queue; // Correct reference
using System;
using System.IO;
using static SnaffCore.Config.Options;

namespace SnaffCore.TreeWalk
{
    public class TreeWalker
    {
        private readonly BlockingMq _mq = BlockingMq.GetMq();
        private readonly Options _options;
        private readonly QueueManager _queueManager;

        public TreeWalker(QueueManager queueManager)
        {
            _options = MyOptions;
            _queueManager = queueManager;
        }

        public void WalkTree(string currentDir)
        {
            if (!Directory.Exists(currentDir))
            {
                _mq.Trace($"Directory not found or inaccessible: {currentDir}");
                return;
            }

            try
            {
                foreach (string file in Directory.GetFiles(currentDir))
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(file);

                        if (fileInfo.Length > _options.MaxSizeToEnumerate)
                        {
                            _mq.Trace($"Skipping file {file} - size {fileInfo.Length} exceeds enumeration limit.");
                            continue;
                        }

                        bool passedLightweightFilters = false;
                        foreach (var classifier in MyOptions.FileClassifiers)
                        {
                            if (classifier.MatchLocation == MatchLoc.FileName ||
                                classifier.MatchLocation == MatchLoc.FilePath ||
                                classifier.MatchLocation == MatchLoc.FileExtension)
                            {
                                FileClassifier fileClassifier = new FileClassifier(classifier);
                                if (fileClassifier.ClassifyFile(fileInfo, false))
                                {
                                    passedLightweightFilters = true;
                                    break;
                                }
                            }
                        }

                        if (passedLightweightFilters)
                        {
                            _queueManager.AddFileToQueue(fileInfo.FullName, fileInfo.Length);
                        }
                    }
                    catch (FileNotFoundException) { }
                    catch (UnauthorizedAccessException) { }
                }

                foreach (string dirStr in Directory.GetDirectories(currentDir))
                {
                    SnaffCore.SnaffCon.GetTreeTaskScheduler().New(() => WalkTree(dirStr));
                }
            }
            catch (Exception e)
            {
                _mq.Trace($"Error walking directory {currentDir}: {e.Message}");
            }
        }
    }
}

