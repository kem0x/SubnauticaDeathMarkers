using UnityEngine;

namespace SubnauticaDeathMarkers
{
    internal class DeathMarker : MonoBehaviour
    {
        public string Cause;

        private Vector3 _baseScale;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        public void SetAlpha(float a)
        {
            transform.localScale = _baseScale * Mathf.Clamp01(a);
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                foreach (var c in Camera.allCameras)
                {
                    if (c != null && c.enabled && c.gameObject.activeInHierarchy)
                    {
                        cam = c;
                        break;
                    }
                }
            }
            if (cam == null) return;

            var dir = cam.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
        }
    }
}
