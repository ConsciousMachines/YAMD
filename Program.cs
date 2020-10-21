using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Drawing;
using HtmlAgilityPack;
using System.Net.Http;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Net.Sockets;

// so mangadex has an API that sends JSON objects. Manganelo has to be parsed like a normal HTML site.
// https://www.codeproject.com/Tips/804660/How-to-Parse-HTML-using-Csharp 
// http://zetcode.com/csharp/httpclient/


namespace manganelo_yolo1
{
    abstract public class IMangaDownloader
    {
        abstract public Task download_chapter(int c, string chap_dir);
        abstract protected Task setup(string manga_url);
        public IMangaDownloader(string manga_url, string referer)
        {
            // set the http Referer so images download properly
            Tools.set_referer(referer);

            // get the manga title & chapter urls
            Task.Run(() => this.setup(manga_url)).Wait();

            // since chapters are displayed with most recent first, reverse them
            chapters.Reverse();
        }

        protected string manga_title;
        protected List<string> chapters = new List<string>();
        public string get_manga_title() => Tools._rgx.Replace(manga_title, "_").Substring(0, Math.Min(20, manga_title.Length));
        public List<string> get_chapters() => chapters;
    }

    public static class Tools
    {
        public static Regex _rgx = new Regex("[^a-zA-Z0-9]");
        private static Random r = new Random();
        private static HttpClient http = new HttpClient();
        private static void play_sound(string file) => Process.Start(@"powershell", $@"-c (New-Object Media.SoundPlayer '{file}').PlaySync();");
        public static void set_referer(string referer) => http.DefaultRequestHeaders.Add("Referer", referer);
        static Tools() // static constructor 
        {
            // setup
            string user_agent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:81.0) Gecko/20100101 Firefox/81.0";
            http.DefaultRequestHeaders.Add("User-Agent", user_agent);
        }
        public static async Task download_imgs(List<string> img_urls, string chap_dir)
        {
            // download the images 
            Console.Write("\t\tDownloading page:");
            for (int p = 0; p < img_urls.Count; p++)
            {
                Console.Write($" {p}");
                string img_path = Path.Combine(chap_dir, p.ToString().PadLeft(4, '0') + ".png");
                await Tools.retrieve_image(img_urls[p], img_path);
            }
        }
        public static async Task retrieve_image(string img_url, string img_path)
        {
            for (int tries = 0; ; tries++)
            {
                //Console.WriteLine($"        Try {tries}");
                try
                {
                    HttpResponseMessage _response = await Tools.http.GetAsync(img_url);
                    _response.EnsureSuccessStatusCode();

                    byte[] image = await _response.Content.ReadAsByteArrayAsync();
                    MemoryStream ms = new MemoryStream(image);
                    Image i = Image.FromStream(ms);

                    i.Save(img_path, ImageFormat.Png);
                    break;
                }
                catch (Exception e) // should prob use HttpException or WebException but w/e
                {
                    System.Console.Error.WriteLine($"[Manga] ERROR: {e}");
                    if (tries > 10) throw e;
                    System.Console.WriteLine($"WebException, retry {tries + 1} in 1 second");
                    System.Threading.Thread.Sleep(1000);
                }
            }
        }
        public static void Wait() // just pause the thread for 10-20 secs
        {
            int rand = Tools.r.Next(10_000, 20_000);
            Console.WriteLine($"\nWaiting {rand / 1000.0f} seconds\n");
            System.Threading.Thread.Sleep(rand);
        }
        public static void Cleanup() // delete cached web pages stored as txt files 
        {
            // delete the cache 
            foreach (var file in Directory.GetFiles(Directory.GetCurrentDirectory()))
            {
                if (Path.GetExtension(file) == ".txt") File.Delete(file);
            }
            play_sound(@"C:\dev\bruh.wav");
        }
        public static void make_dir(string dir_path) // create a directory if it doesnt already exist 
        {
            if (!Directory.Exists(dir_path)) Directory.CreateDirectory(dir_path);
        }

