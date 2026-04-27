# Death Markers

Communal memorials for single-player games. Tested in Subnautica.

[Live stats](https://ol.mr/subnautica) · [Download](https://death-markers-api.kareemolim.workers.dev/download) · Built for Wand Hackweek 2026.

---

## What it does

When other players die nearby, the world remembers — and shows you when you're about to die yourself. Mario Maker death markers, but for an underwater alien planet that wants you dead.

- Records every death (position, cause)
- When your oxygen drops below 3 seconds, hidden tombstone crosses fade in around your location — markers are normally invisible during play
- If you actually die, a HUD names the toll: **"5 OTHERS DIED HERE"**
- Cause-colored crosses (purple = crush, red = fire, electric blue = electrical, amber = drowning, etc.) — a glance reads the story of how the area tends to kill people

The mod ships hidden by default and only reveals when you're already in trouble. The point isn't to clutter the world; it's to deliver one quiet "you're not the first" beat at the moment it lands hardest.

## Install

1. Install [BepInExPack for Subnautica](https://www.nexusmods.com/subnautica/mods/1108) and [Nautilus](https://www.nexusmods.com/subnautica/mods/1262) from NexusMods.
2. Download the [latest release zip](https://death-markers-api.kareemolim.workers.dev/download).
3. Extract the `SubnauticaDeathMarkers/` folder into:
   ```
   <Subnautica>/BepInEx/plugins/
   ```
4. Launch the game. Check `<Subnautica>/BepInEx/LogOutput.log` for a line ending in `Death Markers v… loaded.` to confirm.

## Configure

Settings live at `<Subnautica>/BepInEx/config/com.kareem.deathmarkers.cfg`:

| Key | Default | What |
| --- | --- | --- |
| `ApiBaseUrl` | the public Worker | Override for local dev or self-hosted backend |
| `GameId` | `subnautica` | Stored with each marker; lets one backend serve multiple games |

Debug keybinds (only useful while developing):

| Key | Effect |
| --- | --- |
| `F2` | Trigger the full death reveal at the player's current position |
| `F3` | Trigger the low-oxygen pre-death reveal (markers only, no HUD) |
| `F4` | Force a fade-out of the current reveal |

## Architecture

Three pieces, each a few hundred lines:

```
   ┌──────────────────────────────┐
   │  Subnautica + BepInEx + mod  │  C# / Harmony / Nautilus
   └──────────────┬───────────────┘
                  │ POST /markers   (on death)
                  │ GET  /markers   (on world load, for the area)
                  ▼
   ┌──────────────────────────────┐
   │   Cloudflare Worker (Hono)   │  TS, free tier, ~165 lines
   │     ┌────────────────────┐   │
   │     │     D1 (SQLite)    │   │
   │     └────────────────────┘   │
   └──────────────┬───────────────┘
                  │ GET /api/stats
                  ▼
   ┌──────────────────────────────┐
   │   ol.mr/subnautica (Next)    │  React, lives in the portfolio repo
   └──────────────────────────────┘
```

- The world is partitioned into 200m chunks. The mod queries the chunk it's in plus all 26 neighbors, capped at 100 markers — bounded payload regardless of total deaths.
- Markers are pre-allocated on world load and pooled. Death events never call `Instantiate`; they just toggle visibility on existing GameObjects. Spawn pause is zero.
- The Worker is stateless; D1 holds the only state.

## Build from source

### Mod (C#)

Targets `net472` to match Subnautica's Unity 2019 / Mono runtime.

```sh
cd mod
dotnet build -c Release
```

The post-build target copies the DLL to your local Subnautica install (`Z:\Subnautica` by default — edit the `SubnauticaPath` property in `SubnauticaDeathMarkers.csproj` to match yours).

To produce a release zip:

```sh
pwsh ./scripts/package.ps1
```

That writes `SubnauticaDeathMarkers.zip` at the repo root, ready to upload to GitHub Releases. The download link on the stats page resolves to the latest release automatically.

### Backend (Cloudflare Worker)

```sh
cd backend
cp wrangler.toml.example wrangler.toml
npx wrangler login
npx wrangler d1 create death-markers
# paste the printed database_id into wrangler.toml
npm install
npm run db:migrate:remote
npm run dev          # local: http://127.0.0.1:8787
npx wrangler deploy  # ship to *.workers.dev
```

Free-tier limits are well above what this workload uses (D1 indexed lookup is sub-millisecond; 100k Worker requests/day free).

### Stats page

Lives in [the portfolio repo](https://github.com/kem0x/olmr) under `app/subnautica/`. It's a regular Next.js page that fetches `/api/stats` from the Worker on the client side, then renders with the portfolio's existing components.

## Repo layout

```
backend/             Cloudflare Worker + Hono + D1
  src/index.ts       The whole API (~165 lines)
  schema.sql         D1 schema
  wrangler.toml.example
mod/                 C# mod for Subnautica
  src/
    Plugin.cs        BepInEx entry, config, debug keybinds
    MarkerSpawner.cs Fetch + spawn + reveal/hide state machine
    DeathReporter.cs POST on death
    DeathMarker.cs   Per-marker MonoBehaviour (billboard + scale fade)
    DeathHud.cs      OnGUI overlay text
    Patches/         Harmony postfixes
scripts/
  package.ps1        Builds Release + zips for distribution
presentation.md      Marp slide deck for the hackweek demo
```

## Why

Single-player games are lonely. Multiplayer games are loud. Mario Maker found a third lane — show me where strangers died on this level and the world feels populated without anyone yelling at me. Souls bloodstains do the same trick.

This is a working proof that the harness lifts cleanly to a game that doesn't ship with it. The same backend would run a Hollow Knight version, a Pacific Drive version, a Stardew version — each one a 200-line mod.

## License

MIT. Do whatever you want, just don't pretend you wrote it.
