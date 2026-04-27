using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.LanguageAwareImages.Providers;

public class LanguageAwareMovieImageProvider : LanguageAwareImageProviderBase, IRemoteImageProvider
{
    public LanguageAwareMovieImageProvider(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }

    public bool Supports(BaseItem item) => item is Movie;

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

        var client = CreateClient();
        var images = await client.GetMovieImagesAsync(
            tmdbId,
            language: Config.PreferredLanguage,
            includeImageLanguage: BuildIncludeLanguageParam(),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (images is null)
        {
            return Array.Empty<RemoteImageInfo>();
        }

        var result = new List<RemoteImageInfo>();
        result.AddRange(RankAndMap(images.Posters, ImageType.Primary));
        result.AddRange(RankAndMap(images.Backdrops, ImageType.Backdrop));
        result.AddRange(RankAndMap(images.Logos, ImageType.Logo));
        return result;
    }
}
