using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.LanguageAwareImages.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    // Empty (the default) means: pick up the language from the item's
    // library / parent metadata settings via BaseItem.GetPreferredMetadataLanguage().
    // Set to a 2-letter ISO 639-1 code to force a specific language regardless
    // of library settings.
    public string PreferredLanguageOverride { get; set; } = string.Empty;

    public string FallbackLanguage { get; set; } = "en";

    public bool IncludeNoLanguage { get; set; } = false;

    public string TmdbApiKey { get; set; } = string.Empty;
}
