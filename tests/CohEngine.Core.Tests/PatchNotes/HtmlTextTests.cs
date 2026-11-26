using System.Net;

using CohEngine.Core.PatchNotes;

namespace CohEngine.Core.Tests.PatchNotes;

// Expected values are what CPython 3.14's html.unescape and urllib.robotparser give for the same input.
public sealed class HtmlTextTests
{
    private const string Agent = "coh-build-companion/0.1 (+tool) coh-engine/1";

    private const string Robots = """
        # comment
        User-agent: GPTBot
        Disallow: /

        User-agent: *
        Allow: /
        Disallow: /search/
        Disallow: /*?sort=
        Allow: /forum/*/page/2/$
        Disallow: /forum/*/page/
        Disallow: /bad$path
        Crawl-delay: 5
        User-agent: coh-build-companion
        Disallow: /private
        User-agent: coh-build-companion
        Allow: /private/ok
        Disallow:
        Sitemap: https://x/s.xml
        """;

    [Theory]
    [InlineData("&amp;", "&")]
    [InlineData("&amp", "&")]
    [InlineData("a&notit;b", "a\u00acit;b")]
    [InlineData("&#65;", "A")]
    [InlineData("&#x41;", "A")]
    [InlineData("&#0;", "\ufffd")]
    [InlineData("&#128;", "\u20ac")]
    [InlineData("&#xD800;", "\ufffd")]
    [InlineData("&#1;", "")]
    [InlineData("&#x110000;", "\ufffd")]
    [InlineData("&#65", "A")]
    [InlineData("&bogus;", "&bogus;")]
    [InlineData("&AMP;", "&")]
    [InlineData("&nbsp;x", "\u00a0x")]
    [InlineData("&apos;", "'")]
    [InlineData("&copy2024", "\u00a92024")]
    [InlineData("&lt&gt", "<>")]
    [InlineData("&#x1FFFE;", "")]
    [InlineData("&#13;", "\r")]
    [InlineData("&#99999999999999999999;", "\ufffd")]
    [InlineData("x&y", "x&y")]
    [InlineData("&#x9F;", "\u0178")]
    [InlineData("plain", "plain")]
    public void Unescape_decodes_as_python_does(string text, string expected)
    {
        Assert.Equal(expected, HtmlText.Unescape(text));
    }

    [Fact]
    public void Strip_breaks_lines_at_block_closers_and_strips_each_line()
    {
        Assert.Equal("a\nb &\nc\n", HtmlText.Strip("<p> a </p><div>b&nbsp;&amp;</div>c\r\n"));
    }

    [Fact]
    public void Page_title_drops_the_section_and_site()
    {
        Assert.Equal("October 20 & more", HtmlText.PageTitle("<TITLE> October 20 &amp; more - Page 4 - Homecoming </TITLE>"));
        Assert.Equal("t", HtmlText.PageTitle("<title>t - s - Invision Community</title>"));
        Assert.Equal(string.Empty, HtmlText.PageTitle("<html></html>"));
        Assert.Null(HtmlText.RawTitle("<html></html>"));
    }

    [Fact]
    public void The_opener_is_the_first_post_with_its_nested_divs_whole()
    {
        string page = "<article id='elComment_1'><time datetime='2021-01-01T00:00:00Z'></time><div data-role='commentContent'>a<div>b</div>c</div></article>"
            + "<article id='elComment_2'><div data-role='commentContent'>reply</div></article>";

        Assert.Equal(("a<div>b</div>c", "2021-01-01T00:00:00Z"), HtmlText.Opener(page));
        Assert.Equal((string.Empty, string.Empty), HtmlText.Opener("<article id=\"elComment_3\"></article>"));
        Assert.Equal(("open", string.Empty), HtmlText.Opener("<article id='elComment_4'><div data-role='commentContent'>open"));
        Assert.Null(HtmlText.Opener("<html></html>"));
    }

