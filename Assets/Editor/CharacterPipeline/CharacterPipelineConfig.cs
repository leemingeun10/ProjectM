using UnityEngine;

namespace CharacterPipeline
{
    // One asset per character. Outputs are written next to this asset:
    //   <outputName>_Mesh.asset            mesh copy with the hem weights moved to the hips
    //   Enemy/<outputName>_Color_Enemy.png corrected colour texture (belt, trousers)
    //   Enemy/<outputName>_Normal_Enemy.png normal map copy (1024 / ASTC)
    //   Enemy/<outputName>_Enemy.mat        standard matte material
    //   <outputName>_Fist.prefab            battle prefab (variant of the rigged model prefab)
    // Heights below are fractions of the model's height, measured from the soles in the bind pose.
    [CreateAssetMenu(menuName = "Character Pipeline/Config", fileName = "CharacterPipelineConfig")]
    public class CharacterPipelineConfig : ScriptableObject
    {
        [Header("Input")]
        [Tooltip("Prefab produced by Unity AI GenerateMesh + RigMesh (Tripo biped rig).")]
        public GameObject modelPrefab;
        public RuntimeAnimatorController animatorController;
        public string outputName = "Character";

        [Header("Scale")]
        [Tooltip("Mesh height (soles to top of the hair) in the corrected T-pose, metres.")]
        public float targetHeight = 1.744f;

        [Header("Material")]
        public float smoothness = 0.15f;
        public int maxTextureSize = 1024;
        public TextureImporterFormatSetting colorFormat = TextureImporterFormatSetting.ASTC_6x6;
        public TextureImporterFormatSetting normalFormat = TextureImporterFormatSetting.ASTC_4x4;

        [Header("Belt repaint (leave paintBelt off for characters without one)")]
        public bool paintBelt = true;
        public Color beltColor = new Color(0.33f, 0.16f, 0.11f);
        public Vector2 beltBand = new Vector2(0.548f, 0.595f);
        [Tooltip("Only texels darker than this (original texture) count as belt, so robe flaps over the band stay.")]
        public float beltMaxLuminance = 0.42f;
        public float torsoHalfWidth = 0.15f;

        [Header("Trousers brightening")]
        public bool brightenTrousers = true;
        public Vector2 trousersBand = new Vector2(0.06f, 0.43f);
        public float trousersMaxLuminance = 0.40f;
        public float trousersTargetLuminance = 0.38f;
        public float trousersMaxGain = 1.6f;

        [Header("Mesh fixes")]
        [Tooltip("Average the weights of split vertices along UV seams (the auto-rig weights them independently, so the skin cracks).")]
        public bool weldSeamWeights = true;
        public bool moveHemWeights = true;
        [Tooltip("Vertices brighter than this in the original texture count as robe (the trousers are darker).")]
        public float hemRobeMinLuminance = 0.40f;
        [Tooltip("Top of the hem region; robe vertices above this keep their weights.")]
        public float hemTop = 0.56f;
        [Tooltip("Share of the leg weight moved to the hips at the hem bottom (1 at hemTop).")]
        [Range(0f, 1f)] public float hemBottomHipShare = 0.7f;

        [Header("Sword socket")]
        public float socketGridStep = 0.002f;
        [Tooltip("Blade centre in front of the socket (the TempSword cube is 0.8 m long, centred 0.4 m out).")]
        public float swordForwardOffset = 0.4f;

        [Header("Results (written by the tools)")]
        public float measuredHeightBeforeScale;
        public float appliedScale;
        public float idleHeightOffset;
        public Vector3 socketLocalPosition;
        public Quaternion socketLocalRotation = Quaternion.identity;
        public bool fistHoleThrough;
        [TextArea(4, 30)] public string lastReport;
    }

    // Mirrors the few TextureImporterFormat values we use, so the config stays editor-agnostic.
    public enum TextureImporterFormatSetting
    {
        ASTC_4x4,
        ASTC_5x5,
        ASTC_6x6,
        ASTC_8x8
    }
}
