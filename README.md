# Jellyfin.Plugin.LanguageAwareImages

A drop-in TMDB image provider for Jellyfin that respects each library's
metadata language and ranks images by `vote_count` — the same order TMDB's
own `/images` UI uses.

## Install

In Jellyfin: *Admin → Plugins → Repositories → +* and add

```
https://raw.githubusercontent.com/superuser404notfound/Jellyfin.Plugin.LanguageAwareImages/main/manifest.json
```

Then *Catalog → Metadata → Language-Aware Images → Install*. Restart the server.

> After install, go to *Admin → Library → (your library) → Image Fetchers* and
> drag **Language-Aware TMDB Images** to the top — otherwise the built-in
> provider still wins.

## Configuration

*Admin → Plugins → Language-Aware Images*:

| Field                       | Default | Notes                                                                          |
| --------------------------- | :-----: | ------------------------------------------------------------------------------ |
| `PreferredLanguageOverride` | empty   | Empty = use each library's metadata language. Set e.g. `de` to force globally. |
| `FallbackLanguage`          |  `en`   | Used when preferred has no images.                                             |
| `IncludeNoLanguage`         | `false` | Allow textless images as last resort. Useful for logos.                        |
| `TmdbApiKey`                | empty   | Bring your own TMDB key. Empty = uses Jellyfin's bundled key.                  |

## Why

Jellyfin's built-in TMDB image provider ignores the library language
([#8925](https://github.com/jellyfin/jellyfin/issues/8925)) and prefers
textless posters even when language-matched ones exist
([#9878](https://github.com/jellyfin/jellyfin/issues/9878)).

This plugin fixes both: filter by `iso_639_1`, then `ORDER BY vote_count DESC,
vote_average DESC` within each language bucket — exactly what TMDB's site
shows at `/movie/<id>/images/posters?image_language=de`.

## License

GPL-3.0 (the plugin links against Jellyfin's GPL assemblies).
