using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CharacterPipeline
{
    // Menu entry points. Select a CharacterPipelineConfig asset, then run the steps in order
    // (or "Run All"). Each step appends to the config's lastReport and logs it.
    public static class CharacterPipelineMenu
    {
        const string Root = "Tools/Character Pipeline/";

        static CharacterPipelineConfig Selected()
        {
            var config = Selection.activeObject as CharacterPipelineConfig;
            if (config == null)
                EditorUtility.DisplayDialog("Character Pipeline", "Select a CharacterPipelineConfig asset first.", "OK");
            return config;
        }

        public static string DefaultCaptureDir(CharacterPipelineConfig config)
        {
            return Path.GetFullPath(Path.Combine("Logs", "CharacterPipeline", config.outputName));
        }

        static void Report(CharacterPipelineConfig config, string text, bool append)
        {
            config.lastReport = append ? config.lastReport + text : text;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterPipeline] " + config.outputName + "\n" + text);
        }

        [MenuItem(Root + "1. Import Humanoid")]
        static void MenuImport() { var c = Selected(); if (c) Report(c, HumanoidSetup.ImportHumanoid(c), false); }

        [MenuItem(Root + "2. Enforce T-Pose")]
        static void MenuTPose() { var c = Selected(); if (c) { HumanoidSetup.EnforceTPose(c, out string r); Report(c, r, true); } }

        [MenuItem(Root + "3. Standard Material")]
        static void MenuMaterial() { var c = Selected(); if (c) Report(c, MaterialStandard.Run(c), true); }

        [MenuItem(Root + "4. Mesh Fixes (seam weld, hem weights)")]
        static void MenuHem() { var c = Selected(); if (c) Report(c, HemWeights.Run(c), true); }

        [MenuItem(Root + "5. Build Battle Prefab (socket, idle offset)")]
        static void MenuPrefab() { var c = Selected(); if (c) Report(c, BattlePrefab.Build(c), true); }

        [MenuItem(Root + "6. Inspect (report + captures)")]
        static void MenuInspect() { var c = Selected(); if (c) Report(c, CharacterInspector.Run(c, DefaultCaptureDir(c)), true); }

        [MenuItem(Root + "Run All")]
        static void MenuAll() { var c = Selected(); if (c) RunAll(c, DefaultCaptureDir(c)); }

        public static string RunAll(CharacterPipelineConfig config, string captureDir)
        {
            var sb = new StringBuilder();
            sb.Append(HumanoidSetup.ImportHumanoid(config));
            HumanoidSetup.EnforceTPose(config, out string tpose);
            sb.Append(tpose);
            sb.Append(MaterialStandard.Run(config));
            sb.Append(HemWeights.Run(config));
            sb.Append(BattlePrefab.Build(config));
            sb.Append(CharacterInspector.Run(config, captureDir));
            Report(config, sb.ToString(), false);
            return sb.ToString();
        }
    }
}
