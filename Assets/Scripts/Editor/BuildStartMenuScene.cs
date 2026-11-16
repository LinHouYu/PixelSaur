#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PixelSaur.UI;

namespace PixelSaur.Editor
{
    /// <summary>
    /// 创建并配置双场景架构：
    /// 1. 在 Game.unity 的 HUD_Canvas 上挂载 ResponsiveCanvasAdapter（不同分辨率自适应）；
    /// 2. 构建并保存开始大厅场景 Assets/Scenes/StartMenu.unity（3D 霸王龙展示台、多分辨率 SafeArea 自适应、
    ///    顶部头像/货币/齿轮占位栏、面板管理器 UIPanelManager、异步加载跳转 Game 场景、共享齿轮设置体系）；
    /// 3. 将 StartMenu.unity (0) 与 Game.unity (1) 注册至 EditorBuildSettings.scenes。
    /// </summary>
    public static class BuildStartMenuScene
    {
        private const string StartScenePath = "Assets/Scenes/StartMenu.unity";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("PixelSaur/生成并配置双场景 (StartMenu + Game)")]
        public static string Main()
        {
            // 1. 为 Game.unity 的 HUD_Canvas 补充多分辨率自适应组件（不改任何受保护参数与节点坐标）
            Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            GameObject hudCanvas = GameObject.Find("HUD_Canvas");
            if (hudCanvas != null)
            {
                ResponsiveCanvasAdapter adapter = hudCanvas.GetComponent<ResponsiveCanvasAdapter>();
                if (adapter == null) adapter = hudCanvas.AddComponent<ResponsiveCanvasAdapter>();
                adapter.Configure(hudCanvas.GetComponent<CanvasScaler>(), null);
                EditorUtility.SetDirty(hudCanvas);
            }
            EditorSceneManager.SaveScene(gameScene);

            // 2. 创建全新的 StartMenu.unity
            Scene startScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironmentAndPreviewStage();
            BuildEventSystem();
            BuildStartMenuCanvas();

            bool savedStart = EditorSceneManager.SaveScene(startScene, StartScenePath);

            // 3. 配置 Build Settings 双场景顺序：0=StartMenu, 1=Game
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(StartScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"OK: StartSaved={savedStart}, BuildScenes=2 (StartMenu=0, Game=1)";
        }

        private static void BuildEnvironmentAndPreviewStage()
        {
            // Camera
            GameObject camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0.4f, 1.85f, -5.2f);
            camObj.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            Camera cam = camObj.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.11f, 0.24f, 0.31f, 1f);
            cam.fieldOfView = 42f;

            // Lighting
            GameObject lightObj = new GameObject("Directional Light", typeof(Light));
            lightObj.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light dirLight = lightObj.GetComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.25f;
            dirLight.color = new Color(1f, 0.97f, 0.90f, 1f);

