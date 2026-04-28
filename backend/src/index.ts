import { Hono } from 'hono';
import { cors } from 'hono/cors';

type Bindings = {
  death_markers: D1Database;
  CHUNK_SIZE: string;
  FETCH_LIMIT: string;
  DOWNLOAD_URL?: string;
  REPO_URL?: string;
  STATS_PAGE_URL?: string;
};

// Subnautica DamageType enum values (lowercase).
const VALID_CAUSES = new Set([
  'acid',
  'cold',
  'collide',
  'drill',
  'electrical',
  'explosive',
  'fire',
  'heat',
  'normal',
  'poison',
  'pressure',
  'puncture',
  'radiation',
  'smoke',
  'starve',
]);

// Rate limiter: 1 POST per 30 seconds per IP.
const RATE_WINDOW_MS = 30_000;
const ipLastPost = new Map<string, number>();

function isRateLimited(ip: string): boolean {
  const now = Date.now();
  const last = ipLastPost.get(ip);
  if (last && now - last < RATE_WINDOW_MS) return true;
  ipLastPost.set(ip, now);
  return false;
}

type MarkerRow = {
  x: number;
  y: number;
  z: number;
  cause: string | null;
  note: string | null;
  created_at: number;
};

type CauseRow = { cause: string; n: number };
type RecentRow = {
  cause: string;
  x: number;
  y: number;
  z: number;
  created_at: number;
};

const app = new Hono<{ Bindings: Bindings }>();

// Public stats are embedded from ol.mr/subnautica (and potentially other origins).
app.use('/api/*', cors({ origin: '*', allowMethods: ['GET'] }));

// ---------------------------------------------------------------------------
// API: marker write/read used by the mod
// ---------------------------------------------------------------------------

app.post('/markers', async (c) => {
  const ip = c.req.header('cf-connecting-ip') || 'unknown';
  if (isRateLimited(ip)) {
    return c.json({ error: 'rate limited' }, 429);
  }

  const body = await c.req.json<{
    game?: string;
    x?: number;
    y?: number;
    z?: number;
    cause?: string;
    note?: string;
  }>();

  const game = body.game?.trim();
  const { x, y, z } = body;

  if (!game || typeof x !== 'number' || typeof y !== 'number' || typeof z !== 'number') {
    return c.json({ error: 'game, x, y, z required' }, 400);
  }

  const cause = body.cause?.toLowerCase().trim() ?? null;
  if (cause && !VALID_CAUSES.has(cause)) {
    return c.json({ error: 'invalid cause' }, 400);
  }

  const chunkSize = Number(c.env.CHUNK_SIZE) || 200;
  const cx = Math.floor(x / chunkSize);
  const cy = Math.floor(y / chunkSize);
  const cz = Math.floor(z / chunkSize);

  const note = body.note?.slice(0, 280) ?? null;

  await c.env.death_markers.prepare(
    `INSERT INTO markers (game, chunk_x, chunk_y, chunk_z, x, y, z, cause, note, created_at)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
  )
    .bind(game, cx, cy, cz, x, y, z, cause, note, Date.now())
    .run();

  return c.json({ ok: true, chunk: [cx, cy, cz] });
});

app.get('/markers', async (c) => {
  const game = c.req.query('game');
  const chunk = c.req.query('chunk');

  if (!game || !chunk) {
    return c.json({ error: 'game and chunk=x,y,z required' }, 400);
  }

  const parts = chunk.split(',').map(Number);
  if (parts.length !== 3 || parts.some((n) => !Number.isFinite(n))) {
    return c.json({ error: 'chunk must be three comma-separated integers' }, 400);
  }
  const [cx, cy, cz] = parts;

  const limit = Math.min(Number(c.env.FETCH_LIMIT) || 100, 200);

  const { results } = await c.env.death_markers.prepare(
    `SELECT x, y, z,
            COALESCE(cause, '') AS cause,
            COALESCE(note,  '') AS note,
            created_at
       FROM markers
      WHERE game = ?
        AND chunk_x BETWEEN ? AND ?
        AND chunk_y BETWEEN ? AND ?
        AND chunk_z BETWEEN ? AND ?
      ORDER BY created_at DESC
      LIMIT ?`
  )
    .bind(game, cx - 1, cx + 1, cy - 1, cy + 1, cz - 1, cz + 1, limit)
    .all<MarkerRow>();

  return c.json({ markers: results ?? [] });
});

// ---------------------------------------------------------------------------
// Public stats JSON (consumed by ol.mr/subnautica)
// ---------------------------------------------------------------------------

app.get('/api/stats', async (c) => {
  const game = c.req.query('game') || 'subnautica';

  const total = await c.env.death_markers.prepare(
    'SELECT COUNT(*) AS n FROM markers WHERE game = ?'
  )
    .bind(game)
    .first<{ n: number }>();

  const byCause = await c.env.death_markers.prepare(
    `SELECT COALESCE(NULLIF(cause, ''), 'unknown') AS cause, COUNT(*) AS n
       FROM markers
      WHERE game = ?
      GROUP BY cause
      ORDER BY n DESC
      LIMIT 12`
  )
    .bind(game)
    .all<CauseRow>();

  const recent = await c.env.death_markers.prepare(
    `SELECT COALESCE(NULLIF(cause, ''), 'unknown') AS cause, x, y, z, created_at
       FROM markers
      WHERE game = ?
      ORDER BY created_at DESC
      LIMIT 10`
  )
    .bind(game)
    .all<RecentRow>();

  c.header('Cache-Control', 'public, max-age=60');
  return c.json({
    game,
    total: total?.n ?? 0,
    by_cause: byCause.results ?? [],
    recent: recent.results ?? [],
  });
});

// ---------------------------------------------------------------------------
// Redirects to the canonical surfaces
// ---------------------------------------------------------------------------

// Defaults to the canonical GitHub "latest release" URL — GitHub itself
// resolves it to the most recent release's matching asset, so no API calls
// or polling needed. Override via DOWNLOAD_URL if you need to pin a version.
const LATEST_RELEASE_URL =
  'https://github.com/kem0x/SubnauticaDeathMarkers/releases/latest/download/SubnauticaDeathMarkers.zip';

app.get('/download', (c) => {
  const url = c.env.DOWNLOAD_URL || LATEST_RELEASE_URL;
  return c.redirect(url, 302);
});

app.get('/', (c) => {
  const url = c.env.STATS_PAGE_URL || 'https://ol.mr/subnautica';
  return c.redirect(url, 302);
});

export default app;
