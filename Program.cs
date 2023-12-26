using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        Console.Title = "Proxy Scraper";
        Console.WindowHeight = 30;
        Console.WindowWidth = 120;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        string sourcesFilePath = "sources.txt";
        string proxiesFilePath = "proxies.txt";
        string[] sourceUrls = [];

        try
        {
            sourceUrls = File.ReadAllLines(sourcesFilePath)
                        .Select(url => url.Trim())
                        .Where(url => !string.IsNullOrWhiteSpace(url))
                        .ToArray();
        }
        catch (Exception)
        {
            if (!File.Exists(sourcesFilePath))
            {
                File.Create(sourcesFilePath).Close();
                exit("Sources file has been created, add sources and open program again");
            }
            exit("Error while reading sources.txt file");
        }

        if (sourceUrls == null || sourceUrls.Length == 0)
        {
            exit("No sources found in sources.txt");
        }

        foreach (string url in sourceUrls)
        {
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                exit($"Invalid source: {url}");
            }
        }

        List<Task<List<string>>> tasks = new List<Task<List<string>>>();

        foreach (string url in sourceUrls)
        {
            tasks.Add(ScrapeProxiesAsync(url));
        }

        List<string> proxies = new List<string>();

        int index = 0;
        while (tasks.Count > 0)
        {
            Task<List<string>>[] taskArray = tasks.ToArray();
            Task<List<string>> finishedTask = await Task.WhenAny(taskArray);
            tasks.Remove(finishedTask);
            try
            {
                List<string> extractedProxies = await finishedTask;
                int p = extractedProxies.Count;

                if (extractedProxies != null && p > 0 && extractedProxies[0] == "error")
                {
                    continue;
                }

                proxies.AddRange(extractedProxies);
                lock (Console.Out)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    if (extractedProxies.Count == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                    }
                    Console.WriteLine($"[+] {p} proxies scraped from {sourceUrls[index]}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[-] Error processing {sourceUrls[index]} {ex}");
                Console.ResetColor();
            }
            finally
            {
                index++;
            }
        }

        string[] filteredProxies = RemoveDuplicates(proxies.ToArray());
        File.WriteAllLines(proxiesFilePath, filteredProxies);

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n\n   ╔══════════════════════════════════════════════════════════╗");
        Console.Write("   ║");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("                     Proxy Scraper                     ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("   ║");
        Console.WriteLine("   ╚══════════════════════════════════════════════════════════╝");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"        Scraped Proxies: {filteredProxies.Length} ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write($"|");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($" Sources: {sourceUrls.Length} ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write($"|");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" Time: {stopwatch.Elapsed.TotalSeconds:F2}s\n\n");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("Press any key to close");
        Console.ResetColor();
        Console.ReadKey();
    }

    static async Task<List<string>> ScrapeProxiesAsync(string url)
    {
        HttpClient httpClient = new HttpClient();

        while (true)
        {
            HttpResponseMessage response = await httpClient.GetAsync(url);
            int rl = 0;

            if (response.IsSuccessStatusCode)
            {
                string htmlCode = await response.Content.ReadAsStringAsync();
                return ExtractProxies(htmlCode);
            }
            else if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (rl == 7)
                {
                    return ["error"];
                }
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[!] Rate limit reached. Waiting for 2 seconds before retrying...");
                Console.ResetColor();
                rl++;
                await Task.Delay(2000);
            }
            else
            {
                lock (Console.Out)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[-] Error {((int)response.StatusCode)}: {url}");
                    Console.ResetColor();
                    return ["error"];
                }
            }
        }
    }

    static List<string> ExtractProxies(string html)
    {
        List<string> proxies = new List<string>();

        Regex regex = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}:\d+\b");

        MatchCollection matches = regex.Matches(html);

        foreach (Match match in matches)
        {
            proxies.Add(match.Value);
        }

        return proxies;
    }

    static string[] RemoveDuplicates(string[] proxies)
    {
        var set = new HashSet<string>(proxies);
        return set.ToArray();
    }


    static void exit(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
        Console.ReadKey();
        Environment.Exit(0);
    }
}