            // 像素草地展示台
            GameObject stageBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stageBase.name = "PreviewIslandPlatform";
            stageBase.transform.position = new Vector3(-0.9f, -0.35f, 0f);
            stageBase.transform.localScale = new Vector3(4.6f, 0.7f, 4.6f);
            Renderer baseRen = stageBase.GetComponent<Renderer>();
            if (baseRen != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.32f, 0.56f, 0.24f, 1f);
                baseRen.sharedMaterial = mat;
            }

            // 3D 霸王龙展示模型（待机动画）
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/霸王龙/Minecraft_T_Rex.fbx");
            if (fbxPrefab != null)
            {
                GameObject dinoInst = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
                dinoInst.name = "LobbyTRexPreview";
                dinoInst.transform.position = new Vector3(-0.9f, 0.0f, 0f);
                dinoInst.transform.rotation = Quaternion.Euler(0f, -28f, 0f);
                dinoInst.transform.localScale = Vector3.one * 0.95f;

                Animator anim = dinoInst.GetComponent<Animator>();
                if (anim == null) anim = dinoInst.AddComponent<Animator>();
                RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Model/霸王龙/TRexAnimatorController.controller");
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;

                Material healthyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Model/霸王龙/M_TRex_Healthy.mat");
                if (healthyMat != null)
                {
                    foreach (Renderer r in dinoInst.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] mats = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++) mats[i] = healthyMat;
                        r.sharedMaterials = mats;
                    }
                }
            }
        }

        private static void BuildEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static void BuildStartMenuCanvas()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/hud_pill_bg_64x64.png");
            Sprite gearSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_gear_32x32.png");
            Sprite dinoSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_sneak_32x32.png");
            Sprite foodSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_food_8x8.png");
            Sprite waterSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_water_8x8.png");

            // Root Canvas + Multi-Resolution Adapter
            GameObject canvasGo = new GameObject("StartMenu_Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(ResponsiveCanvasAdapter), typeof(UIPanelManager), typeof(UISoundManager), typeof(StartMenuController));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // SafeArea Root (自适应刘海屏/不同纵横比)
            GameObject safeGo = new GameObject("SafeAreaRoot", typeof(RectTransform));
            safeGo.transform.SetParent(canvasGo.transform, false);
            RectTransform safeRT = safeGo.GetComponent<RectTransform>();
            StretchFull(safeRT);

            ResponsiveCanvasAdapter adapter = canvasGo.GetComponent<ResponsiveCanvasAdapter>();
            adapter.Configure(scaler, safeRT);

            // 1. 顶部状态栏占位（左头像 + 右货币/齿轮）
            GameObject topBar = new GameObject("TopStatusBar", typeof(RectTransform));
            topBar.transform.SetParent(safeRT, false);
            RectTransform topRT = topBar.GetComponent<RectTransform>();
            topRT.anchorMin = new Vector2(0f, 1f);
            topRT.anchorMax = new Vector2(1f, 1f);
            topRT.pivot = new Vector2(0.5f, 1f);
            topRT.anchoredPosition = new Vector2(0f, -20f);
            topRT.sizeDelta = new Vector2(-56f, 82f);

            // 1.1 左上角头像占位栏
            GameObject profileGo = CreatePillCard(topBar.transform, "ProfilePlaceholder", pillSprite, new Color(0.10f, 0.14f, 0.20f, 0.90f));
            RectTransform profRT = profileGo.GetComponent<RectTransform>();
            profRT.anchorMin = new Vector2(0f, 0.5f);
            profRT.anchorMax = new Vector2(0f, 0.5f);
            profRT.pivot = new Vector2(0f, 0.5f);
            profRT.anchoredPosition = Vector2.zero;
            profRT.sizeDelta = new Vector2(360f, 78f);
            Button profileBtn = profileGo.AddComponent<Button>();

            GameObject avatarGo = new GameObject("AvatarIcon", typeof(RectTransform), typeof(Image));
            avatarGo.transform.SetParent(profileGo.transform, false);
            RectTransform avRT = avatarGo.GetComponent<RectTransform>();
            avRT.anchorMin = new Vector2(0f, 0.5f);
            avRT.anchorMax = new Vector2(0f, 0.5f);
            avRT.pivot = new Vector2(0f, 0.5f);
            avRT.anchoredPosition = new Vector2(14f, 0f);
            avRT.sizeDelta = new Vector2(56f, 56f);
            Image avImg = avatarGo.GetComponent<Image>();
            avImg.sprite = dinoSprite;
            avImg.preserveAspect = true;

            CreateLabel(profileGo.transform, "PlayerName", font, "驯龙者 #001  |  Lv.1", 25, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(84f, 4f), new Vector2(-16f, -4f));

            // 1.2 右上角货币栏占位 + 齿轮设置按钮
            Button gearBtn = CreateIconPillButton(topBar.transform, "GearSettingsButton", pillSprite, gearSprite,
                new Color(0.12f, 0.16f, 0.22f, 0.92f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(78f, 78f));

            Button amberAddBtn = CreateCurrencyChip(topBar.transform, "AmberCurrencyPlaceholder", pillSprite, waterSprite,
                "60", new Vector2(-102f, 0f));

            Button goldAddBtn = CreateCurrencyChip(topBar.transform, "GoldCurrencyPlaceholder", pillSprite, foodSprite,
                "1,280", new Vector2(-342f, 0f));

            // 2. 右侧主标题与辅助按钮区
            GameObject titlePanel = new GameObject("HeroTitleGroup", typeof(RectTransform));
            titlePanel.transform.SetParent(safeRT, false);
            RectTransform heroRT = titlePanel.GetComponent<RectTransform>();
            heroRT.anchorMin = new Vector2(1f, 0.5f);
            heroRT.anchorMax = new Vector2(1f, 0.5f);
            heroRT.pivot = new Vector2(1f, 0.5f);
            heroRT.anchoredPosition = new Vector2(-72f, 48f);
            heroRT.sizeDelta = new Vector2(680f, 240f);

            CreateLabel(titlePanel.transform, "GameTitle", font, "PIXELSaur", 86, FontStyle.Bold, Color.white,
                TextAnchor.MiddleRight, new Vector2(0f, 0.38f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            CreateLabel(titlePanel.transform, "SubTitle", font, "像素侏罗纪 · 3D 生存岛屿演示", 30, FontStyle.Bold, new Color(0.68f, 0.88f, 0.96f, 1f),
                TextAnchor.MiddleRight, new Vector2(0f, 0f), new Vector2(1f, 0.36f), Vector2.zero, Vector2.zero);

            // 3. 右下角主动作区：【开始游戏】 + 图鉴/公告占位
            Button startBtn = CreateTextPillButton(safeRT, "StartGameButton", pillSprite, font, "开 始 游 戏",
                new Color(0.18f, 0.68f, 0.34f, 0.98f), 38,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-72f, 64f), new Vector2(400f, 108f));

            Button codexBtn = CreateTextPillButton(safeRT, "CodexButton", pillSprite, font, "恐龙图鉴",
                new Color(0.16f, 0.24f, 0.34f, 0.94f), 26,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-500f, 74f), new Vector2(190f, 84f));

            Button noticeBtn = CreateTextPillButton(safeRT, "NoticeButton", pillSprite, font, "岛屿公告",
                new Color(0.16f, 0.24f, 0.34f, 0.94f), 26,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-714f, 74f), new Vector2(190f, 84f));

            // 4. 面板管理器测试弹窗：占位信息窗口 (初始隐藏)
            GameObject modalGo = CreatePillCard(safeRT, "InfoModalPanel", pillSprite, new Color(0.08f, 0.12f, 0.18f, 0.98f));
            RectTransform modalRT = modalGo.GetComponent<RectTransform>();
            modalRT.anchorMin = new Vector2(0.5f, 0.5f);
            modalRT.anchorMax = new Vector2(0.5f, 0.5f);
            modalRT.sizeDelta = new Vector2(760f, 400f);
            modalRT.anchoredPosition = Vector2.zero;

            Text modalTitle = CreateLabel(modalGo.transform, "ModalTitle", font, "提示标题", 36, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.74f), new Vector2(1f, 1f), new Vector2(36f, 0f), new Vector2(-36f, -12f));
            Text modalBody = CreateLabel(modalGo.transform, "ModalBody", font, "内容占位", 24, FontStyle.Bold, new Color(0.80f, 0.88f, 0.96f, 1f),
                TextAnchor.UpperLeft, new Vector2(0f, 0.24f), new Vector2(1f, 0.72f), new Vector2(36f, 8f), new Vector2(-36f, -8f));
            Button modalClose = CreateTextPillButton(modalGo.transform, "ModalCloseButton", pillSprite, font, "确 定",
                new Color(0.18f, 0.60f, 0.36f, 0.98f), 28,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(220f, 68f));
            modalGo.SetActive(false);

            // 5. 挂载共享设置体系 (SettingsMenuPanel + SubSettingsPage，初始隐藏)
            GameObject settingsMenuObj = new GameObject("SettingsMenuPanel", typeof(RectTransform), typeof(SettingsMenuPanelHUD));
            settingsMenuObj.transform.SetParent(canvasGo.transform, false);
            SettingsMenuPanelHUD settingsMenuHud = settingsMenuObj.GetComponent<SettingsMenuPanelHUD>();
            settingsMenuHud.EnsureUIHierarchy();
            settingsMenuObj.SetActive(false);

            GameObject subSettingsObj = new GameObject("SubSettingsPage", typeof(RectTransform), typeof(SubSettingsPageHUD));
            subSettingsObj.transform.SetParent(canvasGo.transform, false);
            SubSettingsPageHUD subHud = subSettingsObj.GetComponent<SubSettingsPageHUD>();
            subHud.EnsureUIHierarchy();
            subSettingsObj.SetActive(false);

            // 6. 绑定 StartMenuController 与 UIPanelManager 序列化引用
            UIPanelManager panelMgr = canvasGo.GetComponent<UIPanelManager>();
            panelMgr.RegisterPanel("InfoModal", modalGo, true);
            EditorUtility.SetDirty(panelMgr);

            StartMenuController ctrl = canvasGo.GetComponent<StartMenuController>();
            SerializedObject so = new SerializedObject(ctrl);
            so.FindProperty("startGameButton").objectReferenceValue = startBtn;
            so.FindProperty("gearSettingsButton").objectReferenceValue = gearBtn;
            so.FindProperty("profileButton").objectReferenceValue = profileBtn;
            so.FindProperty("goldAddButton").objectReferenceValue = goldAddBtn;
            so.FindProperty("amberAddButton").objectReferenceValue = amberAddBtn;
            so.FindProperty("codexButton").objectReferenceValue = codexBtn;
            so.FindProperty("noticeButton").objectReferenceValue = noticeBtn;
            so.FindProperty("infoModalPanel").objectReferenceValue = modalGo;
            so.FindProperty("infoModalTitle").objectReferenceValue = modalTitle;
            so.FindProperty("infoModalBody").objectReferenceValue = modalBody;
            so.FindProperty("infoModalCloseButton").objectReferenceValue = modalClose;
            so.FindProperty("settingsMenuPanel").objectReferenceValue = settingsMenuHud;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ctrl);
        }

        private static GameObject CreatePillCard(Transform parent, string name, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            return go;
        }

        private static Text CreateLabel(Transform parent, string name, Font font, string text, int size, FontStyle style, Color color,
            TextAnchor align, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.text = text;
            txt.color = color;
            txt.alignment = align;
            txt.raycastTarget = false;
            return txt;
        }

        private static Button CreateIconPillButton(Transform parent, string name, Sprite bgSprite, Sprite iconSprite, Color bgColor,
            Vector2 anchorRight, Vector2 pos, Vector2 size)
        {
            GameObject go = CreatePillCard(parent, name, bgSprite, bgColor);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorRight;
            rt.anchorMax = anchorRight;
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Button btn = go.AddComponent<Button>();

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            RectTransform irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.18f, 0.18f);
            irt.anchorMax = new Vector2(0.82f, 0.82f);
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = Vector2.zero;
            Image iImg = iconGo.GetComponent<Image>();
            iImg.sprite = iconSprite;
            iImg.preserveAspect = true;
            iImg.raycastTarget = false;
            return btn;
        }

        private static Button CreateCurrencyChip(Transform parent, string name, Sprite pillSprite, Sprite iconSprite, string amountText, Vector2 rightPos)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject go = CreatePillCard(parent, name, pillSprite, new Color(0.10f, 0.14f, 0.20f, 0.90f));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = rightPos;
            rt.sizeDelta = new Vector2(220f, 72f);

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            RectTransform irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0f, 0.5f);
            irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(14f, 0f);
            irt.sizeDelta = new Vector2(40f, 40f);
            Image iImg = iconGo.GetComponent<Image>();
            iImg.sprite = iconSprite;
            iImg.preserveAspect = true;

            CreateLabel(go.transform, "Amount", font, amountText, 24, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(0.72f, 1f), new Vector2(64f, 0f), Vector2.zero);

            Button addBtn = CreateTextPillButton(go.transform, "AddButton", pillSprite, font, "+",
                new Color(0.20f, 0.54f, 0.34f, 0.96f), 26,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(46f, 46f));
            return addBtn;
        }

        private static Button CreateTextPillButton(Transform parent, string name, Sprite pillSprite, Font font, string label, Color bgColor, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            GameObject go = CreatePillCard(parent, name, pillSprite, bgColor);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Button btn = go.AddComponent<Button>();

            CreateLabel(go.transform, "Text", font, label, fontSize, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return btn;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
#endif
