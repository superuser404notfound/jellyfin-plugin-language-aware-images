# Jellyfin.Plugin.LanguageAwareImages

A drop-in replacement for Jellyfin's built-in TMDB image provider that
**strictly respects the library language** and **sorts images by `vote_count`** —
the same ordering TMDB's own `/images` UI uses.

## The problem

Two long-standing complaints about Jellyfin's bundled TMDB image provider:

- **[jellyfin/jellyfin#8925](https://github.com/jellyfin/jellyfin/issues/8925)** —
  TMDB posters ignore the library/preferred language. A German-language library
  receives English posters anyway.
- **[jellyfin/jellyfin#9878](https://github.com/jellyfin/jellyfin/issues/9878)** —
  When language-matched posters exist, Jellyfin still picks textless ones and
  ignores TMDB's vote-count signal.

Existing third-party plugins partly address language matching but **none rank
images the way TMDB's own UI does**. Visit
`https://www.themoviedb.org/movie/<id>/images/posters?image_language=de` —
the order you see there is `vote_count DESC, vote_average DESC` within each
language bucket. That's what this plugin gives you.

## How it compares

| Behavior                                     | Built-in TMDB | ArtworkMultiSource | **LanguageAwareImages** |
| -------------------------------------------- | :-----------: | :----------------: | :---------------------: |
| Filters by preferred / fallback language     |   partial     |        yes         |          **yes**        |
| Excludes textless by default                 |     no        |     configurable   |          **yes**        |
| Sorts within bucket by `vote_count`          |     no        |         no         |          **yes**        |
| Matches TMDB UI ranking                      |     no        |         no         |          **yes**        |
| Movies / Series / Seasons                    |     yes       |     yes (most)     |          **yes**        |

## Install

### Via plugin repository (recommended)

In Jellyfin: *Admin → Plugins → Repositories → +* and add:

```
https://raw.githubusercontent.com/superuser404notfound/Jellyfin.Plugin.LanguageAwareImages/main/manifest.json
```

Then *Catalog → Metadata → Language-Aware Images → Install*. Restart the server.

### Manual install

1. Download `language-aware-images_<version>.zip` from
   [Releases](https://github.com/superuser404notfound/Jellyfin.Plugin.LanguageAwareImages/releases).
2. Extract into your Jellyfin `plugins/LanguageAwareImages/` directory:
   - macOS (official installer): `~/Library/Application Support/jellyfin/plugins/LanguageAwareImages/`
   - Linux (default): `~/.local/share/jellyfin/plugins/LanguageAwareImages/`
   - Docker: `/config/plugins/LanguageAwareImages/`
3. Restart Jellyfin.

### Build from source

```bash
git clone https://github.com/superuser404notfound/Jellyfin.Plugin.LanguageAwareImages.git
cd Jellyfin.Plugin.LanguageAwareImages
./build.sh   # honors $JELLYFIN_PLUGIN_DIR; defaults to the macOS path
```

Requires .NET 8 SDK.

## Configuration

*Admin → Plugins → Language-Aware Images*:

| Field                | Default | Notes                                                            |
| -------------------- | :-----: | ---------------------------------------------------------------- |
| `PreferredLanguage`  |  `de`   | ISO 639-1.                                                       |
| `FallbackLanguage`   |  `en`   | Used when preferred has no images.                               |
| `IncludeNoLanguage`  | `false` | Allow textless images as last resort. Useful for logos.          |
| `TmdbApiKey`         | empty   | Bring your own TMDB key. Empty = uses Jellyfin's bundled key.    |

> **Important:** After installing, go to *Admin → Library → (your library) → Image Fetchers*
> and drag **Language-Aware TMDB Images** to the top. Jellyfin queries fetchers
> in user-configured order; this plugin can only win if it runs first.

## How the ranking works

For each `ImageType` (Primary / Backdrop / Logo, plus seasons → Primary only),
the plugin:

1. Fetches `/movie/<id>/images` (or the TV / season equivalent) from TMDB
   with `include_image_language=de,en[,null]`.
2. Filters out anything not in `{preferred, fallback, [null]}`.
3. Sorts:

   ```sql
   ORDER BY language_bucket(preferred=0, fallback=1, null=2) ASC,
            vote_count DESC,
            vote_average DESC
   ```

4. Maps results to `RemoteImageInfo` and returns them — already sorted, with
   `Order = -1` so it preempts Jellyfin's bundled provider.

## License

GPL-3.0 (the plugin links against Jellyfin's GPL assemblies).

## Acknowledgements

- [TMDbLib](https://github.com/jellyfin/TMDbLib) — Jellyfin's fork of LordMike/TMDbLib.
- The Movie Database (TMDB) for the data.
