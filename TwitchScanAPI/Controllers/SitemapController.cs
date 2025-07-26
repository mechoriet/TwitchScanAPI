using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TwitchScanAPI.Data.Twitch.Manager;

namespace TwitchScanAPI.Controllers;

public class SitemapController : Controller
{
    private readonly ILogger<SitemapController> _logger;
    private readonly IConfiguration _configuration;
    private readonly TwitchChannelManager _twitchChannelManager;
    private readonly IMemoryCache _memoryCache;

    public SitemapController(
        ILogger<SitemapController> logger,
        IConfiguration configuration,
        TwitchChannelManager twitchChannelManager,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _configuration = configuration;
        _twitchChannelManager = twitchChannelManager;
        _memoryCache = memoryCache;
    }

    [HttpGet]
    [Route("sitemap.xml")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var cachedSitemap = await _memoryCache.GetOrCreateAsync("sitemap", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                var baseUrl = GetBaseUrl();
                return await GenerateSitemapAsync(baseUrl);
            });

            if (!string.IsNullOrEmpty(cachedSitemap))
            {
                Response.Headers["Content-Type"] = "application/xml; charset=utf-8";
                return Content(cachedSitemap, "application/xml", Encoding.UTF8);
            }

            _logger.LogWarning("Sitemap is empty or could not be generated.");
            return StatusCode(500);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sitemap");
            return StatusCode(500);
        }
    }

    private string GetBaseUrl()
    {
        var baseUrl = _configuration["BaseUrl"];
        if (string.IsNullOrEmpty(baseUrl))
        {
            baseUrl = $"{Request.Scheme}://{Request.Host}";
        }
        return baseUrl.TrimEnd('/');
    }

    private async Task<string> GenerateSitemapAsync(string baseUrl)
    {
        var urls = await GetSitemapUrlsAsync(baseUrl);
        
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(false), // Explicitly use UTF-8 without BOM
            Async = true,
            OmitXmlDeclaration = false // Ensure XML declaration is included
        };

        using var stringWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, settings);

        // Explicitly write XML declaration with UTF-8 encoding
        await xmlWriter.WriteStartDocumentAsync();
        await xmlWriter.WriteRawAsync("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        await xmlWriter.WriteStartElementAsync(null, "urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

        foreach (var url in urls)
        {
            await xmlWriter.WriteStartElementAsync(null, "url", null);
            await xmlWriter.WriteElementStringAsync(null, "loc", null, url.Location);
            await xmlWriter.WriteElementStringAsync(null, "lastmod", null, url.LastModified.ToString("yyyy-MM-ddTHH:mm:ss+00:00"));
            await xmlWriter.WriteElementStringAsync(null, "changefreq", null, url.ChangeFrequency.ToString().ToLower());
            await xmlWriter.WriteElementStringAsync(null, "priority", null, url.Priority.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            await xmlWriter.WriteEndElementAsync();
        }

        await xmlWriter.WriteEndElementAsync();
        await xmlWriter.WriteEndDocumentAsync();
        await xmlWriter.FlushAsync();

        return stringWriter.ToString();
    }

    private async Task<List<SitemapUrl>> GetSitemapUrlsAsync(string baseUrl)
    {
        var urls = new List<SitemapUrl>
        {
            // Add static pages
            new()
            {
                Location = baseUrl,
                LastModified = DateTime.UtcNow,
                ChangeFrequency = ChangeFrequency.Daily,
                Priority = 1.0
            }
        };

        // Add Twitch channel pages
        var twitchUrls = await GetTwitchChannelUrls(baseUrl);
        if (twitchUrls.Count > 0)
        {
            urls.AddRange(twitchUrls);
        }

        // Ensure we don't exceed Google's 50,000 URL limit
        if (urls.Count <= 50000) return urls;
        _logger.LogWarning($"Sitemap contains {urls.Count} URLs, exceeding Google's 50,000 URL limit. Truncating to 50,000.");
        urls = urls.GetRange(0, 50000);

        return urls;
    }

    private async Task<List<SitemapUrl>> GetTwitchChannelUrls(string baseUrl)
    {
        var urls = new List<SitemapUrl>();

        try
        {
            var channels = await _twitchChannelManager.GetInitiatedChannels();
            
            foreach (var channel in channels)
            {
                // Ensure channel name is URL-encoded to handle special characters
                var encodedChannelName = HttpUtility.UrlEncode(channel.ChannelName);
                var channelUrl = $"{baseUrl}/c/{encodedChannelName}";
                
                // Validate URL length (Google's limit is 2,048 characters)
                if (channelUrl.Length > 2048)
                {
                    _logger.LogWarning($"URL for channel {channel.ChannelName} exceeds 2048 characters and will be skipped.");
                    continue;
                }

                urls.Add(new SitemapUrl
                {
                    Location = channelUrl,
                    LastModified = DateTime.UtcNow,
                    ChangeFrequency = channel.IsOnline ? ChangeFrequency.Hourly : ChangeFrequency.Daily,
                    Priority = channel.IsOnline ? 0.9 : 0.7
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Twitch channels for sitemap");
        }

        return urls;
    }
}

public class SitemapUrl
{
    public string Location { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public ChangeFrequency ChangeFrequency { get; set; }
    public double Priority { get; set; }
}

public enum ChangeFrequency
{
    Always,
    Hourly,
    Daily,
    Weekly,
    Monthly,
    Yearly,
    Never
}