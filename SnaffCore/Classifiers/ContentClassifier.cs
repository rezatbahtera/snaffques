using SnaffCore.Concurrency;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using static SnaffCore.Config.Options;

#if ULTRASNAFFLER
using Toxy;
#endif

namespace SnaffCore.Classifiers
{
    public class ContentClassifier
    {
        private ClassifierRule ClassifierRule { get; set; }

        public ContentClassifier(ClassifierRule inRule)
        {
            this.ClassifierRule = inRule;
        }

        public void ClassifyContent(FileInfo fileInfo)
        {
            BlockingMq Mq = BlockingMq.GetMq();
            FileResult fileResult;
            try
            {
                if (MyOptions.MaxSizeToGrep >= fileInfo.Length)
                {
                    switch (ClassifierRule.MatchLocation)
                    {
                        case MatchLoc.FileContentAsBytes:
                            byte[] fileBytes = File.ReadAllBytes(fileInfo.FullName);
                            if (ByteMatch(fileBytes))
                            {
                                fileResult = new FileResult(fileInfo)
                                {
                                    MatchedRule = ClassifierRule
                                };
                                if (!fileResult.RwStatus.CanRead && !fileResult.RwStatus.CanModify && !fileResult.RwStatus.CanWrite) { return; }
                                ;
                                Mq.FileResult(fileResult);
                            }
                            return;

                        case MatchLoc.FileContentAsString:
                            if (!MyOptions.PlainTextExtensions.Contains(fileInfo.Extension, StringComparer.OrdinalIgnoreCase))
                            {
                                return;
                            }

                            TextResult textResult = null;

                            // By wrapping MemoryStream in a 'using' block, we guarantee it gets disposed.
                            using (MemoryStream memStream = new MemoryStream())
                            {
                                int maxRetries = 3;
                                int delay = 1000;
                                bool success = false;

                                for (int i = 0; i < maxRetries; i++)
                                {
                                    try
                                    {
                                        using (FileStream fileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                        {
                                            fileStream.CopyTo(memStream);
                                        }
                                        // If CopyTo succeeds, break the retry loop
                                        success = true;
                                        break;
                                    }
                                    catch (IOException ex) when (ex.Message.Contains("network name is no longer available"))
                                    {
                                        memStream.SetLength(0); // Clear the stream for the next attempt
                                        if (i == maxRetries - 1)
                                        {
                                            // LOG ONLY ON FINAL FAILURE to reduce console bottleneck.
                                            Mq.Trace($"Failed to read {fileInfo.FullName} after {maxRetries} attempts due to network error. Skipping file.");
                                            return; // Exit the method for this file
                                        }
                                        // No per-retry logging.
                                        Thread.Sleep(delay);
                                        delay *= 2;
                                    }
                                    catch (Exception ex)
                                    {
                                        Mq.Error($"An unexpected error occurred while reading {fileInfo.FullName}: {ex.Message}");
                                        return; // Exit the method for this file
                                    }
                                }

                                if (success && memStream.Length > 0)
                                {
                                    memStream.Position = 0; // Rewind the stream to the beginning for reading
                                    using (var reader = new StreamReader(memStream))
                                    {
                                        TextClassifier textClassifier = new TextClassifier(ClassifierRule);
                                        textResult = textClassifier.ScanStream(reader);
                                    }
                                }
                            } // MemoryStream is automatically disposed here.

                            if (textResult != null)
                            {
                                fileResult = new FileResult(fileInfo)
                                {
                                    MatchedRule = ClassifierRule,
                                    TextResult = textResult
                                };
                                if (!fileResult.RwStatus.CanRead && !fileResult.RwStatus.CanModify && !fileResult.RwStatus.CanWrite) { return; }
                                ;
                                Mq.FileResult(fileResult);
                            }
                            return;

                        case MatchLoc.FileLength:
                            bool lengthResult = SizeMatch(fileInfo);
                            if (lengthResult)
                            {
                                fileResult = new FileResult(fileInfo)
                                {
                                    MatchedRule = ClassifierRule
                                };

                                if (!fileResult.RwStatus.CanRead && !fileResult.RwStatus.CanModify && !fileResult.RwStatus.CanWrite) { return; }
                                ;
                                Mq.FileResult(fileResult);
                            }
                            return;
                        case MatchLoc.FileMD5:
                            bool Md5Result = MD5Match(fileInfo);
                            if (Md5Result)
                            {
                                fileResult = new FileResult(fileInfo)
                                {
                                    MatchedRule = ClassifierRule
                                };

                                if (!fileResult.RwStatus.CanRead && !fileResult.RwStatus.CanModify && !fileResult.RwStatus.CanWrite) { return; }
                                ;
                                Mq.FileResult(fileResult);
                            }
                            return;
                        default:
                            Mq.Error("You've got a misconfigured file ClassifierRule named " + ClassifierRule.RuleName + ".");
                            return;
                    }
                }
                else
                {
                    Mq.Trace("The following file was bigger than the MaxSizeToGrep config parameter:" + fileInfo.FullName);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Mq.Error($"Not authorized to access file: {fileInfo.FullName}");
                return;
            }
            catch (IOException e)
            {
                Mq.Trace($"IO Exception on file: {fileInfo.FullName}. {e.Message}");
                return;
            }
            catch (Exception e)
            {
                Mq.Error(e.ToString());
                return;
            }
        }

        public bool SizeMatch(FileInfo fileInfo)
        {
            if (this.ClassifierRule.MatchLength == fileInfo.Length)
            {
                return true;
            }
            return false;
        }

        public bool MD5Match(FileInfo fileInfo)
        {
            string md5Sum = GetMD5HashFromFile(fileInfo.FullName);
            if (md5Sum == this.ClassifierRule.MatchMD5.ToUpper())
            {
                return true;
            }
            return false;
        }

        protected string GetMD5HashFromFile(string fileName)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(fileName))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty);
                }
            }
        }

#if ULTRASNAFFLER
        public string ParseFileToString(FileInfo fileInfo)
        {
            ParserContext context = new ParserContext(fileInfo.FullName);
            ITextParser parser = ParserFactory.CreateText(context);

                string doc = parser.Parse();
            return doc;
        }
#endif

        public bool ByteMatch(byte[] fileBytes)
        {
            // TODO
            throw new NotImplementedException(message: "Haven't implemented byte-based content searching yet lol.");
        }
    }
}

