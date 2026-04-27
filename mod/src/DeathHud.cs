using UnityEngine;

namespace SubnauticaDeathMarkers
{
    internal class DeathHud : MonoBehaviour
    {
        public string Title;
        public string Subtitle;
        public float  Alpha;

        private GUIStyle _titleStyle;
        private GUIStyle _subStyle;

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle
                {
                    fontSize  = 56,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                };
                _titleStyle.normal.textColor = Color.white;
            }
            if (_subStyle == null)
            {
                _subStyle = new GUIStyle
                {
                    fontSize  = 22,
                    alignment = TextAnchor.MiddleCenter,
                };
                _subStyle.normal.textColor = new Color(1.0f, 0.85f, 0.2f);
            }
        }

        private void OnGUI()
        {
            if (Alpha <= 0f) return;
            EnsureStyles();

            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Alpha));

            int w = Screen.width, h = Screen.height;
            if (!string.IsNullOrEmpty(Title))
                GUI.Label(new Rect(0, h * 0.42f, w, 80), Title, _titleStyle);
            if (!string.IsNullOrEmpty(Subtitle))
                GUI.Label(new Rect(0, h * 0.50f, w, 40), Subtitle, _subStyle);

            GUI.color = prev;
        }
    }
}
