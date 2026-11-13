using System.IO;
using UnityEditor;
using UnityEngine;

namespace CharacterPipeline
{
    // Seam weight weld for an existing skinned mesh asset (e.g. older generated characters that
    // were not built with the pipeline). Backs the asset up next to it in "<name>_Backup~/"
    // (ignored by Unity) before changing it.
    public static class MeshSeamWeld
    {
        [MenuItem("Tools/Character Pipeline/Weld Seam Weights (selected mesh asset)")]
        static void Menu()
        {
            var mesh = Selection.activeObject as UnityEngine.Mesh;
            if (mesh == null)
            {
                EditorUtility.DisplayDialog("Character Pipeline", "Select a skinned Mesh asset (.asset) first.", "OK");
                return;
            }
            Debug.Log("[CharacterPipeline] " + WeldMeshAsset(mesh, true));
        }

        public static string WeldMeshAsset(UnityEngine.Mesh mesh, bool backup)
        {
            string path = AssetDatabase.GetAssetPath(mesh);
            if (!path.EndsWith(".asset"))
                return $"{path}: only standalone .asset meshes can be edited (meshes inside a model file are re-imported).";

            string backupPath = null;
            if (backup)
            {
                string folder = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "_Backup~");
                Directory.CreateDirectory(folder);
                backupPath = Path.Combine(folder, Path.GetFileName(path)).Replace('\\', '/');
                File.Copy(Path.GetFullPath(path), Path.GetFullPath(backupPath), true);
                File.Copy(Path.GetFullPath(path + ".meta"), Path.GetFullPath(backupPath + ".meta"), true);
            }

            BoneWeight[] weights = mesh.boneWeights;
            int differing = HemWeights.WeldSeams(mesh.vertices, weights);
            mesh.boneWeights = weights;
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            return $"{path}: {differing} seam positions had differing weights (averaged)" + (backupPath != null ? $"; backup {backupPath}" : "");
        }
    }
}
