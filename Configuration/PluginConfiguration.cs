using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.LanguageAwareImages.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string PreferredLanguage { get; set; } = "de";

    public string FallbackLanguage { get; set; } = "en";

    public bool IncludeNoLanguage { get; set; } = false;

    public string TmdbApiKey { get; set; } = string.Empty;
}
