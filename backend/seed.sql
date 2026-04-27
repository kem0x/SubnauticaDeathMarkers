-- A few hardcoded test markers near Subnautica's Lifepod 5 spawn (~0, 0, 0).
-- Used for the morning round-trip test before the in-game hook exists.
INSERT INTO markers (game, chunk_x, chunk_y, chunk_z, x, y, z, cause, note, created_at) VALUES
  ('subnautica',  0,  0,  0,    12.0,  -8.0,    5.0, 'reaper_leviathan', 'Should have looked behind me.', 1714000000000),
  ('subnautica',  0,  0,  0,   -45.0, -22.0,  -30.0, 'drowning',         null,                            1714100000000),
  ('subnautica',  0,  0,  0,   100.0, -15.0,   60.0, 'crash_fish',       'Tiny fish, big regret.',        1714200000000),
  ('subnautica',  1,  0, -1,   210.0, -40.0, -180.0, 'stalker',          null,                            1714300000000),
  ('subnautica', -1,  0,  0,  -150.0, -90.0,    8.0, 'bleeder',          'Thought it was decor.',         1714400000000);
