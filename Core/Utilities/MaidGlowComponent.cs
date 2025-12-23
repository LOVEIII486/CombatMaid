using UnityEngine;
using System.Collections;

namespace CombatMaid.Core.Utilities
{
    /// <summary>
    /// 角色发光效果
    /// </summary>
    public class MaidGlowComponent : MonoBehaviour
    {
        private Renderer[] _renderers;
        private MaterialPropertyBlock _propBlock;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private Color _currentColor;

        public void Play(Color color)
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _propBlock = new MaterialPropertyBlock();
            _currentColor = color;
            Apply(color);
        }

        public void Stop(float fadeTime)
        {
            StopAllCoroutines();
            StartCoroutine(FadeRoutine(fadeTime));
        }

        private IEnumerator FadeRoutine(float time)
        {
            float elapsed = 0;
            Color startColor = _currentColor;
            while (elapsed < time)
            {
                elapsed += Time.deltaTime;
                Apply(Color.Lerp(startColor, Color.black, elapsed / time));
                yield return null;
            }
            Apply(Color.black);
            Destroy(this);
        }

        private void Apply(Color color)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(EmissionId, color);
                r.SetPropertyBlock(_propBlock);
            }
        }
    }
}