using System;
using HarmonyLib;

namespace SubnauticaDeathMarkers.Patches
{
    [HarmonyPatch(typeof(Player), nameof(Player.OnKill))]
    internal static class PlayerOnKillPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, DamageType damageType)
        {
            try
            {
                var pos = __instance.transform.position;
                Plugin.Logger.LogInfo(
                    $"Player.OnKill caught. pos=({pos.x:F2}, {pos.y:F2}, {pos.z:F2}) cause={damageType}");
                DeathReporter.ReportDeath(pos, damageType);
                MarkerSpawner.RevealDeath(pos);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"OnKill patch error: {ex}");
            }
        }
    }
}