        public static List<HtmlNode> find_by_tag_class(HtmlNode document, string tag_name, string class_name)
        {
            return document.Descendants(tag_name).Where // basically parsing the HTML string. 
                    (x => x.Attributes["class"] != null &&
                       x.Attributes["class"].Value.Contains(class_name)).ToList();
        }
        public static HtmlNode string_to_html(string source)
        {
            // read the response using Html Agility Pack
            HtmlDocument _html_document = new HtmlDocument();
            _html_document.LoadHtml(source);
            HtmlNode document = _html_document.DocumentNode;
            return document;
        }
        public static async Task<string> get_response_string_cached(string url, bool expect_json)
        {
            // here we get a response from a URL. We also cache the response string as a text file for re-use.
            string cache_path = _rgx.Replace(url, "_") + ".txt";
            string source;

            // if the page is cached, use it
            if (File.Exists(cache_path))
            {
                Console.WriteLine($"Cached version of {url} located, using that.");
                source = File.ReadAllText(cache_path);
            }
            else
            {
                // get the web page response text 
                HttpResponseMessage _response;// = await http.GetAsync(url);
                int tries = 0;
                while (true)
                {
                    try
                    {
                        _response = await http.GetAsync(url);
                        break;
                    }
                    catch (HttpRequestException e)
                    {
                        tries++;
                        if ((e.InnerException as SocketException).SocketErrorCode == SocketError.TryAgain)
                        {
                            System.Console.WriteLine($"Try {tries}, Socket error, retrying in 1 second");
                            System.Threading.Thread.Sleep(1000);
                        }
                        else { Console.WriteLine(e.StackTrace); throw e; }
                    }
                    catch (Exception e) { Console.WriteLine(e.StackTrace); throw e; }
                }

                _response.EnsureSuccessStatusCode();

                // if all is well, cache the thing in case im trying to access the website 10000000 times 
                if (_response.StatusCode == HttpStatusCode.OK)
                {
                    // in case of JSON, we don't do fancy HTML decoding, just read the result as string
                    if (expect_json)
                    {
                        source = await _response.Content.ReadAsStringAsync(); // no idea how this differs from my decoding.
                    }
                    // otherwise, do a little HTML decoding
                    else
                    {
                        byte[] response = await _response.Content.ReadAsByteArrayAsync();
                        source = Encoding.GetEncoding("utf-8").GetString(response, 0, response.Length - 1);
                        source = WebUtility.HtmlDecode(source);
                    }
                    File.WriteAllText(cache_path, source);
                }
                else throw new Exception("failed to get response, and no cache either!");
            }
            return source;
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            string save_dir = @"C:\Users\pwnag\Desktop\dex";
            string manga_url;
            IMangaDownloader crawler;

            // prompt user for manga url 
            while (true)
            {
                Console.WriteLine("Enter a MangaDex or Manganelo url:");
                string input_str = Console.ReadLine();
                
                //string input_str = @"https://manganelo.com/manga/ri924485";

                try
                {
                    if (input_str.Contains("mangadex"))
                    {
                        uint code = uint.Parse(input_str.Split('/')[4]);
                        manga_url = $"https://mangadex.org/api/manga/{code}";
                        crawler = new MangaDex(manga_url);
                        break;
                    }
                    else if (input_str.Contains("manganelo"))
                    {
                        if (!(input_str.Substring(0, 28) == @"https://manganelo.com/manga/")) throw new Exception("manganelo link kinda bad");
                        manga_url = input_str;
                        crawler = new Manganelo(manga_url);
                        break;
                    }
                }
                catch (Exception e) { Console.Error.WriteLine(e.StackTrace); Console.WriteLine("Failed to parse, try again!"); }
            }

            string manga_title = crawler.get_manga_title();
            List<string> chapters = crawler.get_chapters();
            Console.WriteLine($"\nManga Title: {manga_title}\n");


            // select which chapter to start with 
            for (int i = 0; i < chapters.Count; i++) Console.WriteLine($"\t[ {i} ]\t{chapters[i]}");
            Console.WriteLine("\nPlease select which chapter to start with:");
            int idx;
            while(true)
            {
                try
                {
                    idx = int.Parse(Console.ReadLine());
                    if (idx < 0 || idx >= chapters.Count) throw new Exception("chapter is out of range bruv");
                    break;
                }
                catch { Console.WriteLine("invalid number, learn to type dumbass!"); }
            }
            Console.WriteLine($"\nStarting with chapter {idx} : {chapters[idx]}");


            // create directory for the manga 
            string manga_dir = Path.Combine(save_dir, manga_title);
            Tools.make_dir(manga_dir);
            Tools.make_dir(save_dir);


            // download each chapter 
            for (int c = idx; c < chapters.Count; c++)
            {
                Console.WriteLine($"\tStarting chapter {c}");

                // make chapter's directory
                string chapter_dir = Path.Combine(manga_dir, c.ToString().PadLeft(4, '0'));
                Tools.make_dir(chapter_dir);

                // download the images to the chapter's directory
                await crawler.download_chapter(c, chapter_dir);

                // wait a bit 
                Tools.Wait();
            }
            Tools.Cleanup();
            
            Console.WriteLine($"\nDone downloading {manga_title}");
            Console.ReadKey();
        }
    }
}
