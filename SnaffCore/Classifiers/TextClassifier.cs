using SnaffCore.Concurrency;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using static SnaffCore.Config.Options;

namespace SnaffCore.Classifiers
{
    public class TextClassifier
    {
        private ClassifierRule ClassifierRule { get; set; }
        public TextClassifier(ClassifierRule inRule)
        {
            this.ClassifierRule = inRule;
        }

        private BlockingMq Mq { get; set; } = BlockingMq.GetMq();

        // This method takes a StreamReader and scans it line by line.
        public TextResult ScanStream(StreamReader reader)
        {
            string line;
            try
            {
                while ((line = reader.ReadLine()) != null)
                {
                    TextResult result = TextMatch(line);
                    if (result != null)
                    {
                        return result; // Return on the first match found in the file
                    }
                }
            }
            catch (OutOfMemoryException)
            {
                // This can happen with extremely long lines in a file.
                Mq.Error("Out of memory while reading a line from a file. The file may contain very long lines.");
            }
            catch (Exception e)
            {
                Mq.Error("Exception while reading stream in TextClassifier: " + e.Message);
            }
            return null;
        }

        // The original TextMatch is kept for other classifiers and now for line-by-line checks.
        internal TextResult TextMatch(string input)
        {
            foreach (Regex regex in ClassifierRule.Regexes)
            {
                try
                {
                    if (regex.IsMatch(input))
                    {
                        return new TextResult()
                        {
                            MatchedStrings = new List<string>() { regex.ToString() },
                            MatchContext = GetContext(input, regex)
                        };
                    }
                }
                catch (Exception e)
                {
                    Mq.Error(e.ToString());
                }
            }
            return null;
        }

        internal string GetContext(string original, Regex matchRegex)
        {
            try
            {
                int contextBytes = MyOptions.MatchContextBytes;
                if (contextBytes == 0)
                {
                    return "";
                }

                // If the line is shorter than the context window, return the whole line.
                if (original.Length <= (contextBytes * 2))
                {
                    return Regex.Replace(original.Trim(), @"\r\n?|\n", "\\n");
                }

                Match match = matchRegex.Match(original);
                int foundIndex = match.Index;

                int contextStart = SubtractWithFloor(foundIndex, contextBytes, 0);
                int contextLength = Math.Min(original.Length - contextStart, contextBytes * 2);

                string matchContext = original.Substring(contextStart, contextLength);

                return Regex.Replace(matchContext.Trim(), @"\r\n?|\n", "\\n");
            }
            catch (Exception e)
            {
                Mq.Trace(e.ToString());
                return "";
            }
        }

        internal int SubtractWithFloor(int num1, int num2, int floor)
        {
            int result = num1 - num2;
            if (result <= floor) return floor;
            return result;
        }
    }

    public class TextResult
    {
        public List<string> MatchedStrings { get; set; }
        public string MatchContext { get; set; }
    }
}
