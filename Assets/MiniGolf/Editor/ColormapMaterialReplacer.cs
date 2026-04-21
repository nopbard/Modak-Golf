using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MiniGolf.Editor
{
    // Tools > MiniGolf > Colormap Material Replacer
    // Step 1: FBX 임베드 머터리얼 → 외부 .mat 추출
    // Step 2: 추출된 머터리얼 셰이더 교체
    // Step 3: 프리팹에 추출된 머터리얼 일괄 적용
    public class ColormapMaterialReplacer : EditorWindow
    {
        private Texture2D targetTexture;
        private Shader    cartoonShader;
        private string    textureSlot    = "_BaseMap";
        private string    searchFolder   = "Assets/MiniGolf";
        private string    extractFolder  = "Assets/MiniGolf/Materials/Extracted";
        private string    prefabFolder   = "Assets/MiniGolf/Prefabs/Course Parts";
        private bool      previewOnly    = true;
        private Vector2   scroll;
        private List<string> results = new();

        [MenuItem("Tools/MiniGolf/Colormap Material Replacer")]
        static void Open() => GetWindow<ColormapMaterialReplacer>("Colormap Replacer");

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Colormap Material Replacer", EditorStyles.boldLabel);

            EditorGUILayout.Space(4);
            previewOnly   = EditorGUILayout.Toggle("Preview Only (dry-run)", previewOnly);
            targetTexture = (Texture2D)EditorGUILayout.ObjectField("Colormap Texture", targetTexture, typeof(Texture2D), false);

            if(targetTexture == null)
                EditorGUILayout.HelpBox("Colormap Texture를 먼저 지정하세요.", MessageType.Warning);

            // ── Step 1 ──────────────────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Step 1 — FBX에서 머터리얼 추출", EditorStyles.boldLabel);
            searchFolder  = EditorGUILayout.TextField("Search Folder", searchFolder);
            extractFolder = EditorGUILayout.TextField("Extract To", extractFolder);
            using(new EditorGUI.DisabledScope(targetTexture == null))
            {
                if(GUILayout.Button(previewOnly ? "Step 1: Preview" : "Step 1: Extract", GUILayout.Height(26)))
                    ExtractEmbeddedMaterials();
            }

            // ── Step 2 ──────────────────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Step 2 — 셰이더 교체", EditorStyles.boldLabel);
            cartoonShader = (Shader)EditorGUILayout.ObjectField("Cartoon Shader", cartoonShader, typeof(Shader), false);
            textureSlot   = EditorGUILayout.TextField("Texture Slot Name", textureSlot);
            bool canStep2 = targetTexture != null && (previewOnly || cartoonShader != null);
            using(new EditorGUI.DisabledScope(!canStep2))
            {
                if(GUILayout.Button(previewOnly ? "Step 2: Preview" : "Step 2: Replace Shader", GUILayout.Height(26)))
                    ReplaceShader();
            }

            // ── Step 3 ──────────────────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Step 3 — 프리팹에 머터리얼 적용", EditorStyles.boldLabel);
            prefabFolder  = EditorGUILayout.TextField("Prefab Folder", prefabFolder);
            using(new EditorGUI.DisabledScope(targetTexture == null))
            {
                if(GUILayout.Button(previewOnly ? "Step 3: Preview" : "Step 3: Apply to Prefabs", GUILayout.Height(26)))
                    ApplyToPrefabs();
            }

            // ── 결과 로그 ────────────────────────────────────────────────
            if(results.Count > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("결과", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(250));
                foreach(var line in results)
                    EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
            }
        }

        // ── Step 1 ────────────────────────────────────────────────────────
        void ExtractEmbeddedMaterials()
        {
            results.Clear();
            if(!previewOnly)
                Directory.CreateDirectory(extractFolder);

            int count = 0;
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { searchFolder });

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach(string guid in guids)
                {
                    string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
                    foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                    {
                        if(asset is not Material mat) continue;
                        bool uses = false;
                        foreach(var slot in mat.GetTexturePropertyNames())
                            if(mat.GetTexture(slot) == targetTexture) { uses = true; break; }
                        if(!uses) continue;

                        string destPath = $"{extractFolder}/{mat.name}.mat";
                        results.Add($"{(previewOnly ? "[PREVIEW]" : "[EXTRACT]")} {fbxPath} → {destPath}");
                        count++;
                        if(previewOnly) continue;
                        if(File.Exists(destPath)) { results[^1] += " (skipped, exists)"; continue; }
                        string err = AssetDatabase.ExtractAsset(asset, destPath);
                        if(!string.IsNullOrEmpty(err)) results[^1] += $" ⚠ {err}";
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                if(!previewOnly) AssetDatabase.Refresh();
            }
            results.Add($"── {(previewOnly ? "프리뷰" : "완료")}: {count}개 ──");
        }

        // ── Step 2 ────────────────────────────────────────────────────────
        void ReplaceShader()
        {
            results.Clear();
            int count = 0;
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { extractFolder, searchFolder });
            foreach(string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat == null) continue;
                bool uses = false;
                foreach(var slot in mat.GetTexturePropertyNames())
                    if(mat.GetTexture(slot) == targetTexture) { uses = true; break; }
                if(!uses) continue;

                results.Add($"{(previewOnly ? "[PREVIEW]" : "[CHANGED]")} {path} ({mat.shader.name})");
                count++;
                if(previewOnly || cartoonShader == null) continue;

                Undo.RecordObject(mat, "Replace Cartoon Shader");
                mat.shader = cartoonShader;
                if(mat.HasProperty(textureSlot))
                    mat.SetTexture(textureSlot, targetTexture);
                EditorUtility.SetDirty(mat);
            }
            if(!previewOnly) AssetDatabase.SaveAssets();
            results.Add($"── {(previewOnly ? "프리뷰" : "완료")}: {count}개 ──");
        }

        // ── Step 3 ────────────────────────────────────────────────────────
        void ApplyToPrefabs()
        {
            results.Clear();

            // Extract 폴더에서 colormap 텍스처를 쓰는 추출된 머터리얼 찾기
            Material extractedMat = FindExtractedMaterial();
            if(extractedMat == null)
            {
                results.Add("⚠ Extract 폴더에서 colormap 머터리얼을 찾지 못했습니다. Step 1을 먼저 실행하세요.");
                return;
            }
            results.Add($"사용할 머터리얼: {AssetDatabase.GetAssetPath(extractedMat)}");

            int prefabCount = 0, rendererCount = 0;
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });

            foreach(string guid in guids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);

                // PrefabUtility로 prefab contents를 임시 로드
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                bool changed = false;

                foreach(var renderer in contents.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = renderer.sharedMaterials;
                    for(int i = 0; i < mats.Length; i++)
                    {
                        if(mats[i] == null) continue;
                        bool isColormap = mats[i].name == "colormap";
                        if(!isColormap)
                        {
                            foreach(var slot in mats[i].GetTexturePropertyNames())
                                if(mats[i].GetTexture(slot) == targetTexture) { isColormap = true; break; }
                        }
                        if(!isColormap) continue;

                        results.Add($"  {(previewOnly ? "[PREVIEW]" : "[APPLY]")} {prefabPath} → {renderer.name} [slot {i}]");
                        rendererCount++;
                        if(!previewOnly) mats[i] = extractedMat;
                        changed = true;
                    }
                    if(!previewOnly && changed)
                        renderer.sharedMaterials = mats;
                }

                if(!previewOnly && changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    prefabCount++;
                }
                PrefabUtility.UnloadPrefabContents(contents);
            }

            results.Add($"── {(previewOnly ? "프리뷰" : "완료")}: {prefabCount}개 프리팹, {rendererCount}개 렌더러 ──");
            if(!previewOnly) AssetDatabase.Refresh();
        }

        Material FindExtractedMaterial()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { extractFolder });
            foreach(string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat == null) continue;
                foreach(var slot in mat.GetTexturePropertyNames())
                    if(mat.GetTexture(slot) == targetTexture) return mat;
                if(mat.name == "colormap") return mat;
            }
            return null;
        }
    }
}
