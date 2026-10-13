using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace WaterSystem
{
    /// <summary>
    /// URP 17+ removed ScriptableRenderPass.Configure/Execute.
    /// This feature is kept as a no-op so Viking Village water still loads
    /// without blocking Play Mode compile. Water meshes/materials still render;
    /// the old WaterFX / caustics injection passes are disabled.
    /// </summary>
    public class WaterSystemFeature : ScriptableRendererFeature
    {
        public WaterSystemSettings settings = new WaterSystemSettings();

        [HideInInspector][SerializeField] private Shader causticShader;
        [HideInInspector][SerializeField] private Texture2D causticTexture;

        public override void Create()
        {
            // Intentionally empty for URP 17 compatibility.
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // No custom passes enqueued under URP 17.
        }

        [System.Serializable]
        public class WaterSystemSettings
        {
            [Header("Caustics Settings")]
            [Range(0.1f, 1f)]
            public float causticScale = 0.25f;

            public float causticBlendDistance = 3f;

            [Header("Advanced Settings")]
            public DebugMode debug = DebugMode.Disabled;

            public enum DebugMode
            {
                Disabled,
                WaterEffects,
                Caustics
            }
        }
    }
}
