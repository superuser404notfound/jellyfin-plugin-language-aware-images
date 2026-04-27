using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.LanguageAwareImages.Providers;

public class LanguageAwareSeriesImageProvider : LanguageAwareImageProviderBase, IRemoteImageProvider
{
    public LanguageAwareSeriesImageProvider(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }

    public bool Supports(BaseItem item) => item is Series;

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new[]
    {
        ImageType.Primary,
        ImageType.Backdrop,
        ImageType.Logo
    };

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var tmdbIdRaw = item.GetProviderId(MetadataProvider.Tmdb);
        if (!int.TryParse(tmdbIdRaw, out var tmdbId))
        {
            return Array.Empty<RemoteImageInfo>();
        }

        var preferredLanguage = GetEffectivePreferredLanguage(item);
        var apiLanguage = string.IsNullOrEmpty(preferredLanguage)
            ? Config.FallbackLanguage
            : preferredLanguage;

        var client = CreateClient();
        var images = await client.GetTvShowImagesAsync(
            tmdbId,
            language: apiLanguage,
            includeImageLanguage: BuildIncludeLanguageParam(preferredLanguage),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (images is null)
        {
            return Array.Empty<RemoteImageInfo>();
        }

        var result = new List<RemoteImageInfo>();
        result.AddRange(RankAndMap(images.Posters, ImageType.Primary, preferredLanguage));
        result.AddRange(RankAndMap(images.Backdrops, ImageType.Backdrop, preferredLanguage));
        result.AddRange(RankAndMap(images.Logos, ImageType.Logo, preferredLanguage));
        return result;
    }
}