    [Fact]
    public void A_listing_names_each_topic_once_and_no_sub_forum()
    {
        const string Topic = "https://forums.homecomingservers.com/topic/1-a/";
        string listing = $"<li class='ipsDataItem'><a href='{Topic}'>a</a></li><li class='ipsDataItem'><a href='{Topic}'>again</a></li>"
            + "<li data-forumid='5' class='ipsDataItem'><a href='https://forums.homecomingservers.com/topic/2-b/'>b</a></li><li class='ipsDataItem'>no link</li>";

        Assert.Equal([Topic], HtmlText.TopicUrls(listing), StringComparer.Ordinal);
        Assert.Equal(3, HtmlText.LastPageNumber("<ul class='ipsPagination' data-pages='3'><li><a href='https://forums.homecomingservers.com/forum/74-forum/page/2/'>"));
        Assert.Equal(1, HtmlText.LastPageNumber(listing));
        Assert.Equal("https://forums.homecomingservers.com/forum/74-forum", HtmlText.ListingBase("<ul class='ipsPagination'><a href='https://forums.homecomingservers.com/forum/74-forum/page/2/'>", "https://x/forum/76-alias/"));
        Assert.Equal("https://x/forum/76-alias", HtmlText.ListingBase(listing, "https://x/forum/76-alias/"));
    }

    [Theory]
    [InlineData("https://h/", true, true, false)]
    [InlineData("https://h/robots.txt", true, true, true)]
    [InlineData("https://h/search/x", true, false, false)]
    [InlineData("https://h/topic/1-a/?sort=new", true, false, false)]
    [InlineData("https://h/forum/73-x/page/2/", true, true, false)]
    [InlineData("https://h/forum/73-x/page/3/", true, false, false)]
    [InlineData("https://h/private/ok/1", true, true, false)]
    [InlineData("https://h/private/no", false, true, false)]
    [InlineData("https://h", true, true, false)]
    [InlineData("https://h/a%2fb", true, true, false)]
    [InlineData("https://h/%7Euser/caf%C3%A9?q=a b&c=%zz", true, true, false)]
    [InlineData("/relative/caf\u00e9/\ud83d\ude00", true, true, false)]
    public void Robots_rules_answer_as_python_does(string url, bool tool, bool other, bool blocked)
    {
        RobotsRules rules = new(Robots.Split('\n'));

        Assert.Equal((tool, other, blocked), (rules.CanFetch(Agent, url), rules.CanFetch("Other/1", url), rules.CanFetch("GPTBot/1", url)));
    }

    [Theory]
    [InlineData("https://h/tie/x", true)]
    [InlineData("https://h/a1b2c3", false)]
    [InlineData("https://h/a1c2b", true)]
    public void A_tie_goes_to_allow_and_stars_match_in_order(string url, bool allowed)
    {
        RobotsRules rules = new(["User-agent: *", "Disallow: /tie", "Allow: /tie", "Disallow: /a*b*c"]);

        Assert.Equal(allowed, rules.CanFetch("X/1", url));
    }

    [Fact]
    public void Without_rules_everything_may_be_fetched()
    {
        Assert.True(new RobotsRules([]).CanFetch(Agent, "https://h/x"));
        Assert.True(new RobotsRules(["Disallow: /", "no colon"]).CanFetch(Agent, "https://h/x"));
    }

    [Fact]
    public void The_fetcher_paces_requests_and_reports_every_failure()
    {
        using FakeSite site = new();
        site.Routes[FakeSite.Forum + "/ok"] = "\u00e9";
        site.Routes[FakeSite.Forum + "/gone"] = HttpStatusCode.Gone;
        site.Routes[FakeSite.Forum + "/down"] = new HttpRequestException("boom");
        using HttpClient http = new(site, disposeHandler: false);
        using StringWriter error = new();
        PageFetcher fetcher = new(http, Agent, 0.01, FakeSite.Forum, error);

        Assert.Equal("\u00e9", fetcher.Get(FakeSite.Forum + "/ok"));
        Assert.Null(fetcher.Get(FakeSite.Forum + "/gone"));
        Assert.Null(fetcher.Get(FakeSite.Forum + "/down"));
        Assert.Null(fetcher.Get(FakeSite.Forum + "/search/x"));
        Assert.Equal(
            string.Join(Environment.NewLine, $"E200: fetch failed for {FakeSite.Forum}/gone: HTTP Error 410", $"E200: fetch failed for {FakeSite.Forum}/down: boom", $"skip (robots.txt disallows): {FakeSite.Forum}/search/x", string.Empty),
            error.ToString());
    }

