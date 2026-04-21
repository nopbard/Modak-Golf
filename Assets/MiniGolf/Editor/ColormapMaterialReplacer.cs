using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MiniGolf.Editor
{
    // Tools > MiniGolf > Colormap Material Replacer
    // Step 1: FBX/OBJ 임베드 머터리얼 → 외부 .mat 추출
    //   - sharedMaterial ON: 첫 매칭 모델에서 1개만 추출 (OBJ 처럼 145개 모두 같은 머터리얼 공유하는 경우)
    //   - sharedMaterial OFF: 매칭되는 모든 모델에서 각각 추출
    // Step 2: 추출된 머터리얼 셰이더 교체 (텍스처 슬롯은 유지)
    // Step 3: 프리팹에 추출된 머터리얼 일괄 적용
    // Step 4: 모델의 Material Remap 에 일괄 적용 (OBJ/FBX importer ExternalObjects)
    public class ColormapMaterialReplacer : EditorWindow
    {
        private Texture2D targetTexture;
        private Shader    cartoonShader;
        private string    textureSlot       = "_BaseMap";
        private string    searchFolder      = "Assets/MiniGolf";
        private string    extractFolder     = "Assets/MiniGolf/Materials/Extracted";
        private string    prefabFolder      = "Assets/MiniGolf/Prefabs/Course Parts";
        private bool      previewOnly       = true;
        private bool      sharedMaterial    = false;                    // OBJ 패키지처럼 모든 모델이 같은 머터리얼 공유할 때
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
            EditorGUILayout.LabelField("Step 1 — FBX/OBJ에서 머터리얼 추출", EditorStyles.boldLabel);
            searchFolder  = EditorGUILayout.TextField("Search Folder", searchFolder);
            extractFolder = EditorGUILayout.TextField("Extract To", extractFolder);
            sharedMaterial = EditorGUILayout.Toggle(
                new GUIContent("Shared Material",
                    "ON이면 첫 매칭 모델에서 .mat 1개만 추출. OBJ Kenney 처럼 모든 모델이 같은 머터리얼 공유할 때 사용."),
                sharedMaterial);
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

            // ── Step 4 ──────────────────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Step 4 — 모델 Material Remap 적용", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "OBJ/FBX 의 ModelImporter ExternalObjects 에 추출된 머터리얼을 연결합니다.\n" +
                "모델 자체는 수정 안 하고 importer 설정만 바뀌므로 Kenney 같은 서드파티 에셋에도 안전.",
                MessageType.Info);
            using(new EditorGUI.DisabledScope(targetTexture == null))
            {
                if(GUILayout.Button(previewOnly ? "Step 4: Preview" : "Step 4: Apply to Model Remaps", GUILayout.Height(26)))
                    ApplyToModelRemaps();
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
            bool done = false;
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { searchFolder });

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach(string guid in guids)
                {
                    if(done) break;
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

                        if(!previewOnly)
                        {
                            if(File.Exists(destPath))
                            {
                                results[^1] += " (skipped, exists)";
                            }
                            else
                            {
                                string err = AssetDatabase.ExtractAsset(asset, destPath);
                                if(!string.IsNullOrEmpty(err)) results[^1] += $" ⚠ {err}";
                            }
                        }

                        if(sharedMaterial) { done = true; break; }       // 첫 매칭에서 종료
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

        // ── Step 4 ────────────────────────────────────────────────────────
        // 각 모델의 ModelImporter.ExternalObjects 에 추출된 머터리얼을 remap.
        // 모델 파일 자체(.obj/.fbx)는 수정 안 하므로 Kenney 같은 서드파티 에셋에 안전.
        void ApplyToModelRemaps()
        {
            results.Clear();

            Material extractedMat = FindExtractedMaterial();
            if(extractedMat == null)
            {
                results.Add("⚠ Extract 폴더에서 colormap 머터리얼을 찾지 못했습니다. Step 1을 먼저 실행하세요.");
                return;
            }
            results.Add($"사용할 머터리얼: {AssetDatabase.GetAssetPath(extractedMat)}");

            int modelCount = 0, remapCount = 0;
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { searchFolder });

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach(string guid in guids)
                {
                    string modelPath = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                    if(importer == null) continue;

                    bool modelChanged = false;
                    foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                    {
                        if(asset is not Material m) continue;

                        // 이 sub-material 이 target texture 를 쓰는지 (또는 이름이 colormap 인지) 확인
                        bool matches = false;
                        foreach(var slot in m.GetTexturePropertyNames())
                            if(m.GetTexture(slot) == targetTexture) { matches = true; break; }
                        if(!matches && m.name == "colormap") matches = true;
                        if(!matches) continue;

                        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name);
                        results.Add($"  {(previewOnly ? "[PREVIEW]" : "[REMAP]")} {modelPath} → {m.name}");
                        remapCount++;
                        if(previewOnly) continue;

                        importer.AddRemap(id, extractedMat);
                        modelChanged = true;
                    }

                    if(!previewOnly && modelChanged)
                    {
                        importer.SaveAndReimport();
                        modelCount++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                if(!previewOnly) AssetDatabase.Refresh();
            }

            results.Add($"── {(previewOnly ? "프리뷰" : "완료")}: {modelCount}개 모델, {remapCount}개 remap ──");
        }
    }
}
