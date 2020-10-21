using System;
using System.Linq;
using HtmlAgilityPack;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace manganelo_yolo1
{
    public class Manganelo : IMangaDownloader
    {
        // this class basically parses the response from Manganelo so as to get the title & chapters
        override public async Task download_chapter(int c, string chap_dir)
        {
            List<string> img_urls = new List<string>();

            // get chapter page response <cached version>
            string _document = await Tools.get_response_string_cached(chapters[c], false);
            HtmlNode document = Tools.string_to_html(_document);

            // parse the page to get the urls of each image 
            try
            {
                List<HtmlNode> image_nodes = document.Descendants("img").ToList();
                foreach (var im_node in image_nodes)
                {
                    string img_url = im_node.GetAttributeValue("src", null);
                    if (img_url == null) throw new Exception("null page!");
                    if (img_url == @"https://manganelo.com/themes/hm/images/gohome.png" ||
                        img_url == @"https://manganelo.com/themes/hm/images/logo-chap.png") continue;

                    img_urls.Add(img_url);
                }
            }
            catch (Exception e) { Console.Error.WriteLine(e.StackTrace); throw e; }

            await Tools.download_imgs(img_urls, chap_dir);
        }

        override protected async Task setup(string manga_url)
        {
            // get manga info <cached version>
            string _document = await Tools.get_response_string_cached(manga_url, false);
            HtmlNode document = Tools.string_to_html(_document);

            // parse title
            try
            {
                List<HtmlNode> info_panel = Tools.find_by_tag_class(document, "div", "panel-story-info");
                this.manga_title = info_panel.First().Descendants("h1").First().InnerText;
            }
            catch (Exception e) { Console.WriteLine(e.StackTrace); throw e; }

            // parse chapter list 
            try
            {
                HtmlNode chapters_panel = Tools.find_by_tag_class(document, "div", "panel-story-chapter-list").First();
                List<HtmlNode> chapter_nodes = Tools.find_by_tag_class(chapters_panel, "li", "a-h");
                foreach (HtmlNode chap_node in chapter_nodes)
                {
                    var link = chap_node.Descendants("a").First().GetAttributeValue("href", null);
                    if (link == null) throw new Exception("link not found!");
                    this.chapters.Add(link);
                }
            }
            catch (Exception e) { Console.WriteLine(e.StackTrace); throw e; }
        }
        public Manganelo(string manga_url) : base(manga_url, @"https://manganelo.com/") { }
    }
}