    [Fact]
    public void Robots_rules_judge_the_path_that_is_sent_not_the_text_of_the_link()
    {
        using FakeSite site = new();
        string sneaky = FakeSite.Forum + "/topic/1-a\\..\\..\\search\\x/";
        site.Routes[sneaky] = "secret";
        using HttpClient http = new(site, disposeHandler: false);

        Assert.Null(new PageFetcher(http, Agent, 0, FakeSite.Forum, TextWriter.Null).Get(sneaky));
        Assert.Equal(0, site.Count(sneaky));
    }

    [Fact]
    public void Only_the_site_s_own_host_is_fetched_and_a_redirect_elsewhere_is_not_followed()
    {
        using FakeSite site = new();
        site.Routes[FakeSite.Forum + "/moved"] = new Uri(FakeSite.Forum + "/here");
        site.Routes[FakeSite.Forum + "/here"] = "arrived";
        site.Routes[FakeSite.Forum + "/away"] = new Uri("https://elsewhere.example/x");
        using HttpClient http = new(site, disposeHandler: false);
        using StringWriter error = new();
        PageFetcher fetcher = new(http, Agent, 0, FakeSite.Forum, error);

        Assert.Equal("arrived", fetcher.Get(FakeSite.Forum + "/moved"));
        Assert.Null(fetcher.Get("https://elsewhere.example/topic/1-a/"));
        Assert.Null(fetcher.Get("http://forums.homecomingservers.com/topic/1-a/"));
        Assert.Null(fetcher.Get(FakeSite.Forum + "/away"));
        Assert.DoesNotContain(site.Calls, call => call.Contains("elsewhere", StringComparison.Ordinal) || call.StartsWith("http:", StringComparison.Ordinal));
        Assert.Contains("E222: https://elsewhere.example/topic/1-a/ is not on https://forums.homecomingservers.com", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("E222: https://forums.homecomingservers.com/away redirects to https://elsewhere.example/x, which is not on https://forums.homecomingservers.com", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "E201: robots.txt refused (403) at https://forums.homecomingservers.com/robots.txt; nothing will be fetched")]
    [InlineData(HttpStatusCode.BadGateway, "E202: robots.txt unreadable (502) at https://forums.homecomingservers.com/robots.txt; re-run when the site recovers")]
    public void A_refused_or_broken_robots_file_blocks_the_site(HttpStatusCode code, string expected)
    {
        using FakeSite site = new();
        site.Routes[FakeSite.Forum + "/robots.txt"] = code;
        site.Routes[FakeSite.Forum + "/ok"] = "x";
        using HttpClient http = new(site, disposeHandler: false);
        using StringWriter error = new();

        Assert.Null(new PageFetcher(http, Agent, 0, FakeSite.Forum, error).Get(FakeSite.Forum + "/ok"));
        Assert.StartsWith(expected, error.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, site.Count(FakeSite.Forum + "/robots.txt"));
        Assert.Equal(0, site.Count(FakeSite.Forum + "/ok"));
    }

    [Fact]
    public void An_unreachable_robots_file_blocks_the_site_and_a_missing_one_allows_it()
    {
        using FakeSite site = new();
        site.Routes[FakeSite.Forum + "/robots.txt"] = new TaskCanceledException("timed out");
        site.Routes[FakeSite.Wiki + "/ok"] = "x";
        using HttpClient http = new(site, disposeHandler: false);
        using StringWriter error = new();

        Assert.Null(new PageFetcher(http, Agent, 0, FakeSite.Forum, error).Get(FakeSite.Forum + "/ok"));
        Assert.Equal("x", new PageFetcher(http, Agent, 0, FakeSite.Wiki, error).Get(FakeSite.Wiki + "/ok"));
        Assert.StartsWith($"E202: robots.txt unreadable at {FakeSite.Forum}/robots.txt: timed out; re-run when the site recovers", error.ToString(), StringComparison.Ordinal);
    }
}
