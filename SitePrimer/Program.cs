using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Diagnostics;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace SitePrimer
{
    class Program
    {
        static bool _writeToEventLog;

        static void Main(string[] args)
        {
            try
            {
                Run(args);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.ToString());
            }
        }

        private static void Run(string[] args)
        {
            AggregateNumberedPaths = true;
            PatternsFound = new string[] { "/" }.ToList();

            if (args.Length > 0)
            {
                RootUrl = args[0];
            }
            else
            {
                Console.WriteLine("Usage: SitePrimer.exe [site] <options>");
                Console.WriteLine("       --nocrawl               Don't crawl the site, just get the root page");
                Console.WriteLine("       --eventlog              Write messages to the system event log");
                return;
            }

            bool crawlLinks = !args.Contains("--nocrawl");
            _writeToEventLog = args.Contains("--eventlog");

            //Queue<string> 

            Queue<string> urlsToScan = new Queue<string>();
            urlsToScan.Enqueue("/");


            using (WebClient client = new WebClient() { Proxy = null, UseDefaultCredentials = true })
            {
                while (urlsToScan.Count > 0)
                {
                    string url = urlsToScan.Dequeue();
                    string fullUrl = RootUrl + url;


                    try
                    {
                        var sw = Stopwatch.StartNew();
                        var html = client.DownloadString(fullUrl);
                        sw.Stop();

                        string displayUrl = fullUrl;
                        if (displayUrl.Length > 79)
                            displayUrl = fullUrl.Substring(0, 76) + "...";

                        WriteMessage(displayUrl.PadRight(80) + sw.Elapsed);

                        if (crawlLinks)
                        {
                            foreach (string link in GetUrls(html).Where(link => link.StartsWith("/") && NotFoundYet(link)))
                            {
                                MarkFound(link);
                                urlsToScan.Enqueue(link);
                            }
                        }
                    }
                    catch (WebException e)
                    {
                        WriteMessage(fullUrl.PadRight(80) + ((HttpWebResponse)e.Response).StatusCode);
                    }

                }

            }
        }

        public static void WriteMessage(string message)
        {
            Console.WriteLine(message);
            if (_writeToEventLog)
                WriteToEventLog(message);
        }

        private static void WriteToEventLog(string message)
        {
            string source = "SitePrimer";
            EventLog elog = new EventLog();
            if (!EventLog.SourceExists(source))
            {
                EventLog.CreateEventSource(source, "Application");
            }
            elog.Source = source;
            elog.EnableRaisingEvents = true;
            elog.WriteEntry(message);
        }

        static bool AggregateNumberedPaths { get; set; }
        static List<string> PatternsFound { get; set; }
        static string RootUrl { get; set; }

        static void MarkFound(string link)
        {
            link = AggregateNumberedPaths ? ConvertLinkToAggregate(link) : link;
            PatternsFound.Add(link);

        }

        /// <summary>
        /// Returns true if link is new
        /// </summary>
        /// <param name="link"></param>
        /// <returns></returns>
        static bool NotFoundYet(string link)
        {
            link = AggregateNumberedPaths ? ConvertLinkToAggregate(link) : link;

            return ! PatternsFound.Contains(link);
        }

        /// <summary>
        /// Converts numeric paths to generic paths
        /// ie /Details/123 to /Details/{id}
        /// </summary>
        /// <param name="link"></param>
        /// <returns></returns>
        static string ConvertLinkToAggregate(string link)
        {
            //if (link.Contains("typeid"))
            //{
            //    link = Regex.Replace(link, "asdf", "asdf");
            //}
            return Regex.Replace(link, @"\d+", @"{id}");
        }



        static string [] GetUrls(string src)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(src);

            var nodes = doc.DocumentNode.SelectNodes("//a[@href]");

            if (nodes == null)
                return new string[0];

            return
                nodes
                .Select(link => link.Attributes["href"].Value)
                .Select(s => s.Replace("&amp;", "&"))
                .ToArray();

        }
    }
}
