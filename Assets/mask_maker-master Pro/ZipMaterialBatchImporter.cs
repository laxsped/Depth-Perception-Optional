#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

public class ZipMaterialBatchImporter : EditorWindow
{
    private const string MaterialsRoot = "Assets/материалы";
    private const string LitShaderName = "HDRP/Lit";

    private bool createHdrpMaterial = true;
    private bool createLitMaskTexture = true;
    private bool deleteSourceZip = false;
    private Vector2 scroll;
    private string logText = string.Empty;

    [MenuItem("Инструменты/Импорт материалов из ZIP (HDRP)")]
    public static void Open()
    {
        ZipMaterialBatchImporter wnd = GetWindow<ZipMaterialBatchImporter>("ZIP -> HDRP Material");
        wnd.minSize = new Vector2(640f, 420f);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Пакетный импорт ZIP материалов", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Ищет ZIP в Assets/материалы, распаковывает в папки по имени архива, оставляет только нужные карты (Color/Normal/AO/Roughness/Metallic/Height), удаляет остальное. Опционально создает HDRP/Lit материал.",
            MessageType.Info);

        createHdrpMaterial = EditorGUILayout.ToggleLeft("Создавать HDRP/Lit материал", createHdrpMaterial);
        createLitMaskTexture = EditorGUILayout.ToggleLeft("Создавать LitMask (R=Metal G=AO B=Detail A=Smooth)", createLitMaskTexture);
        deleteSourceZip = EditorGUILayout.ToggleLeft("Удалять ZIP после успешного импорта", deleteSourceZip);

        EditorGUILayout.Space(6);
        if (GUILayout.Button("Запустить импорт", GUILayout.Height(30f)))
        {
            RunImport();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Лог", EditorStyles.boldLabel);
        using (var view = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = view.scrollPosition;
            EditorGUILayout.TextArea(logText, GUILayout.ExpandHeight(true));
        }
    }

    private void RunImport()
    {
        logText = string.Empty;
        if (!AssetDatabase.IsValidFolder(MaterialsRoot))
        {
            Append("Папка не найдена: " + MaterialsRoot);
            return;
        }

        string absRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", MaterialsRoot)).Replace('\\', '/');
        if (!Directory.Exists(absRoot))
        {
            Append("Путь не найден: " + absRoot);
            return;
        }

        string[] zipFiles = Directory.GetFiles(absRoot, "*.zip", SearchOption.TopDirectoryOnly);
        if (zipFiles.Length == 0)
        {
            Append("ZIP архивы не найдены в " + MaterialsRoot);
            return;
        }

        int success = 0;
        for (int i = 0; i < zipFiles.Length; i++)
        {
            string zipPath = zipFiles[i];
            try
            {
                ProcessZip(zipPath, absRoot);
                success++;
            }
            catch (Exception ex)
            {
                Append("[ERROR] " + Path.GetFileName(zipPath) + " -> " + ex.Message);
            }
        }

        AssetDatabase.Refresh();
        Append("Готово: " + success + " / " + zipFiles.Length);
    }

    private void ProcessZip(string zipPath, string absRoot)
    {
        string zipName = Path.GetFileNameWithoutExtension(zipPath);
        string targetAbsDir = Path.Combine(absRoot, zipName);
        string targetAssetDir = MaterialsRoot + "/" + zipName;

        if (Directory.Exists(targetAbsDir))
        {
            Directory.Delete(targetAbsDir, true);
        }
        Directory.CreateDirectory(targetAbsDir);

        using (ZipArchive archive = ZipFile.OpenRead(zipPath))
        {
            for (int i = 0; i < archive.Entries.Count; i++)
            {
                ZipArchiveEntry entry = archive.Entries[i];
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                string ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                if (!IsImageExt(ext))
                {
                    continue;
                }

                string fileName = SanitizeFileName(entry.Name);
                string outPath = Path.Combine(targetAbsDir, fileName);
                entry.ExtractToFile(outPath, true);
            }
        }

        AssetDatabase.Refresh();
        KeepOnlyTargetTextures(targetAbsDir);
        AssetDatabase.Refresh();

        if (createHdrpMaterial)
        {
            CreateHdrpLitMaterial(targetAssetDir, zipName);
        }

        if (deleteSourceZip)
        {
            File.Delete(zipPath);
        }

        Append("[OK] " + Path.GetFileName(zipPath) + " -> " + targetAssetDir);
    }

    private static void KeepOnlyTargetTextures(string targetAbsDir)
    {
        string[] files = Directory.GetFiles(targetAbsDir, "*.*", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (!IsImageExt(ext))
            {
                SafeDelete(file);
                continue;
            }

            string n = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            if (!LooksLikeTargetTexture(n))
            {
                SafeDelete(file);
            }
        }
    }

    private static bool LooksLikeTargetTexture(string name)
    {
        return ContainsAny(name, "color", "albedo", "diffuse", "basecolor", "base_color")
               || ContainsAny(name, "normal", "normaldx")
               || ContainsAny(name, "ambientocclusion", "ao", "occlusion")
               || ContainsAny(name, "roughness", "smoothness", "gloss")
               || ContainsAny(name, "metallic", "metalness")
               || ContainsAny(name, "height", "displacement");
    }

    private static bool IsImageExt(string ext)
    {
        return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".tif" || ext == ".tiff" || ext == ".exr";
    }

