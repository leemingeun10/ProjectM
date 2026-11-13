using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterPipeline
{
    // Step 7: matte enemy material on corrected texture copies. The generated texture and material
    // stay untouched; everything is written to Enemy/ next to the config.
    public static class MaterialStandard
    {
        public static string OutputFolder(CharacterPipelineConfig config)
        {
            return Path.GetDirectoryName(AssetDatabase.GetAssetPath(config)).Replace('\\', '/');
        }

        public static string EnemyFolder(CharacterPipelineConfig config)
        {
            string folder = OutputFolder(config) + "/Enemy";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(OutputFolder(config), "Enemy");
            return folder;
        }

        public static string MaterialPath(CharacterPipelineConfig config) => EnemyFolder(config) + "/" + config.outputName + "_Enemy.mat";

        public static string Run(CharacterPipelineConfig config)
        {
            var smr = config.modelPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Material source = smr.sharedMaterial;
            string colorPath = BindMesh.TexturePath(source, "_BaseMap") ?? BindMesh.TexturePath(source, "_MainTex");
            string normalPath = BindMesh.TexturePath(source, "_BumpMap");
            string folder = EnemyFolder(config);
            var report = new StringBuilder("Material:\n");

            Texture2D original = BindMesh.ReadPixelsFromFile(colorPath);
            Texture2D corrected = BindMesh.ReadPixelsFromFile(colorPath);
            int w = original.width, h = original.height;
            BindMesh mesh = BindMesh.Load(config.modelPrefab);
            float meanBefore = CoveredMeanLuminance(mesh, original);

            if (config.brightenTrousers)
                report.Append(BrightenTrousers(config, mesh, original, corrected));
            if (config.paintBelt)
                report.Append(PaintBelt(config, mesh, original, corrected));
            float meanAfter = CoveredMeanLuminance(mesh, corrected);
            report.Append($" mean luminance (UV-covered texels) {meanBefore:F3} -> {meanAfter:F3}\n");
            // Generated textures have almost no padding around UV islands; at 1024 with mipmaps the
            // empty texels bleed in as light seams, so grow every island outwards.
            bool[] covered = CoveredMask(mesh, w, h);
            int padded = Dilate(corrected, covered, PaddingPixels(w));
            corrected.Apply();
            report.Append($" island padding: {PaddingPixels(w)} px, {padded} texels filled\n");

            string colorOut = folder + "/" + config.outputName + "_Color_Enemy.png";
            File.WriteAllBytes(Path.GetFullPath(colorOut), corrected.EncodeToPNG());
            Object.DestroyImmediate(original);
            Object.DestroyImmediate(corrected);
            AssetDatabase.ImportAsset(colorOut);
            ApplyImportSettings(colorOut, config.maxTextureSize, config.colorFormat, false);

            string normalOut = null;
            if (normalPath != null)
            {
                normalOut = folder + "/" + config.outputName + "_Normal_Enemy.png";
                Texture2D normal = BindMesh.ReadPixelsFromFile(normalPath);
                bool[] normalCovered = CoveredMask(mesh, normal.width, normal.height);
                Dilate(normal, normalCovered, PaddingPixels(normal.width));
                normal.Apply();
                File.WriteAllBytes(Path.GetFullPath(normalOut), normal.EncodeToPNG());
                Object.DestroyImmediate(normal);
                AssetDatabase.ImportAsset(normalOut);
                ApplyImportSettings(normalOut, config.maxTextureSize, config.normalFormat, true);
            }

            string matPath = MaterialPath(config);
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, matPath);
            }
            else
            {
                material.CopyPropertiesFromMaterial(source);
            }
            var colorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(colorOut);
            material.SetTexture("_BaseMap", colorTex);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", colorTex);
            if (normalOut != null)
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalOut));
            material.SetFloat("_Smoothness", config.smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", config.smoothness);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            report.Append($" {matPath}: smoothness {config.smoothness}, textures {config.maxTextureSize} ({config.colorFormat} / {config.normalFormat} on Android), source {w}x{h}\n");
            return report.ToString();
        }

        static string BrightenTrousers(CharacterPipelineConfig config, BindMesh mesh, Texture2D original, Texture2D corrected)
        {
            int w = original.width, h = original.height;
            var mask = new bool[w * h];
            for (int t = 0; t < mesh.TriangleCount; t++)
            {
                float hf = mesh.HeightFraction(mesh.TriangleCentroid(t));
                if (hf < config.trousersBand.x || hf > config.trousersBand.y)
                    continue;
                Vector2 uvc = mesh.TriangleUvCentroid(t);
                if (PipelineMath.Luminance(original.GetPixelBilinear(uvc.x, uvc.y)) > config.trousersMaxLuminance)
                    continue;
                mesh.RasterizeTriangle(t, w, h, 1, (x, y) => mask[y * w + x] = true);
            }
            double sum = 0;
            int n = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i])
                    continue;
                sum += PipelineMath.Luminance(original.GetPixel(i % w, i / w));
                n++;
            }
            if (n == 0)
                return " trousers: no texels found\n";
            float mean = (float)(sum / n);
            float gain = Mathf.Min(config.trousersMaxGain, config.trousersTargetLuminance / Mathf.Max(mean, 1e-3f));
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i])
                    continue;
                Color c = original.GetPixel(i % w, i / w);
                corrected.SetPixel(i % w, i / w, new Color(Mathf.Clamp01(c.r * gain), Mathf.Clamp01(c.g * gain), Mathf.Clamp01(c.b * gain), c.a));
            }
            return $" trousers: {n} texels, mean luminance {mean:F3} x{gain:F2} -> {Mathf.Min(1f, mean * gain):F3}\n";
        }

        static string PaintBelt(CharacterPipelineConfig config, BindMesh mesh, Texture2D original, Texture2D corrected)
        {
            int w = original.width, h = original.height;
            var mask = new bool[w * h];
            int tris = 0;
            for (int t = 0; t < mesh.TriangleCount; t++)
            {
                Vector3 c = mesh.TriangleCentroid(t);
                float hf = mesh.HeightFraction(c);
                if (hf < config.beltBand.x || hf > config.beltBand.y)
                    continue;
                if (Mathf.Abs(c.x) > config.torsoHalfWidth * mesh.Height)
                    continue;
                Vector2 uvc = mesh.TriangleUvCentroid(t);
                if (PipelineMath.Luminance(original.GetPixelBilinear(uvc.x, uvc.y)) > config.beltMaxLuminance)
                    continue;
                tris++;
                mesh.RasterizeTriangle(t, w, h, 1, (x, y) => mask[y * w + x] = true);
            }
            double sum = 0;
            int n = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i])
                    continue;
                sum += PipelineMath.Luminance(original.GetPixel(i % w, i / w));
                n++;
            }
            if (n == 0)
                return " belt: no triangles found\n";
            float mean = (float)(sum / n);
            // Keep the cloth's shading detail: scale the belt colour by each texel's luminance relative to the mean.
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i])
                    continue;
                Color o = original.GetPixel(i % w, i / w);
                float k = PipelineMath.Luminance(o) / Mathf.Max(mean, 1e-3f);
                Color b = config.beltColor * k;
                corrected.SetPixel(i % w, i / w, new Color(Mathf.Clamp01(b.r), Mathf.Clamp01(b.g), Mathf.Clamp01(b.b), o.a));
            }
            return $" belt: {tris} triangles, {n} texels, colour {config.beltColor}\n";
        }

        static int PaddingPixels(int textureSize) => Mathf.Max(4, textureSize / 128);

        static bool[] CoveredMask(BindMesh mesh, int w, int h)
        {
            var mask = new bool[w * h];
            for (int t = 0; t < mesh.TriangleCount; t++)
                mesh.RasterizeTriangle(t, w, h, 0, (x, y) => mask[y * w + x] = true);
            return mask;
        }

        // Grows covered texels outwards by `iterations` rings (each new texel = mean of covered neighbours).
        public static int Dilate(Texture2D texture, bool[] covered, int iterations)
        {
            int w = texture.width, h = texture.height, filled = 0;
            Color[] px = texture.GetPixels();
            var done = (bool[])covered.Clone();
            var next = new List<int>();
            for (int it = 0; it < iterations; it++)
            {
                next.Clear();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (done[i])
                            continue;
                        Color sum = Color.clear;
                        int n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= w || ny >= h)
                                    continue;
                                int j = ny * w + nx;
                                if (!done[j])
                                    continue;
                                sum += px[j];
                                n++;
                            }
                        }
                        if (n > 0)
                        {
                            px[i] = sum / n;
                            next.Add(i);
                        }
                    }
                }
                foreach (int i in next)
                    done[i] = true;
                filled += next.Count;
            }
            texture.SetPixels(px);
            return filled;
        }

        static float CoveredMeanLuminance(BindMesh mesh, Texture2D texture)
        {
            int w = texture.width, h = texture.height;
            bool[] mask = CoveredMask(mesh, w, h);
            double sum = 0;
            int n = 0;
            Color[] px = texture.GetPixels();
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i])
                    continue;
                sum += PipelineMath.Luminance(px[i]);
                n++;
            }
            return n == 0 ? 0f : (float)(sum / n);
        }

        public static void ApplyImportSettings(string path, int maxSize, TextureImporterFormatSetting format, bool normalMap)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.maxTextureSize = maxSize;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = maxSize;
            android.format = ToImporterFormat(format);
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        static TextureImporterFormat ToImporterFormat(TextureImporterFormatSetting f)
        {
            switch (f)
            {
                case TextureImporterFormatSetting.ASTC_4x4: return TextureImporterFormat.ASTC_4x4;
                case TextureImporterFormatSetting.ASTC_5x5: return TextureImporterFormat.ASTC_5x5;
                case TextureImporterFormatSetting.ASTC_8x8: return TextureImporterFormat.ASTC_8x8;
                default: return TextureImporterFormat.ASTC_6x6;
            }
        }
    }
}
