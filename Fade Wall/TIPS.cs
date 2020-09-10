using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

#if UNITY_EDITOR

using UnityEditor.Rendering.HighDefinition;
using UnityEditor;

[CustomPassDrawerAttribute(typeof(TIPS))]
class TIPSEditor : CustomPassDrawer
{
    private class Styles
    {
        public static float DefaultLineSpace = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public static GUIContent EdgeThreshold = new GUIContent("Edge Threshold", "Edge detect effect threshold.");
        public static GUIContent EdgeRadius = new GUIContent("Edge Radius", "Radius of the edge detect effect.");
        public static GUIContent GlowColor = new GUIContent("Color", "Color of the effect");
    }

    SerializedProperty		EdgeDetectThreshold;
    SerializedProperty		EdgeRadius;
    SerializedProperty		GlowColor;

    protected override void Initialize(SerializedProperty customPass)
    {
        EdgeDetectThreshold = customPass.FindPropertyRelative(nameof(TIPS.EdgeDetectThreshold));
        EdgeRadius = customPass.FindPropertyRelative(nameof(TIPS.EdgeRadius));
        GlowColor = customPass.FindPropertyRelative(nameof(TIPS.GlowColor));
    }

    // We only need the name to be displayed, the rest is controlled by the TIPS effect
    protected override PassUIFlag commonPassUIFlags => PassUIFlag.Name;

    protected override void DoPassGUI(SerializedProperty customPass, Rect rect)
    {
        rect.y += Styles.DefaultLineSpace;
        EdgeDetectThreshold.floatValue = EditorGUI.Slider(rect, Styles.EdgeThreshold, EdgeDetectThreshold.floatValue, 0.1f, 5f);
        rect.y += Styles.DefaultLineSpace;
        EdgeRadius.intValue = EditorGUI.IntSlider(rect, Styles.EdgeRadius, EdgeRadius.intValue, 1, 6);
        rect.y += Styles.DefaultLineSpace;
        GlowColor.colorValue = EditorGUI.ColorField(rect, Styles.GlowColor, GlowColor.colorValue, true, false, true);
    }

    protected override float GetPassHeight(SerializedProperty customPass) => Styles.DefaultLineSpace * 6;
}

#endif

class TIPS : CustomPass
{
    public float    EdgeDetectThreshold = 1;
    public int      EdgeRadius = 2;
    public Color    GlowColor = Color.white;


    Material    FullscreenMaterial;
    RTHandle    TtipsBuffer; // additional render target for compositing the custom and camera color buffers

    int         CompositingPass;
    int         BlurPass;

    // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
    // When empty this render pass will render to the active camera render target.
    // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
    // The render pipeline will ensure target setup and clearing happens in a performant manner.
    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        FullscreenMaterial = CoreUtils.CreateEngineMaterial("FullScreen/TIPS");
        TtipsBuffer = RTHandles.Alloc(Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
			colorFormat: GraphicsFormat.R16G16B16A16_SFloat, useDynamicScale: true, name: "TIPS Buffer");

        CompositingPass = FullscreenMaterial.FindPass("Compositing");
        BlurPass = FullscreenMaterial.FindPass("Blur");
        targetColorBuffer = TargetBuffer.Custom;
        targetDepthBuffer = TargetBuffer.Custom;
        clearFlags = ClearFlag.All;
    }

    protected override void Execute(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera camera, CullingResults cullingResult)
    {
        if (FullscreenMaterial == null)
            return ;

        FullscreenMaterial.SetTexture("_TIPSBuffer", TtipsBuffer);
        FullscreenMaterial.SetFloat("_EdgeDetectThreshold", EdgeDetectThreshold);
        FullscreenMaterial.SetColor("_GlowColor", GlowColor);
        FullscreenMaterial.SetFloat("_EdgeRadius", (float)EdgeRadius);
        CoreUtils.SetRenderTarget(cmd, TtipsBuffer, ClearFlag.All);
        CoreUtils.DrawFullScreen(cmd, FullscreenMaterial, shaderPassId: CompositingPass);

        SetCameraRenderTarget(cmd);
        CoreUtils.DrawFullScreen(cmd, FullscreenMaterial, shaderPassId: BlurPass);
    }

    protected override void Cleanup()
    {
        CoreUtils.Destroy(FullscreenMaterial);
        TtipsBuffer.Release();
    }
}