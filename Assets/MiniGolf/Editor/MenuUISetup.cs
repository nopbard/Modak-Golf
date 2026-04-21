using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace MiniGolf.Editor
{
    // Tools > MiniGolf > Setup Menu UI
    // 기존 Menu 씬 Canvas 구조(Screen_MainMenu, Screen_Courses)를 활용.
    // Screen_PlayerSelect만 새로 추가하고 _MenuManager 참조를 자동 연결합니다.
    public static class MenuUISetup
    {
        [MenuItem("Tools/MiniGolf/Setup Menu UI")]
        static void Setup()
        {
            // ── 기존 오브젝트 찾기 ────────────────────────────────────────────
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if(canvas == null)
            {
                Debug.LogError("씬에 Canvas가 없습니다. Menu 씬에서 실행하세요.");
                return;
            }

            Transform canvasT = canvas.transform;

            GameObject mainMenuScreen = canvasT.Find("Screen_MainMenu")?.gameObject;
            GameObject coursesScreen  = canvasT.Find("Screen_Courses")?.gameObject;

            if(mainMenuScreen == null || coursesScreen == null)
            {
                Debug.LogError("Screen_MainMenu 또는 Screen_Courses를 찾지 못했습니다.");
                return;
            }

            MenuManager menuManager = Object.FindAnyObjectByType<MenuManager>();
            if(menuManager == null)
            {
                Debug.LogError("_MenuManager를 씬에서 찾지 못했습니다.");
                return;
            }

            // ── 기존 CoursesButton → OnStartButton으로 교체 ──────────────────
            // 원래 OnCoursesButton을 호출하던 버튼을 OnStartButton으로 변경
            GameObject coursesBtn = mainMenuScreen.transform.Find("CoursesButton")?.gameObject;
            if(coursesBtn != null)
            {
                Button btn = coursesBtn.GetComponent<Button>();
                if(btn != null)
                {
                    SerializedObject btnSO = new SerializedObject(btn);
                    // onClick 리스너를 지우고 새로 연결
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(menuManager.OnStartButton);

                    // 버튼 텍스트 변경
                    TextMeshProUGUI label = coursesBtn.GetComponentInChildren<TextMeshProUGUI>();
                    if(label != null) label.text = "게임 시작";

                    EditorUtility.SetDirty(btn);
                }
            }

            // ── Screen_PlayerSelect 생성 (없으면) ────────────────────────────
            GameObject playerSelectScreen = canvasT.Find("Screen_PlayerSelect")?.gameObject;
            if(playerSelectScreen == null)
            {
                playerSelectScreen = new GameObject("Screen_PlayerSelect");
                playerSelectScreen.transform.SetParent(canvasT, false);

                RectTransform rt = playerSelectScreen.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                // 타이틀
                CreateLabel(playerSelectScreen, "Title", "플레이어 선택",
                    new Vector2(0, 100), new Vector2(300, 60), 32);

                // 1P 버튼
                GameObject btn1P = CreateButton(playerSelectScreen, "1PButton", "1P",
                    new Vector2(-100, 0), new Vector2(160, 80));
                btn1P.GetComponent<Button>().onClick.AddListener(() => menuManager.OnPlayerSelect(1));

                // 2P 버튼
                GameObject btn2P = CreateButton(playerSelectScreen, "2PButton", "2P",
                    new Vector2(100, 0), new Vector2(160, 80));
                btn2P.GetComponent<Button>().onClick.AddListener(() => menuManager.OnPlayerSelect(2));

                // 뒤로 버튼
                GameObject backBtn = CreateButton(playerSelectScreen, "BackButton", "← 뒤로",
                    new Vector2(0, -120), new Vector2(160, 50));
                backBtn.GetComponent<Button>().onClick.AddListener(menuManager.OnBackButton);
            }

            // ── CoursesScreen BackButton 연결 ────────────────────────────────
            GameObject coursesBackBtn = coursesScreen.transform.Find("BackButton")?.gameObject;
            if(coursesBackBtn != null)
            {
                Button bk = coursesBackBtn.GetComponent<Button>();
                if(bk != null)
                {
                    bk.onClick.RemoveAllListeners();
                    bk.onClick.AddListener(menuManager.OnBackButton);
                    EditorUtility.SetDirty(bk);
                }
            }

            // ── MenuManager에 세 화면 연결 ────────────────────────────────────
            SerializedObject so = new SerializedObject(menuManager);
            so.FindProperty("mainMenuScreen").objectReferenceValue    = mainMenuScreen;
            so.FindProperty("playerSelectScreen").objectReferenceValue = playerSelectScreen;
            so.FindProperty("coursesScreen").objectReferenceValue     = coursesScreen;
            so.ApplyModifiedProperties();

            // 시작 시 MainMenu만 활성화
            mainMenuScreen.SetActive(true);
            playerSelectScreen.SetActive(false);
            coursesScreen.SetActive(false);

            EditorUtility.SetDirty(menuManager);
            Debug.Log("Menu UI 설정 완료.");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────────

        static GameObject CreateButton(GameObject parent, string name, string label,
                                       Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent.transform, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            GameObject textGO = new GameObject("Text",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(go.transform, false);
            RectTransform trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 24;
            tmp.color = Color.white;

            return go;
        }

        static void CreateLabel(GameObject parent, string name, string text,
                                Vector2 pos, Vector2 size, float fontSize)
        {
            GameObject go = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
        }
    }
}
