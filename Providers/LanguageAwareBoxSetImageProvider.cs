using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using TMDbLib.Objects.Collections;

namespace Jellyfin.Plugin.LanguageAwareImages.Providers;

// Collections (BoxSet), whether created by hand or by the TMDb Box Sets plugin:
// both store the TMDB collection id under MetadataProvider.Tmdb, same as
// Jellyfin's built-in TmdbBoxSetImageProvider reads it.
public class LanguageAwareBoxSetImageProvider : LanguageAwareImageProviderBase, IRemoteImageProvider
{
    public LanguageAwareBoxSetImageProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<LanguageAwareBoxSetImageProvider> logger)
        : base(httpClientFactory, logger)
    {
    }

    public bool Supports(BaseItem item) => item is BoxSet;

    // TMDB collections carry posters and backdrops, no logos.
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new[]
    {
        ImageType.Primary,
        ImageType.Backdrop,
        ImageType.Thumb
    };

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var tmdbIdRaw = item.GetProviderId(MetadataProvider.Tmdb);
        if (!int.TryParse(tmdbIdRaw, out var tmdbId))
        {
            return Array.Empty<RemoteImageInfo>();
        }

        var preferred = GetPreferredLanguages(item);
        var apiLanguage = LanguageMatching.ToTmdbLanguage(
            preferred.Count > 0 ? preferred[0] : (GetFallbackLanguages().FirstOrDefault() ?? string.Empty));

        var client = GetClient();

        // A collection has no original_language of its own on TMDB, so take the
        // most common one among its movies.
        var originalLanguage = string.Empty;
        if (NeedsOriginalLanguage())
        {
            var collection = await client.GetCollectionAsync(tmdbId, CollectionMethods.Undefined, cancellationToken)
                .ConfigureAwait(false);
            originalLanguage = collection?.Parts?
                .Select(p => NormaliseLanguage(p.OriginalLanguage))
                .Where(l => !string.IsNullOrEmpty(l))
                .GroupBy(l => l)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? string.Empty;
        }

        return await FetchRankMapAsync(
            item,
            new[] { ImageType.Primary, ImageType.Backdrop, ImageType.Thumb },
            originalLanguage,
            async (include, ct) =>
            {
                // GetCollectionImagesAsync has no include_image_language parameter,
                // so go through the details call with the Images append instead.
                var collection = await client.GetCollectionAsync(
                    tmdbId, apiLanguage, include, CollectionMethods.Images, ct)
                    .ConfigureAwait(false);
                return new MultiImages(collection?.Images?.Posters, collection?.Images?.Backdrops, null);
            },
            cancellationToken).ConfigureAwait(false);
    }
}