    private static bool ContainsAny(string src, params string[] needles)
    {
        for (int i = 0; i < needles.Length; i++)
        {
            if (src.Contains(needles[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return Path.GetFileName(name);
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored by design
        }
    }

    private void CreateHdrpLitMaterial(string folderAssetPath, string materialName)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderAssetPath });
        if (guids == null || guids.Length == 0)
        {
            Append("  Материал не создан: текстуры не найдены");
            return;
        }

        Texture2D color = null;
        Texture2D normal = null;
        Texture2D ao = null;
        Texture2D smoothOrRough = null;
        bool smoothSourceIsRoughness = false;
        Texture2D metallic = null;
        Texture2D detail = null;

        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (tex == null)
            {
                continue;
            }

            if (color == null && ContainsAny(n, "color", "albedo", "diffuse", "basecolor", "base_color"))
            {
                color = tex;
                continue;
            }

            if (normal == null && ContainsAny(n, "normal", "normaldx"))
            {
                normal = tex;
                continue;
            }

            if (ao == null && ContainsAny(n, "ambientocclusion", "ao", "occlusion"))
            {
                ao = tex;
                continue;
            }

            if (smoothOrRough == null && ContainsAny(n, "roughness", "smoothness", "gloss"))
            {
                smoothOrRough = tex;
                smoothSourceIsRoughness = ContainsAny(n, "roughness");
                continue;
            }

            if (metallic == null && ContainsAny(n, "metallic", "metalness"))
            {
                metallic = tex;
                continue;
            }

            if (detail == null && ContainsAny(n, "detail"))
            {
                detail = tex;
            }
        }

        Shader shader = Shader.Find(LitShaderName);
        if (shader == null)
        {
            Append("  HDRP/Lit не найден. Проверь проектный pipeline.");
            return;
        }

        string matPath = folderAssetPath + "/" + materialName + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else
        {
            mat.shader = shader;
        }

        if (color != null && mat.HasProperty("_BaseColorMap"))
        {
            mat.SetTexture("_BaseColorMap", color);
        }

        if (normal != null)
        {
            string normalPath = AssetDatabase.GetAssetPath(normal);
            TextureImporter importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }

            if (mat.HasProperty("_NormalMap"))
            {
                mat.SetTexture("_NormalMap", normal);
            }
        }

        if (ao != null && mat.HasProperty("_OcclusionMap"))
        {
            mat.SetTexture("_OcclusionMap", ao);
        }

        if (metallic != null && mat.HasProperty("_MetallicMap"))
        {
            mat.SetTexture("_MetallicMap", metallic);
        }

        Texture2D litMask = null;
        if (createLitMaskTexture)
        {
            litMask = CreateLitMaskTexture(folderAssetPath, materialName, metallic, ao, detail, smoothOrRough, smoothSourceIsRoughness);
        }

        if (litMask != null && mat.HasProperty("_MaskMap"))
        {
            mat.SetTexture("_MaskMap", litMask);
        }
        else if (smoothOrRough != null && mat.HasProperty("_MaskMap"))
        {
            mat.SetTexture("_MaskMap", smoothOrRough);
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Append("  Материал: " + matPath);
    }

    private Texture2D CreateLitMaskTexture(
        string folderAssetPath,
        string materialName,
        Texture2D metallic,
        Texture2D ao,
        Texture2D detail,
        Texture2D smoothOrRough,
        bool smoothSourceIsRoughness)
    {
        int width = 0;
        int height = 0;
        TryTakeSize(metallic, ref width, ref height);
        TryTakeSize(ao, ref width, ref height);
        TryTakeSize(detail, ref width, ref height);
        TryTakeSize(smoothOrRough, ref width, ref height);

        if (width <= 0 || height <= 0)
        {
            return null;
        }

        Color[] metalPx = SampleTexture(metallic, width, height);
        Color[] aoPx = SampleTexture(ao, width, height);
        Color[] detailPx = SampleTexture(detail, width, height);
        Color[] smoothPx = SampleTexture(smoothOrRough, width, height);

        Texture2D outTex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        Color[] outPx = new Color[width * height];

        for (int i = 0; i < outPx.Length; i++)
        {
            float r = metalPx != null ? metalPx[i].r : 0f;
            float g = aoPx != null ? aoPx[i].r : 1f;
            float b = detailPx != null ? detailPx[i].r : 0f;
            float a = 0.5f;

            if (smoothPx != null)
            {
                a = smoothSourceIsRoughness ? 1f - smoothPx[i].r : smoothPx[i].r;
            }

            outPx[i] = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), Mathf.Clamp01(a));
        }

        outTex.SetPixels(outPx);
        outTex.Apply(false, false);

        string fileName = materialName + "_LitMask.png";
        string assetPath = folderAssetPath + "/" + fileName;
        string absPath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), assetPath).Replace('\\', '/');
        File.WriteAllBytes(absPath, outTex.EncodeToPNG());
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
        }

        Append("  LitMask: " + assetPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    private static void TryTakeSize(Texture2D tex, ref int width, ref int height)
    {
        if (tex == null)
        {
            return;
        }

        if (width <= 0 || height <= 0)
        {
            width = tex.width;
            height = tex.height;
        }
    }

    private static Color[] SampleTexture(Texture2D source, int width, int height)
    {
        if (source == null)
        {
            return null;
        }

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture prev = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;

        Texture2D temp = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        temp.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        temp.Apply(false, false);
        Color[] px = temp.GetPixels();

        UnityEngine.Object.DestroyImmediate(temp);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return px;
    }

    private void Append(string line)
    {
        logText += line + Environment.NewLine;
        Repaint();
    }
}
#endif
