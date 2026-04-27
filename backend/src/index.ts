import { Hono } from 'hono';

type Bindings = {
  DB: D1Database;
  CHUNK_SIZE: string;
  FETCH_LIMIT: string;
  DOWNLOAD_URL?: string;
  REPO_URL?: string;
};

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

// ---------------------------------------------------------------------------
// API: marker write/read used by the mod
// ---------------------------------------------------------------------------

app.post('/markers', async (c) => {
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

  const chunkSize = Number(c.env.CHUNK_SIZE) || 200;
  const cx = Math.floor(x / chunkSize);
  const cy = Math.floor(y / chunkSize);
  const cz = Math.floor(z / chunkSize);

  const cause = body.cause?.slice(0, 64) ?? null;
  const note = body.note?.slice(0, 280) ?? null;

  await c.env.DB.prepare(
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

  const { results } = await c.env.DB.prepare(
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
// Public stats — JSON for embedding, HTML for portfolio link
// ---------------------------------------------------------------------------

async function loadStats(c: any, game: string) {
  const total = await c.env.DB.prepare(
    'SELECT COUNT(*) AS n FROM markers WHERE game = ?'
  )
    .bind(game)
    .first<{ n: number }>();

  const byCause = await c.env.DB.prepare(
    `SELECT COALESCE(NULLIF(cause, ''), 'unknown') AS cause, COUNT(*) AS n
       FROM markers
      WHERE game = ?
      GROUP BY cause
      ORDER BY n DESC
      LIMIT 12`
  )
    .bind(game)
    .all<CauseRow>();

  const recent = await c.env.DB.prepare(
    `SELECT COALESCE(NULLIF(cause, ''), 'unknown') AS cause, x, y, z, created_at
       FROM markers
      WHERE game = ?
      ORDER BY created_at DESC
      LIMIT 10`
  )
    .bind(game)
    .all<RecentRow>();

  return {
    game,
    total: total?.n ?? 0,
    by_cause: byCause.results ?? [],
    recent: recent.results ?? [],
  };
}

app.get('/api/stats', async (c) => {
  const game = c.req.query('game') || 'subnautica';
  const stats = await loadStats(c, game);
  c.header('Cache-Control', 'public, max-age=60');
  return c.json(stats);
});

app.get('/download', (c) => {
  const url = c.env.DOWNLOAD_URL || c.env.REPO_URL || 'https://example.invalid';
  return c.redirect(url, 302);
});

app.get('/', async (c) => {
  const game = 'subnautica';
  const stats = await loadStats(c, game);
  const downloadUrl = c.env.DOWNLOAD_URL || '';
  const repoUrl = c.env.REPO_URL || '';
  c.header('Cache-Control', 'public, max-age=60');
  return c.html(renderPage(stats, downloadUrl, repoUrl));
});

export default app;

// ---------------------------------------------------------------------------
// HTML rendering
// ---------------------------------------------------------------------------

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function relTime(ts: number): string {
  const ms = Date.now() - ts;
  const sec = Math.max(1, Math.floor(ms / 1000));
  if (sec < 60) return `${sec}s ago`;
  const min = Math.floor(sec / 60);
  if (min < 60) return `${min}m ago`;
  const hr = Math.floor(min / 60);
  if (hr < 24) return `${hr}h ago`;
  const day = Math.floor(hr / 24);
  if (day < 30) return `${day}d ago`;
  const mo = Math.floor(day / 30);
  return `${mo}mo ago`;
}

function renderPage(
  stats: {
    game: string;
    total: number;
    by_cause: CauseRow[];
    recent: RecentRow[];
  },
  downloadUrl: string,
  repoUrl: string
): string {
  const max = Math.max(1, ...stats.by_cause.map((r) => r.n));

  const causeRows = stats.by_cause
    .map(
      (r) => `
      <div class="row cause">
        <span class="cause-name">${escapeHtml(r.cause)}</span>
        <span class="bar"><span class="bar-fill" style="width:${((r.n / max) * 100).toFixed(1)}%"></span></span>
        <span class="num">${r.n}</span>
      </div>`
    )
    .join('');

  const recentRows = stats.recent
    .map(
      (r) => `
      <div class="row recent">
        <span class="r-cause">${escapeHtml(r.cause)}</span>
        <span class="r-where">(${r.x.toFixed(0)}, ${r.y.toFixed(0)}, ${r.z.toFixed(0)})</span>
        <span class="r-when">${relTime(r.created_at)}</span>
      </div>`
    )
    .join('');

  const downloadCta = downloadUrl
    ? `<a class="cta" href="${escapeHtml(downloadUrl)}">Download mod</a>`
    : '';
  const repoLink = repoUrl
    ? `<a class="link" href="${escapeHtml(repoUrl)}">Source</a>`
    : '';

  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Death Markers — communal memorials in Subnautica</title>
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="description" content="A Subnautica mod that records every player's death and reveals memorials to others when they're about to die.">
<meta property="og:title" content="Death Markers">
<meta property="og:description" content="A Subnautica mod that gives single-player games a communal memory.">
<style>
  :root {
    --bg: #0e1116;
    --card: #151b23;
    --line: #1f2731;
    --fg: #e6e6e6;
    --muted: #8b95a5;
    --accent: #ffd83a;
  }
  * { box-sizing: border-box; }
  html, body { margin: 0; padding: 0; }
  body {
    background: var(--bg);
    color: var(--fg);
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", system-ui, sans-serif;
    line-height: 1.55;
    padding: 4rem 1.5rem;
    -webkit-font-smoothing: antialiased;
  }
  main { max-width: 760px; margin: 0 auto; }
  header { display: flex; align-items: center; gap: 1rem; margin-bottom: 0.5rem; }
  header svg { flex: 0 0 auto; }
  h1 { font-size: 2.25rem; margin: 0; letter-spacing: -0.02em; }
  .tagline { color: var(--muted); margin: 0 0 3rem; }
  .stat-block {
    background: var(--card);
    border: 1px solid var(--line);
    border-radius: 12px;
    padding: 2rem;
    margin-bottom: 1rem;
  }
  .big { font-size: 4.5rem; font-weight: 600; color: var(--accent); line-height: 1; font-variant-numeric: tabular-nums; }
  .big-label { color: var(--muted); margin-top: 0.5rem; }
  h2 {
    font-size: 0.85rem;
    color: var(--muted);
    font-weight: 600;
    margin: 2.5rem 0 1rem;
    text-transform: uppercase;
    letter-spacing: 0.08em;
  }
  .row { padding: 0.55rem 0; border-bottom: 1px solid var(--line); }
  .row:last-child { border-bottom: none; }
  .cause {
    display: grid;
    grid-template-columns: 9rem 1fr 3rem;
    gap: 1rem;
    align-items: center;
  }
  .cause-name { font-size: 0.95rem; }
  .bar { height: 8px; background: var(--line); border-radius: 4px; overflow: hidden; }
  .bar-fill { display: block; height: 100%; background: var(--accent); }
  .num { color: var(--muted); text-align: right; font-variant-numeric: tabular-nums; }
  .recent {
    display: grid;
    grid-template-columns: 9rem 1fr auto;
    gap: 1rem;
    align-items: center;
    font-family: ui-monospace, "SF Mono", Menlo, monospace;
    font-size: 0.875rem;
  }
  .r-cause { color: var(--accent); }
  .r-where { color: var(--muted); }
  .r-when { color: var(--muted); text-align: right; }
  .cta {
    display: inline-block;
    margin-top: 2.5rem;
    padding: 0.85rem 1.5rem;
    background: var(--accent);
    color: #0e1116;
    text-decoration: none;
    border-radius: 8px;
    font-weight: 600;
  }
  .cta:hover { filter: brightness(1.1); }
  .link { color: var(--accent); margin-left: 1rem; text-decoration: none; }
  .link:hover { text-decoration: underline; }
  footer { color: var(--muted); margin-top: 4rem; font-size: 0.875rem; }
  footer a { color: inherit; }
  .empty { color: var(--muted); font-style: italic; }
  @media (max-width: 540px) {
    body { padding: 2rem 1rem; }
    .big { font-size: 3rem; }
    .cause, .recent { grid-template-columns: auto 1fr auto; }
  }
</style>
</head>
<body>
  <main>
    <header>
      <svg width="40" height="56" viewBox="0 0 40 56" aria-hidden="true">
        <rect x="18" y="0" width="4" height="56" fill="#ffd83a"/>
        <rect x="6" y="14" width="28" height="4" fill="#ffd83a"/>
      </svg>
      <h1>Death Markers</h1>
    </header>
    <p class="tagline">Communal memorials in single-player games.</p>

    <div class="stat-block">
      <div class="big">${stats.total}</div>
      <div class="big-label">deaths recorded in Subnautica</div>
    </div>

    <h2>How they died</h2>
    ${stats.by_cause.length ? causeRows : '<p class="empty">No deaths yet. Be the first.</p>'}

    <h2>Recent</h2>
    ${stats.recent.length ? recentRows : '<p class="empty">No deaths yet.</p>'}

    <div>
      ${downloadCta}
      ${repoLink}
    </div>

    <footer>
      Built for Wand Hackweek · 2026.
      Stats refresh every minute.
    </footer>
  </main>
</body>
</html>`;
}
