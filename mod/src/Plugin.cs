using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SubnauticaDeathMarkers
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid    = "com.kareem.deathmarkers";
        public const string PluginName    = "Death Markers";
        public const string PluginVersion = "1.0.0";

        internal static Plugin Instance { get; private set; }
        internal static new ManualLogSource Logger;
        internal static ConfigEntry<string> ApiBaseUrl;
        internal static ConfigEntry<string> GameId;

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;

            ApiBaseUrl = Config.Bind(
                "Network",
                "ApiBaseUrl",
                "https://death-markers-api.kareemolim.workers.dev",
                "Base URL of the Death Markers API.");

            GameId = Config.Bind(
                "Network",
                "GameId",
                "subnautica",
                "Game id sent with each marker.");

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded. API: {ApiBaseUrl.Value}");

            new Harmony(PluginGuid).PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo("Harmony patches applied.");

            MarkerSpawner.Begin();
        }

        private const float LowOxygenTrigger  = 3f;   // seconds remaining
        private const float LowOxygenClear    = 6f;   // hysteresis: reset latch above this
        private bool _lowOxygenLatched;

        private void Update()
        {
            // Debug: F2 = full death reveal, F3 = low-oxygen pinned reveal, F4 = recover.
            if (Player.main != null)
            {
                if (Input.GetKeyDown(KeyCode.F2))
                {
                    Logger.LogInfo("[debug] F2 → death reveal.");
                    MarkerSpawner.RevealDeath(Player.main.transform.position);
                }
                else if (Input.GetKeyDown(KeyCode.F3))
                {
                    Logger.LogInfo("[debug] F3 → low-oxygen reveal.");
                    MarkerSpawner.RevealLowOxygen(Player.main.transform.position);
                }
                else if (Input.GetKeyDown(KeyCode.F4))
                {
                    Logger.LogInfo("[debug] F4 → recover.");
                    MarkerSpawner.RecoverOxygen();
                }
            }

            // Reveal markers as oxygen runs out (no HUD); fade out when oxygen recovers.
            if (Player.main != null && Player.main.oxygenMgr != null && Player.main.IsAlive())
            {
                float oxy = Player.main.oxygenMgr.GetOxygenAvailable();
                if (!_lowOxygenLatched && oxy <= LowOxygenTrigger)
                {
                    _lowOxygenLatched = true;
                    Logger.LogInfo($"Oxygen low ({oxy:F1}s) — revealing markers.");
                    MarkerSpawner.RevealLowOxygen(Player.main.transform.position);
                }
                else if (_lowOxygenLatched && oxy >= LowOxygenClear)
                {
                    _lowOxygenLatched = false;
                    Logger.LogInfo($"Oxygen recovered ({oxy:F1}s) — fading markers.");
                    MarkerSpawner.RecoverOxygen();
                }
            }
        }
    }
}
