using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SubnauticaDeathMarkers
{
    internal static class DeathReporter
    {
        public static void ReportDeath(Vector3 position, DamageType damageType)
        {
            var json = BuildJson(position, damageType);
            Plugin.Logger.LogInfo($"Reporting death: {json}");
            Plugin.Instance.StartCoroutine(PostMarker(json));
        }

        private static IEnumerator PostMarker(string json)
        {
            var url = $"{Plugin.ApiBaseUrl.Value.TrimEnd('/')}/markers";
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 5;

                yield return req.SendWebRequest();

                if (req.isNetworkError || req.isHttpError)
                {
                    Plugin.Logger.LogWarning(
                        $"POST /markers failed: {req.error} (status {req.responseCode})");
                }
                else
                {
                    Plugin.Logger.LogInfo($"POST /markers ok: {req.downloadHandler.text}");
                }
            }
        }

        private static string BuildJson(Vector3 position, DamageType damageType)
        {
            var ic = CultureInfo.InvariantCulture;
            return "{"
                + $"\"game\":\"{Plugin.GameId.Value}\","
                + $"\"x\":{position.x.ToString("R", ic)},"
                + $"\"y\":{position.y.ToString("R", ic)},"
                + $"\"z\":{position.z.ToString("R", ic)},"
                + $"\"cause\":\"{damageType.ToString().ToLowerInvariant()}\""
                + "}";
        }
    }
}
