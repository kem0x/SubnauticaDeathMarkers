---
marp: true
theme: default
paginate: true
backgroundColor: "#0e1116"
color: "#e6e6e6"
---

<!-- _class: lead -->

# Death Markers

A Subnautica mod that gives single-player games a communal memory.

Wand Hackweek · April 2026

---

## The pitch

Single-player games are lonely. Multiplayer games are loud.

Mario Maker found the middle — show me where strangers died on this level, and the world feels populated without anyone yelling at me. Souls bloodstains do the same trick.

I wanted to see if I could bolt that mechanic onto a game that doesn't ship with it.

---

## What I built

A Subnautica mod that:

- **Records every death** — position + cause (drowning, crush, reaper bite, fire…)
- **Reveals other players' deaths nearby when you're about to die** — oxygen drops below 3 seconds, a field of memorial crosses fades into view around you
- **Names the toll on actual death** — *"5 OTHERS DIED HERE"* over the blackout

Two beats: the warning, and the eulogy.

---

## Demo

[live run]

Talking points while the mod loads:
- Markers stay invisible during normal play — clean game
- Hold breath underwater → markers bloom in
- Surface in time → they fade out, no harm done
- Don't surface → HUD kicks in over the death cam

---

## How it works

```
                  ┌──────────────────────────┐
   Subnautica     │   Cloudflare Worker      │
   ┌─────────┐    │   ┌──────────────────┐   │
   │ BepInEx │    │   │      Hono        │   │
   │   +     │ ── │ → │  POST /markers   │ → │ → D1 (SQLite)
   │ Harmony │    │   │  GET  /markers   │   │
   └─────────┘    │   └──────────────────┘   │
   patch:         └──────────────────────────┘
   Player.OnKill
```

- Mod: BepInEx 5 + Nautilus + HarmonyX, postfix on `Player.OnKill(DamageType)`
- Backend: Cloudflare Worker + Hono + D1, free tier
- World chunked into 200m cubes; queries return chunk + 26 neighbors, capped at 100 markers

Cost to host: $0.

---

## Why this could be more than hackweek

Wand's whole thing is making single-player games feel better without changing what makes them single-player.

Communal-but-asynchronous is a layer the customization scene hasn't really touched. The same backend works for any game with a death event and world coordinates: Hollow Knight, Stardew, Pacific Drive, Dark Souls — pick one, write a 200-line mod, you have death markers.

The Subnautica mod is a working proof that the harness is the hard part, not the per-game integration.

---
