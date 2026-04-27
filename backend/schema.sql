CREATE TABLE IF NOT EXISTS markers (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  game TEXT NOT NULL,
  chunk_x INTEGER NOT NULL,
  chunk_y INTEGER NOT NULL,
  chunk_z INTEGER NOT NULL,
  x REAL NOT NULL,
  y REAL NOT NULL,
  z REAL NOT NULL,
  cause TEXT,
  note TEXT,
  created_at INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_markers_chunk
  ON markers (game, chunk_x, chunk_y, chunk_z);

CREATE INDEX IF NOT EXISTS idx_markers_created_at
  ON markers (created_at);
