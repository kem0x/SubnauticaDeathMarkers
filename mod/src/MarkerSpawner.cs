using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace SubnauticaDeathMarkers
{
    internal class MarkerDto
    {
        public float x;
        public float y;
        public float z;
        public string cause;
        public string note;
        public long created_at;
    }

    internal class MarkersResponse
    {
        public MarkerDto[] markers;
    }

    internal static class MarkerSpawner
    {
        private const int   ChunkSize     = 200;
        private const float RevealRadius  = 100f;
        private const float FadeInTime    = 0.4f;
        private const float HudFadeIn     = 0.5f;
        private const float HoldSeconds   = 5.0f;
        private const float FadeOutTime   = 1.5f;

        private enum State { Hidden, FadingIn, PinnedAlive, DeathHold, FadingOut }

        private static State _state = State.Hidden;
        private static bool                  _fetched;
        private static GameObject            _root;
        private static DeathHud              _hud;
        private static readonly List<DeathMarker> _markers   = new List<DeathMarker>();
        private static readonly List<DeathMarker> _revealed  = new List<DeathMarker>();
        private static Coroutine             _routine;

        public static void Begin()
        {
            if (_fetched) return;
            Plugin.Instance.StartCoroutine(WaitAndFetch());
        }

        // Pre-death warning: markers visible, no HUD, stays pinned until cleared.
        public static void RevealLowOxygen(Vector3 pos)
        {
            if (_markers.Count == 0) return;
            if (_state == State.PinnedAlive || _state == State.DeathHold) return;
            EnsureHud();
            StopRoutine();
            _routine = Plugin.Instance.StartCoroutine(LowOxygenRoutine(pos));
        }

        // Crisis averted: fade out from pinned state.
        public static void RecoverOxygen()
        {
            if (_state != State.PinnedAlive && _state != State.FadingIn) return;
            StopRoutine();
            _routine = Plugin.Instance.StartCoroutine(FadeOutRoutine());
        }

        // Full death reveal: markers + HUD text, hold, then auto-fade.
        public static void RevealDeath(Vector3 pos)
        {
            if (_markers.Count == 0) return;
            EnsureHud();
            StopRoutine();
            _routine = Plugin.Instance.StartCoroutine(DeathRoutine(pos));
        }

        private static void StopRoutine()
        {
            if (_routine != null) Plugin.Instance.StopCoroutine(_routine);
            _routine = null;
        }

        private static void EnsureHud()
        {
            if (_hud != null) return;
            var go = new GameObject("DeathMarkersHud");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _hud = go.AddComponent<DeathHud>();
        }

        private static IEnumerator WaitAndFetch()
        {
            while (Player.main == null)
                yield return null;
            yield return new WaitForSeconds(2f);

            if (_fetched) yield break;
            _fetched = true;

            var pos = Player.main.transform.position;
            int cx = Mathf.FloorToInt(pos.x / ChunkSize);
            int cy = Mathf.FloorToInt(pos.y / ChunkSize);
            int cz = Mathf.FloorToInt(pos.z / ChunkSize);

            var url = $"{Plugin.ApiBaseUrl.Value.TrimEnd('/')}/markers"
                    + $"?game={UnityWebRequest.EscapeURL(Plugin.GameId.Value)}"
                    + $"&chunk={cx},{cy},{cz}";

            Plugin.Logger.LogInfo(
                $"Fetching markers around player ({pos.x:F1},{pos.y:F1},{pos.z:F1}) chunk=({cx},{cy},{cz})");

            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 8;
                yield return req.SendWebRequest();

                if (req.isNetworkError || req.isHttpError)
                {
                    Plugin.Logger.LogWarning(
                        $"GET /markers failed: {req.error} (status {req.responseCode})");
                    yield break;
                }

                var body = req.downloadHandler.text ?? "";
                MarkersResponse resp;
                try
                {
                    resp = JsonConvert.DeserializeObject<MarkersResponse>(body);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Failed to parse markers: {ex}");
                    yield break;
                }

                if (resp?.markers == null)
                {
                    Plugin.Logger.LogWarning("GET /markers returned no markers field.");
                    yield break;
                }

                Plugin.Logger.LogInfo($"Got {resp.markers.Length} markers; spawning hidden.");
                _root = new GameObject("DeathMarkersRoot");

                int spawned = 0;
                foreach (var m in resp.markers)
                {
                    var marker = SpawnMarker(m, _root.transform);
                    if (marker != null) _markers.Add(marker);
                    if (spawned == 0 && marker != null)
                    {
                        var rend = marker.GetComponentInChildren<Renderer>();
                        var sh = rend?.sharedMaterial?.shader?.name ?? "<none>";
                        Plugin.Logger.LogInfo($"First marker shader: {sh}");
                    }
                    spawned++;
                    if (spawned % 10 == 0) yield return null;
                }

                Plugin.Logger.LogInfo($"Spawned {spawned} death markers (hidden, awaiting trigger).");
            }
        }

        private static DeathMarker SpawnMarker(MarkerDto m, Transform parent)
        {
            var go = new GameObject(string.IsNullOrEmpty(m.cause) ? "DeathMarker" : $"DeathMarker_{m.cause}");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = new Vector3(m.x, m.y, m.z);

            var color  = ColorForCause(m.cause);
            var shader = Shader.Find("GUI/Text Shader") ?? Shader.Find("Unlit/Color");

            // Memorial cross: vertical pole + horizontal crossguard near the top.
            BuildBar(go.transform, Vector3.zero,                    new Vector3(0.25f, 2.5f, 0.25f), color, shader);
            BuildBar(go.transform, new Vector3(0f, 0.6f, 0f),       new Vector3(1.4f,  0.25f, 0.25f), color, shader);

            var dm = go.AddComponent<DeathMarker>();
            dm.Cause = m.cause;
            go.SetActive(false);
            return dm;
        }

        private static void BuildBar(Transform parent, Vector3 localPos, Vector3 localScale, Color color, Shader shader)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.transform.SetParent(parent, worldPositionStays: false);
            bar.transform.localPosition = localPos;
            bar.transform.localScale    = localScale;

            var collider = bar.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            var rend = bar.GetComponent<Renderer>();
            if (shader != null)
            {
                rend.material.shader = shader;
                if (rend.material.HasProperty("_Color"))
                    rend.material.SetColor("_Color", color);
            }
            else
            {
                rend.material.color = color;
            }
        }

        private static Color ColorForCause(string cause)
        {
            if (string.IsNullOrEmpty(cause)) return new Color(1.0f, 0.85f, 0.2f);
            switch (cause.ToLowerInvariant())
            {
                case "heat":
                case "fire":
                case "explosive":  return new Color(1.0f, 0.40f, 0.20f); // red-orange
                case "pressure":   return new Color(0.65f, 0.30f, 1.0f); // crush purple
                case "cold":       return new Color(0.45f, 0.85f, 1.0f); // ice cyan
                case "acid":
                case "poison":     return new Color(0.35f, 1.0f,  0.45f); // bio green
                case "electrical": return new Color(0.55f, 0.75f, 1.0f);  // electric blue
                case "radiation":  return new Color(0.75f, 1.0f,  0.30f); // radioactive
                case "puncture":
                case "collide":
                case "drill":      return new Color(1.0f, 0.55f, 0.30f); // angry orange
                case "starve":     return new Color(0.85f, 0.70f, 0.50f); // pale tan
                default:           return new Color(1.0f, 0.85f, 0.20f); // amber default
            }
        }

        private static void BuildRevealed(Vector3 pos)
        {
            _revealed.Clear();
            float r2 = RevealRadius * RevealRadius;
            foreach (var dm in _markers)
            {
                if (dm == null) continue;
                if ((dm.transform.position - pos).sqrMagnitude > r2) continue;
                dm.gameObject.SetActive(true);
                dm.SetAlpha(0f);
                _revealed.Add(dm);
            }
            Plugin.Logger.LogInfo($"Revealing {_revealed.Count} markers within {RevealRadius}m of ({pos.x:F1},{pos.y:F1},{pos.z:F1}).");
        }

        private static IEnumerator FadeMarkersIn()
        {
            float t = 0f;
            while (t < FadeInTime)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / FadeInTime);
                foreach (var dm in _revealed)
                    if (dm != null) dm.SetAlpha(a);
                yield return null;
            }
            foreach (var dm in _revealed)
                if (dm != null) dm.SetAlpha(1f);
        }

        private static IEnumerator FadeAllOut()
        {
            float t = 0f;
            while (t < FadeOutTime)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(t / FadeOutTime);
                if (_hud != null) _hud.Alpha = a;
                foreach (var dm in _revealed)
                    if (dm != null) dm.SetAlpha(a);
                yield return null;
            }
            if (_hud != null)
            {
                _hud.Alpha = 0f;
                _hud.Title = null;
                _hud.Subtitle = null;
            }
            foreach (var dm in _revealed)
                if (dm != null) { dm.SetAlpha(0f); dm.gameObject.SetActive(false); }
            _revealed.Clear();
        }

        private static IEnumerator LowOxygenRoutine(Vector3 pos)
        {
            _state = State.FadingIn;
            BuildRevealed(pos);
            yield return FadeMarkersIn();
            _state = State.PinnedAlive;
            // Stay visible. Externally transitioned by RecoverOxygen or RevealDeath.
        }

        private static IEnumerator FadeOutRoutine()
        {
            _state = State.FadingOut;
            yield return FadeAllOut();
            _state = State.Hidden;
        }

        private static IEnumerator DeathRoutine(Vector3 deathPos)
        {
            _state = State.DeathHold;
            // If markers aren't already up (e.g. instant death without low-oxygen warning),
            // build the reveal set now and fade them in. Otherwise reuse the pinned set.
            if (_revealed.Count == 0)
            {
                BuildRevealed(deathPos);
                yield return FadeMarkersIn();
            }

            _hud.Title = _revealed.Count == 1
                ? "1 OTHER DIED HERE"
                : $"{_revealed.Count} OTHERS DIED HERE";
            _hud.Subtitle = $"within {RevealRadius:F0}m";

            // Fade HUD in (markers stay at full).
            float t = 0f;
            while (t < HudFadeIn)
            {
                t += Time.unscaledDeltaTime;
                _hud.Alpha = Mathf.Clamp01(t / HudFadeIn);
                yield return null;
            }
            _hud.Alpha = 1f;

            yield return new WaitForSecondsRealtime(HoldSeconds);

            yield return FadeAllOut();
            _state = State.Hidden;
        }
    }
}
