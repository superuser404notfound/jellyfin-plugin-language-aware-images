# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Jellyfin metadata plugin (GPL-3.0) that replaces the built-in TMDB
image provider with one that respects the library's language and matches
episode stills by title instead of by (Season, Episode) position. Pulls images
via `TMDbLib`.

The project **multi-targets two Jellyfin server lines** from one source tree
(see "Dual-target layout" below): `net8.0` against Jellyfin 10.10/10.11 and
`net10.0` against Jellyfin 12. The source compiles unchanged against both
package sets, so there are no `#if` branches and there must not be any.

## Build / install / develop

```bash
dotnet build                       # compiles BOTH target frameworks
./build.sh                         # publish + install into the local Jellyfin plugin dir (net10.0 / Jellyfin 12)
TFM=net8.0 ./build.sh              # same, for a local Jellyfin 10.10/10.11
```

`build.sh` installs to `$HOME/Library/Application Support/jellyfin/plugins/LanguageAwareImages`
by default (macOS); override with `JELLYFIN_PLUGIN_DIR`. It reads the version
and `targetAbi` for the chosen framework back out of MSBuild and writes a
matching `meta.json` next to the DLLs, so the installed plugin always declares
the ABI it was actually built against. It needs `jq`.

Only two assemblies ship: `Jellyfin.Plugin.LanguageAwareImages.dll` and
`TMDbLib.dll` (the Jellyfin `Controller`/`Model` packages are reference-only,
note `ExcludeAssets>runtime` in the csproj, they're provided by the host server
at runtime). `Newtonsoft.Json.dll` is deliberately *not* shipped either:
TMDbLib needs it, and Jellyfin's own `MediaBrowser.Providers` still references
it on both 10.10 and 12, so it resolves from the server output. Re-check that
before targeting any future server line.

There are no automated tests. Verification is manual: build, install, restart
Jellyfin, and trigger a metadata refresh on a library item. Enable `Debug`
logging in Jellyfin to see this plugin's ranking/match decisions (it logs under
the `LanguageAwareImages` prefix).

## Dual-target layout

The csproj is the single source of truth for the whole mapping. `PluginVersion`
holds the three-part release number; each target framework then adds its own
`PluginRevision` (the fourth version component) and `PluginTargetAbi`:

| TFM       | Jellyfin packages | Version   | `targetAbi` | Server line   |
| --------- | ----------------- | --------- | ----------- | ------------- |
| `net8.0`  | 10.10.7           | `X.Y.Z.0` | `10.10.0.0` | 10.10 / 10.11 |
| `net10.0` | 12.0.0            | `X.Y.Z.1` | `12.0.0.0`  | 12+           |

**Why the fourth component carries the line marker.** Jellyfin's
`InstallationManager.GetCompatibleVersions` keeps every manifest entry whose
`targetAbi <= ApplicationVersion` and then takes the highest `VersionNumber`.
A Jellyfin 12 server therefore considers *both* entries compatible, so the 12
build must sort strictly above the 10.10 build or the server would install the
net8.0 assembly. Two entries sharing one version number would be ambiguous in
exactly that way. `ApplicationVersion` is `Assembly.GetName().Version`, i.e.
always four-part (`12.0.0.0`), so a four-part `targetAbi` compares cleanly.

Shell scripts and CI must read this mapping back via
`dotnet msbuild -getProperty:PluginRevision -getProperty:PluginTargetAbi -p:TargetFramework=<tfm>`
rather than restating it, so it exists in one place only.

## Release flow

Releases are driven entirely by git tags through `.github/workflows/build.yml`:

1. Bump `PluginVersion` in the `.csproj` (three parts, no fourth component) and
   set `version` + `changelog` in `meta.json`.
2. Push a tag `vX.Y.Z`. CI verifies the tag matches `PluginVersion` and fails
   the build if it doesn't, then for **each** target framework publishes, writes
   a derived `meta.json` (version/targetAbi/framework), and zips it with the two
   DLLs. Both zips go onto one GitHub Release (release notes = the tag commit's
   full message), and CI commits **two** `manifest.json` entries back to `main`
   with `[skip ci]`.

`manifest.json` is the Jellyfin plugin-repo feed users add; it is machine-edited
by CI, so don't hand-edit version entries. The checked-in `meta.json` is only a
template: its `version`/`targetAbi`/`framework` are overwritten per artifact, so
the values sitting in git are the `net8.0` ones and are not authoritative.

## Architecture

Five `IRemoteImageProvider`s, all subclassing `LanguageAwareImageProviderBase`,
each gated to one Jellyfin entity type via `Supports`:

- `LanguageAwareMovieImageProvider` — Movie: Primary/Backdrop/Logo
- `LanguageAwareSeriesImageProvider` — Series: Primary/Backdrop/Logo
- `LanguageAwareSeasonImageProvider` — Season: Primary only (TMDB has no season backdrops/logos)
- `LanguageAwareEpisodeImageProvider` — Episode: Primary (still), title-matched
- `Plugin.cs` — registers config page; providers are auto-discovered by Jellyfin's DI.

