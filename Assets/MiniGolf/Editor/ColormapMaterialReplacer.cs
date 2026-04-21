using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MiniGolf.Editor
{
    // Tools > MiniGolf > Colormap Material Replacer
    // FBX 안에 임베드된 "colormap" 머터리얼을 외부 머터리얼로 일괄 교체.
    // 1. Replace Material 슬롯에 카툰 머터리얼 할당
    // 2. Search Folder 설정 (기본 Assets/)
    // 3. "Run Replace" 클릭
    public class ColormapMaterialReplacer : EditorWindow
    {
        private Material replaceMaterial;
        private string materialName   = "colormap";
        private string searchFolder   = "Assets/MiniGolf/Models/Course Parts";
        private bool   previewOnly    = true;
        private Vector2 scroll;
        private List<string> results  = new();

        [MenuItem("Tools/MiniGolf/Colormap Material Replacer")]
        static void Open() => GetWindow<ColormapMaterialReplacer>("Colormap Replacer");

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Colormap Material Replacer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "FBX 안에 임베드된 머터리얼을 외부 머터리얼로 일괄 교체합니다.\n" +
                "같은 이름의 머터리얼은 하나의 외부 파일로 합쳐져 모든 FBX가 공유합니다.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "⚠️ Search Folder를 반드시 대상 폴더로 좁히세요.\n" +
                "Assets 전체로 설정하면 구매한 에셋 FBX까지 리맵됩니다.",
                MessageType.Warning);

            EditorGUILayout.Space(6);
            materialName    = EditorGUILayout.TextField("Material Name to Replace", materialName);
            replaceMaterial = (Material)EditorGUILayout.ObjectField(
                "Replace With (Cartoon Material)", replaceMaterial, typeof(Material), false);
            searchFolder    = EditorGUILayout.TextField("Search Folder", searchFolder);

            EditorGUILayout.Space(4);
            previewOnly = EditorGUILayout.Toggle("Preview Only (dry-run)", previewOnly);

            EditorGUILayout.Space(8);

            using(new EditorGUI.DisabledScope(replaceMaterial == null && !previewOnly))
            {
                if(GUILayout.Button(previewOnly ? "Preview (no changes)" : "Run Replace", GUILayout.Height(32)))
                    Execute();
            }

            if(replaceMaterial == null && !previewOnly)
                EditorGUILayout.HelpBox("Replace Material을 먼저 지정하세요.", MessageType.Warning);

            if(results.Count > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField($"결과 ({results.Count}개 FBX)", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(300));
                foreach(var line in results)
                    EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
            }
        }

        void Execute()
        {
            results.Clear();

            // 검색 폴더 안 모든 FBX 찾기
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { searchFolder });
            int changed = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach(string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if(importer == null) continue;

                    // 이 FBX 안에 대상 머터리얼이 있는지 확인
                    var remap = importer.GetExternalObjectMap();
                    bool found = false;

                    // 임베드된 오브젝트 목록에서 머터리얼 이름 검색
                    foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if(asset is Material mat && mat.name == materialName)
                        {
                            found = true;
                            break;
                        }
                    }

                    // External remap에 이미 등록된 경우도 확인
                    if(!found)
                    {
                        foreach(var kv in remap)
                        {
                            if(kv.Key.type == typeof(Material) && kv.Key.name == materialName)
                            {
                                found = true;
                                break;
                            }
                        }
                    }

                    if(!found) continue;

                    results.Add($"{(previewOnly ? "[PREVIEW]" : "[CHANGED]")} {path}");
                    changed++;

                    if(!previewOnly && replaceMaterial != null)
                    {
                        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), materialName);
                        importer.AddRemap(id, replaceMaterial);
                        importer.SaveAndReimport();
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if(!previewOnly)
            {
                AssetDatabase.Refresh();
                results.Add($"── 완료: {changed}개 FBX 교체됨 ──");
                Debug.Log($"[ColormapReplacer] {changed}개 FBX의 '{materialName}' 머터리얼을 '{replaceMaterial?.name}'으로 교체 완료.");
            }
            else
            {
                results.Add($"── 프리뷰: {changed}개 FBX에서 '{materialName}' 발견 ──");
            }
        }
    }
}
