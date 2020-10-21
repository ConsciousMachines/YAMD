using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace manganelo_yolo1
{
    class MangaDex : IMangaDownloader
    {
        // this class basically parses the response from Mangadex so as to get the title & chapters
        override public async Task download_chapter(int c, string chap_dir)
        {
            List<string> img_urls = new List<string>();

            // get the chapter json
            string json = await Tools.get_response_string_cached(chapters[c], true);

            // parse the chapter json
            try
            {
                JObject _obj = JObject.Parse(json);
                JArray _pages = _obj["page_array"] as JArray;

                for (int i = 0; i < _pages.Count; i++)
                {
                    var img_url = _obj["server"].ToObject<string>() + _obj["hash"].ToObject<string>() + "/" + _pages[i].ToObject<string>();
                    img_urls.Add(img_url);
                }
            }
            catch (FormatException e) { Console.Error.WriteLine(e.ToString()); throw e; }
            catch (Exception e) { Console.Error.WriteLine(e.StackTrace); throw e; }

            await Tools.download_imgs(img_urls, chap_dir);
        }
        override protected async Task setup(string manga_url)
        {
            // get the json from the website 
            string json = await Tools.get_response_string_cached(manga_url, true);

            // if that worked, parse result
            JObject __obj = JObject.Parse(json);
            JObject _mangaobj = __obj["manga"] as JObject;
            JObject _chapters = __obj["chapter"] as JObject;

            // parse title 
            this.manga_title = _mangaobj["title"].ToObject<string>();

            // parse chapter list 
            foreach (var chap in _chapters.Properties())
            {
                if ((chap.Value as JObject)["lang_code"].ToObject<string>() == "gb") // wtf is this syntax :P
                {
                    chapters.Add(@"https://mangadex.org/api/chapter/" + chap.Name);
                }
            }
        }
        public MangaDex(string manga_url) : base(manga_url, @"https://mangadex.org/") { }
    }
}