Providers set `Order = -1` (vs. the built-in TMDB provider's `0`) so their
results are offered first. Jellyfin merges all providers' results and applies
its *own* downstream filter+sort, which is the source of most of the
non-obvious code (see below).

### The two key mechanisms

**Bucket ranking (`RankAndMap` in the base class)** — the core of the
poster/backdrop/logo logic. TMDB is asked (via `include_image_language`) for
preferred + optional original + fallback + optional textless (`null`) language
images in one call, then images are bucketed and ordered:

```
rank 0: preferred language
rank 1: fallback language   (default "en")
rank 2: original language   (opt-in, IncludeOriginalLanguage; dead-last by
                             default, or moved before fallback when
                             OriginalLanguageLast=false)
rank 3: textless (null)     (opt-in per image type; moved to rank -1, above the
                             preferred bucket, when PreferNoLanguageFor{Posters,
                             Backdrops,Logos} is set for that type)
excluded: dropped before mapping (no rank)
ORDER BY rank, then vote_count DESC tiebreak within a bucket
```

**Episode title-matching (`LanguageAwareEpisodeImageProvider`)** — fetches the
whole show's episodes once per `(showId, language)`, caches a normalised
`title → stillPath` map in a process-lifetime static `ConcurrentDictionary`,
and looks up the local episode's title against it. This decouples the image
from the (S,E) slot so alternative orderings (Disney+/DVD/chronological) get
the correct still. Always returns the title-matched still when found (not just
for out-of-sync episodes — see `meta.json` v0.7.4 changelog); returns empty on
no match so the built-in provider takes over with no regression.

### Two Jellyfin-pipeline workarounds (read before touching ranking)

Jellyfin re-processes the `RemoteImageInfo` list *after* the provider returns,
and that re-processing — not our own sort — is what actually decides the order
the user sees. Two facts about it (verified against `release-10.10.z` **and**
the `12.0.0` tag):

- **Filter** (`ProviderManager.GetImages`, when "show all languages" is off):
  keeps an image only if its `Language` is empty, equals the library metadata
  language (`item.GetPreferredMetadataLanguage()`, case-insensitive), or equals
  `"en"`. Nothing else survives — *not* a `PreferredLanguageOverride` value, not
  a regional variant like `pt-pt`, not the original language. **Unchanged in 12.**
- **Sort** (`OrderByLanguageDescending` in `MediaBrowser.Model`): orders by a
  language score, then `CommunityRating`, then `VoteCount`. So `VoteCount` is
  only a *third*-level tiebreak; `CommunityRating` dominates it. Jellyfin 12
  changed this method twice:
  - the score for `"en"` and for no-language swapped (10.10: empty 3, `"en"` 2;
    12: `"en"` 3, empty 2). Irrelevant to us, because we tag everything with the
    library language and land on 4 either way.
  - the `CommunityRating` comparison is now `Math.Round(rating, 1)`, so
    **anything finer than 0.1 is erased on 12**. This one bites, see below.

Our single workaround (`BuildRemoteImageInfo`) leans on both: it tags **every**
image we keep with the library metadata language (so all pass the filter and sit
in the top score tier), and bakes the bucket rank into `CommunityRating` — lower
rank → higher value → sorts first — so our intended order survives Jellyfin's
re-sort no matter the vote counts. Buckets are a whole `1.0` apart. Within a
bucket the value is identical and Jellyfin's `VoteCount` tiebreak applies
(`SortByVotes` on); with `SortByVotes` off we add `vote_average/10`, clamped to
`9.4`, so the within-bucket order is by rating instead. Both numbers are picked
for Jellyfin 12's rounding: `/10` puts the term on the same 0.1 grid the sort
rounds to (ten usable tiers on 12, full precision on 10.10), and the `9.4` clamp
caps it at `0.94`, which rounds to at most `0.9` and so can never push an image
into the next bucket. Do not shrink that term back below 0.1 granularity.

Cosmetic costs (accepted): the picker shows synthetic ratings instead of TMDB's,
and a Japanese / `pt-pt` poster is labelled with the library language. **Do not**
re-tag images with the override code or a regional variant — Jellyfin's filter
would drop them (this was the v0.8.0 bug behind issue #2's follow-up reports).

### Other shared base-class behavior

- `Config` is a static accessor over `Plugin.Instance.Configuration`; all
  provider tuning reads from `PluginConfiguration` (mirrored 1:1 in the README
  config table and the embedded `Configuration/configPage.html`).
- One shared static `TMDbClient`, rebuilt only when the API key changes
  (`GetClient`). Falls back to Jellyfin's bundled public TMDB key when the user
  hasn't set their own.
- `GetPreferredLanguages` resolves the ordered preferred cascade
  (`PreferredLanguageOverride` as a comma list, else the item's library metadata
  language auto-expanded `pt-BR` → `[pt-br, pt]`). `GetLibraryLanguageTag`
  returns the raw library metadata language used to tag images for Jellyfin's
  filter — these are deliberately different (see the workaround above).
- `NeedsOriginalLanguage()` gates an extra TMDB detail call — only fetched when
  `IncludeOriginalLanguage` or `OnlyOriginalLanguageForPosters` is set.

## Conventions

- Adding a config option means touching three places in lockstep:
  `PluginConfiguration.cs`, `Configuration/configPage.html` (the admin form),
  and the README config table.
- Title normalisation for episode matching (`NormaliseTitle`) strips a leading
  DE/EN article, lowercases, and drops all non-alphanumerics; combined titles
  (`"A / B"`) are split on `/` and each half tried. Keep it deterministic and
  cheap — it deliberately avoids fuzzy/Levenshtein matching.
