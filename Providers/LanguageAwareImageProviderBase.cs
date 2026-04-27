using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using TMDbLib.Client;
using TMDbLib.Objects.General;

namespace Jellyfin.Plugin.LanguageAwareImages.Providers;

public abstract class LanguageAwareImageProviderBase : IHasOrder
{
    // Public, well-known key that ships in Jellyfin's own source
    // (MediaBrowser.Providers/Plugins/Tmdb/TmdbUtils.cs). Used as a fallback
    // when the user hasn't provided their own.
    protected const string DefaultJellyfinTmdbKey = "4219e299c89411838049ab0dab19ebd5";

    protected const string TmdbImageBaseUrl = "https://image.tmdb.org/t/p/original";

    protected readonly IHttpClientFactory HttpClientFactory;

    protected LanguageAwareImageProviderBase(IHttpClientFactory httpClientFactory)
    {
        HttpClientFactory = httpClientFactory;
    }

    public string Name => "Language-Aware TMDB Images";

    // Lower than Jellyfin's bundled TmdbXxxImageProvider (Order = 0),
    // so this provider's results appear first.
    public int Order => -1;

    protected static Configuration.PluginConfiguration Config =>
        Plugin.Instance!.Configuration;

    protected TMDbClient CreateClient()
    {
        var key = string.IsNullOrWhiteSpace(Config.TmdbApiKey)
            ? DefaultJellyfinTmdbKey
            : Config.TmdbApiKey;
        return new TMDbClient(key);
    }

    // Resolves the effective preferred language for a given item:
    // 1. If the user set a global PreferredLanguageOverride, that wins.
    // 2. Otherwise, ask the item what its library/parent language is.
    // 3. Normalise to a 2-letter ISO 639-1 code (Jellyfin sometimes returns
    //    "en-US" style; TMDB filters expect plain "en").
    // Returns empty string if nothing is configured anywhere.
    protected static string GetEffectivePreferredLanguage(BaseItem item)
    {
        var lang = !string.IsNullOrWhiteSpace(Config.PreferredLanguageOverride)
            ? Config.PreferredLanguageOverride
            : item.GetPreferredMetadataLanguage();

        if (string.IsNullOrWhiteSpace(lang))
        {
            return string.Empty;
        }

        // "en-US" -> "en", "de-DE" -> "de"
        var dash = lang.IndexOf('-');
        return (dash > 0 ? lang[..dash] : lang).ToLowerInvariant();
    }

    // TMDB's `include_image_language` accepts a comma list. The literal token
    // "null" pulls textless images. Order in the list does not affect ranking;
    // we apply our own bucket sort below.
    protected string BuildIncludeLanguageParam(string preferredLanguage)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferredLanguage))
        {
            parts.Add(preferredLanguage);
        }

        if (!string.IsNullOrWhiteSpace(Config.FallbackLanguage))
        {
            parts.Add(Config.FallbackLanguage);
        }

        if (Config.IncludeNoLanguage)
        {
            parts.Add("null");
        }

        return string.Join(",", parts.Distinct());
    }

    // Heart of the plugin: filter by language bucket, then ORDER BY vote_count DESC,
    // vote_average DESC — the same ordering TMDB's own /images UI uses.
    protected IEnumerable<RemoteImageInfo> RankAndMap(
        IEnumerable<ImageData>? images,
        ImageType type,
        string preferredLanguage)
    {
        if (images is null)
        {
            return Array.Empty<RemoteImageInfo>();
        }

        var fallback = Config.FallbackLanguage;
        var includeNull = Config.IncludeNoLanguage;

        int Rank(string? iso)
        {
            if (!string.IsNullOrEmpty(preferredLanguage)
                && string.Equals(iso, preferredLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (!string.IsNullOrEmpty(fallback)
                && string.Equals(iso, fallback, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.IsNullOrEmpty(iso) && includeNull)
            {
                return 2;
            }

            return 99;
        }

        return images
            .Where(i => Rank(i.Iso_639_1) < 99)
            .OrderBy(i => Rank(i.Iso_639_1))
            .ThenByDescending(i => i.VoteCount)
            .ThenByDescending(i => i.VoteAverage)
            .Select(i => new RemoteImageInfo
            {
                ProviderName = Name,
                Type = type,
                Url = TmdbImageBaseUrl + i.FilePath,
                Width = i.Width,
                Height = i.Height,
                Language = i.Iso_639_1,
                CommunityRating = i.VoteAverage,
                VoteCount = i.VoteCount,
                RatingType = RatingType.Score
            })
            .ToList();
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return HttpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
    }
}
