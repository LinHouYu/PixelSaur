#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PixelSaur.Environment;
using PixelSaur.Grass;
using PixelSaur.UI;
using PixelSaur.Water;

namespace PixelSaur.Editor
{
    /// <summary>
    /// 创建并配置双场景架构：
    /// 1. 确保 Game.unity 的 HUD_Canvas 挂载 ResponsiveCanvasAdapter；
    /// 2. 从 Game.unity 完整克隆岛屿地形 (Island_Terrain)、水体 (WaterSurface_Plane / WaterRippleManager)、
    ///    草地渲染 (GrassManager)、光照与 Global Volume 至 StartMenu.unity，
    ///    让玩家在开始大厅调设置时可实时预览草地密度、水面涟漪与画质效果；
    /// 3. 将 StartMenu.unity (0) 与 Game.unity (1) 注册至 EditorBuildSettings.scenes。
    /// </summary>
    public static class BuildStartMenuScene
    {
        private const string StartScenePath = "Assets/Scenes/StartMenu.unity";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("PixelSaur/生成并配置双场景 (StartMenu + Game)")]
        public static string Main()
        {
            EnsureUiAssetImports();

            // 1. 打开 Game.unity，确保 HUD_Canvas 挂载 ResponsiveCanvasAdapter、恐龙仓库面板及更新设置菜单按钮
            Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            GameObject hudCanvas = GameObject.Find("HUD_Canvas");
            if (hudCanvas != null)
            {
                ResponsiveCanvasAdapter adapter = hudCanvas.GetComponent<ResponsiveCanvasAdapter>();
                if (adapter == null) adapter = hudCanvas.AddComponent<ResponsiveCanvasAdapter>();
                adapter.Configure(hudCanvas.GetComponent<CanvasScaler>(), null);

                InteractionProgressHUD progressHud = hudCanvas.GetComponent<InteractionProgressHUD>();
                if (progressHud == null) progressHud = hudCanvas.AddComponent<InteractionProgressHUD>();
                progressHud.BuildUI();
                Transform progBar = hudCanvas.transform.Find("InteractionProgressBar");
                if (progBar != null) progBar.gameObject.SetActive(false);
                EditorUtility.SetDirty(progressHud);

                // 替换 5 个操作与技能按钮的贴图（skill1~skill5）并挂载小眼睛按钮（eyes.png）与摇杆扇形疾跑配置
                ApplySkillButtonSprites(hudCanvas.transform);
                EnsureFreeLookEyeButton(hudCanvas.transform);
                ConfigureVirtualJoystickSprint(hudCanvas.transform);

                // TopBarStatusHUD 严格保持原始尺寸 (360 x 68) 与样式不改动
                TopBarStatusHUD topBar = hudCanvas.GetComponentInChildren<TopBarStatusHUD>(true);
                if (topBar != null)
                {
                    SerializedObject topSo = new SerializedObject(topBar);
                    topSo.FindProperty("panelPosition").vector2Value = new Vector2(24f, -20f);
                    topSo.FindProperty("panelSize").vector2Value = new Vector2(360f, 68f);
                    topSo.ApplyModifiedPropertiesWithoutUndo();
                    topBar.EnsureUIHierarchy();
                    EditorUtility.SetDirty(topBar);
                }

                HudLayoutManager.ApplyLayoutToGameHud(HudLayoutManager.GetDefaultLayout());

                // 修复 SettingsMenuPanel 按钮原本颜色发灰及 Layer 问题（不删除任何图片或组件）
                SettingsMenuPanelHUD settingsInGame = hudCanvas.GetComponentInChildren<SettingsMenuPanelHUD>(true);
                if (settingsInGame != null)
                {
                    settingsInGame.RestoreOriginalButtonColors();
                    settingsInGame.EnsureUIHierarchy();
                    EditorUtility.SetDirty(settingsInGame);
                }

                Transform oldInv = hudCanvas.transform.Find("InventoryPanel");
                if (oldInv != null) Object.DestroyImmediate(oldInv.gameObject);
                InventoryPanelHUD gameInv = AttachInventoryPanelWithSprites(hudCanvas.transform);
                gameInv.gameObject.SetActive(false);

                Transform oldLayoutEditor = hudCanvas.transform.Find("HudLayoutEditorPanel");
                if (oldLayoutEditor != null) Object.DestroyImmediate(oldLayoutEditor.gameObject);
                HudLayoutEditorHUD gameLayoutEditor = AttachHudLayoutEditorWithSprites(hudCanvas.transform);
                gameLayoutEditor.gameObject.SetActive(false);

                EditorUtility.SetDirty(hudCanvas);
            }
            EditorSceneManager.SaveScene(gameScene);

            // 记录 Game.unity 环境与渲染设置
            Material gameSkybox = RenderSettings.skybox;
            AmbientMode gameAmbientMode = RenderSettings.ambientMode;
            Color gameAmbientSky = RenderSettings.ambientSkyColor;
            Color gameAmbientEquator = RenderSettings.ambientEquatorColor;
            Color gameAmbientGround = RenderSettings.ambientGroundColor;
            bool gameFog = RenderSettings.fog;
            Color gameFogColor = RenderSettings.fogColor;

            // 2. 以 Additive 模式创建新的 StartMenu 场景，从 Game.unity 精确克隆水/草/岛屿资产
            Scene startScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            GameObject dinoShowcase = BuildEnvironmentFromGameScene(gameScene, startScene);

            // 切换到 startScene 并关闭 gameScene（保持 Game.unity 原样不受影响）
            SceneManager.SetActiveScene(startScene);
            EditorSceneManager.CloseScene(gameScene, true);

            // 同步天空盒与环境光照
            RenderSettings.skybox = gameSkybox;
            RenderSettings.ambientMode = gameAmbientMode;
            RenderSettings.ambientSkyColor = gameAmbientSky;
            RenderSettings.ambientEquatorColor = gameAmbientEquator;
            RenderSettings.ambientGroundColor = gameAmbientGround;
            RenderSettings.fog = gameFog;
            RenderSettings.fogColor = gameFogColor;

            WireClonedEnvironmentTargets(startScene, dinoShowcase);
            BuildEventSystem(startScene);
            BuildStartMenuCanvas(startScene);

            bool savedStart = EditorSceneManager.SaveScene(startScene, StartScenePath);

            // 3. 配置 Build Settings 双场景顺序：0=StartMenu, 1=Game
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(StartScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"OK: StartSaved={savedStart}, HasGrassWaterIsland=True, BuildScenes=2";
        }

        private static GameObject BuildEnvironmentFromGameScene(Scene gameScene, Scene startScene)
        {
            string[] envNames =
            {
                "Directional Light",
                "Global Volume",
                "Island_Terrain",
                "GrassManager",
                "WaterSurface_Plane",
                "WaterRippleManager"
            };

            GameObject[] gameRoots = gameScene.GetRootGameObjects();
            foreach (string targetName in envNames)
            {
                GameObject src = FindRootByName(gameRoots, targetName);
                if (src == null) continue;

                GameObject clone = Object.Instantiate(src);
                clone.name = targetName;
                SceneManager.MoveGameObjectToScene(clone, startScene);
            }

            // 克隆/创建主摄像机并框景在岛屿左侧（展示水、草、霸王龙），右侧留白给标题与按钮
            GameObject camSrc = FindRootByName(gameRoots, "Main Camera");
            GameObject camObj;
            if (camSrc != null)
            {
                camObj = Object.Instantiate(camSrc);
                camObj.name = "Main Camera";
                var orbit = camObj.GetComponent<PixelSaur.Cam.ThirdPersonOrbitCamera>();
                if (orbit != null) Object.DestroyImmediate(orbit);
                SceneManager.MoveGameObjectToScene(camObj, startScene);
            }
            else
            {
                camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                SceneManager.MoveGameObjectToScene(camObj, startScene);
            }

            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(1.1f, 2.75f, -6.2f);
            camObj.transform.rotation = Quaternion.Euler(14f, -14f, 0f);
            Camera cam = camObj.GetComponent<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.fieldOfView = 46f;
            }

            // 放置 3D 霸王龙展示角色于岛屿草地偏左区域，绑定 idle 呼吸动作与 Player Tag 以兼容草/水交互
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/霸王龙/Minecraft_T_Rex.fbx");
            if (fbxPrefab == null) return null;

            GameObject dinoInst = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
            dinoInst.name = "LobbyTRexPreview";
            dinoInst.tag = "Player";
            SceneManager.MoveGameObjectToScene(dinoInst, startScene);
            dinoInst.transform.position = new Vector3(-0.75f, 0.14f, -0.35f);
            dinoInst.transform.rotation = Quaternion.Euler(0f, -26f, 0f);
            dinoInst.transform.localScale = Vector3.one * 0.78f;

            Animator anim = dinoInst.GetComponent<Animator>();
            if (anim == null) anim = dinoInst.AddComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Model/霸王龙/TRexAnimatorController.controller");
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

            return dinoInst;
        }

        private static void WireClonedEnvironmentTargets(Scene startScene, GameObject dinoShowcase)
        {
            GameObject[] roots = startScene.GetRootGameObjects();
            GameObject island = FindRootByName(roots, "Island_Terrain");
            GameObject grassMgrObj = FindRootByName(roots, "GrassManager");
            GameObject rippleMgrObj = FindRootByName(roots, "WaterRippleManager");
            GameObject waterPlaneObj = FindRootByName(roots, "WaterSurface_Plane");

            // 1. 绑定草地生成网格至 StartMenu 自己的 Grass_Plain
            if (grassMgrObj != null && island != null)
            {
                Transform grassPlain = island.transform.Find("Grass_Plain");
                MeshFilter grassMesh = grassPlain != null ? grassPlain.GetComponentInChildren<MeshFilter>() : null;

                ProceduralGrassRenderer grassRenderer = grassMgrObj.GetComponent<ProceduralGrassRenderer>();
                if (grassRenderer != null && grassMesh != null)
                {
                    SerializedObject gSo = new SerializedObject(grassRenderer);
                    gSo.FindProperty("targetMeshFilter").objectReferenceValue = grassMesh;
                    gSo.ApplyModifiedPropertiesWithoutUndo();
                    grassRenderer.RebuildGrass();
                }

                GrassInteractionManager grassInteract = grassMgrObj.GetComponent<GrassInteractionManager>();
                if (grassInteract != null && dinoShowcase != null)
                {
                    SerializedObject giSo = new SerializedObject(grassInteract);
                    giSo.FindProperty("targetPlayer").objectReferenceValue = dinoShowcase.transform;
                    giSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // 2. 绑定水波交互至 StartMenu 的霸王龙预览节点，并触发一圈轻微初始水波
            if (rippleMgrObj != null && dinoShowcase != null)
            {
                WaterRippleManager rippleMgr = rippleMgrObj.GetComponent<WaterRippleManager>();
                if (rippleMgr != null)
                {
                    SerializedObject wrSo = new SerializedObject(rippleMgr);
                    wrSo.FindProperty("targetPlayer").objectReferenceValue = dinoShowcase.transform;
                    wrSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            if (island != null)
            {
                var setup = island.GetComponent<IslandTerrainColliderSetup>();
                if (setup != null) EditorUtility.SetDirty(setup);
            }
            if (waterPlaneObj != null) EditorUtility.SetDirty(waterPlaneObj);
        }

        private static GameObject FindRootByName(GameObject[] roots, string targetName)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == targetName)
                {
                    return roots[i];
                }
            }
            return null;
        }

        private static void BuildEventSystem(Scene startScene)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(es, startScene);
        }

        private static void EnsureUiAssetImports()
        {
            AssetDatabase.Refresh();
            var pillImporter = AssetImporter.GetAtPath("Assets/Textures/UI/ui_white_pill_64x64.png") as TextureImporter;
            if (pillImporter != null)
            {
                pillImporter.textureType = TextureImporterType.Sprite;
                pillImporter.spriteImportMode = SpriteImportMode.Single;
                pillImporter.alphaIsTransparency = true;
                pillImporter.mipmapEnabled = false;
                pillImporter.filterMode = FilterMode.Bilinear;
                pillImporter.spriteBorder = new Vector4(14f, 14f, 14f, 14f);
                pillImporter.textureCompression = TextureImporterCompression.Uncompressed;
                pillImporter.SaveAndReimport();
            }

            var bevelImporter = AssetImporter.GetAtPath("Assets/Textures/UI/ui_bevel_pill_64x64.png") as TextureImporter;
            if (bevelImporter != null)
            {
                bevelImporter.textureType = TextureImporterType.Sprite;
                bevelImporter.spriteImportMode = SpriteImportMode.Single;
                bevelImporter.alphaIsTransparency = true;
                bevelImporter.mipmapEnabled = false;
                bevelImporter.filterMode = FilterMode.Point;
                bevelImporter.spriteBorder = new Vector4(15f, 15f, 15f, 15f);
                bevelImporter.textureCompression = TextureImporterCompression.Uncompressed;
                bevelImporter.SaveAndReimport();
            }

            string[] skillPaths =
            {
                "Assets/Textures/UI/skill1.png",
                "Assets/Textures/UI/skill2.png",
                "Assets/Textures/UI/skill3.png",
                "Assets/Textures/UI/skill4.png",
                "Assets/Textures/UI/skill5.png",
                "Assets/Textures/UI/eyes.png"
            };
            foreach (string spPath in skillPaths)
            {
                var skImp = AssetImporter.GetAtPath(spPath) as TextureImporter;
                if (skImp != null)
                {
                    skImp.textureType = TextureImporterType.Sprite;
                    skImp.spriteImportMode = SpriteImportMode.Single;
                    skImp.alphaIsTransparency = true;
                    skImp.mipmapEnabled = false;
                    skImp.filterMode = FilterMode.Point;
                    skImp.textureCompression = TextureImporterCompression.Uncompressed;
                    skImp.SaveAndReimport();
                }
            }

            var qqImporter = AssetImporter.GetAtPath("Assets/Textures/UI/qq_group_card.jpg") as TextureImporter;
            if (qqImporter != null)
            {
                qqImporter.textureType = TextureImporterType.Sprite;
                qqImporter.spriteImportMode = SpriteImportMode.Single;
                qqImporter.mipmapEnabled = false;
                qqImporter.filterMode = FilterMode.Bilinear;
                qqImporter.SaveAndReimport();
            }

            string[] skinGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/DinoSkins" });
            for (int i = 0; i < skinGuids.Length; i++)
            {
                string skinPath = AssetDatabase.GUIDToAssetPath(skinGuids[i]);
                var skinImporter = AssetImporter.GetAtPath(skinPath) as TextureImporter;
                if (skinImporter != null)
                {
                    skinImporter.textureType = TextureImporterType.Default;
                    skinImporter.mipmapEnabled = false;
                    skinImporter.filterMode = FilterMode.Point;
                    skinImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    skinImporter.SaveAndReimport();
                }
            }

            string[] iconGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/DinoIcons" });
            for (int i = 0; i < iconGuids.Length; i++)
            {
                string iconPath = AssetDatabase.GUIDToAssetPath(iconGuids[i]);
                var iconImporter = AssetImporter.GetAtPath(iconPath) as TextureImporter;
                if (iconImporter != null)
                {
                    iconImporter.textureType = TextureImporterType.Sprite;
                    iconImporter.spriteImportMode = SpriteImportMode.Single;
                    iconImporter.alphaIsTransparency = true;
                    iconImporter.mipmapEnabled = false;
                    iconImporter.filterMode = FilterMode.Point;
                    iconImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    iconImporter.SaveAndReimport();
                }
            }
        }

        private static void BuildStartMenuCanvas(Scene startScene)
        {
            EnsureUiAssetImports();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/ui_bevel_pill_64x64.png");
            Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/ui_white_pill_64x64.png");
            Sprite qqCardSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/qq_group_card.jpg");
            Sprite gearSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_gear_32x32.png");
            Sprite dinoSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_sneak_32x32.png");
            Sprite playersSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_players_32x32.png");
            Sprite foodSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_food_8x8.png");
            Sprite waterSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_water_8x8.png");

            // Root Canvas + Multi-Resolution Adapter
            GameObject canvasGo = new GameObject("StartMenu_Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(ResponsiveCanvasAdapter), typeof(UIPanelManager), typeof(UISoundManager), typeof(StartMenuController));
            SceneManager.MoveGameObjectToScene(canvasGo, startScene);

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

            // 1.15 左上角查找好友入口按钮（紧邻头像栏）
            Button friendsBtn = CreateTextPillButton(topBar.transform, "FriendsButton", pillSprite, font, "查找好友",
                new Color(0.18f, 0.30f, 0.46f, 0.94f), 22,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(378f, 0f), new Vector2(228f, 76f));
            Text friendsBtnTxt = friendsBtn.transform.Find("Text").GetComponent<Text>();
            RectTransform fbTxtRT = friendsBtnTxt.GetComponent<RectTransform>();
            fbTxtRT.offsetMin = new Vector2(54f, 0f);
            fbTxtRT.offsetMax = new Vector2(-10f, 0f);

            GameObject fbIconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            fbIconGo.transform.SetParent(friendsBtn.transform, false);
            RectTransform fbIconRT = fbIconGo.GetComponent<RectTransform>();
            fbIconRT.anchorMin = new Vector2(0f, 0.5f);
            fbIconRT.anchorMax = new Vector2(0f, 0.5f);
            fbIconRT.pivot = new Vector2(0f, 0.5f);
            fbIconRT.anchoredPosition = new Vector2(14f, 0f);
            fbIconRT.sizeDelta = new Vector2(38f, 38f);
            Image fbIconImg = fbIconGo.GetComponent<Image>();
            fbIconImg.sprite = playersSprite;
            fbIconImg.preserveAspect = true;
            fbIconImg.raycastTarget = false;

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

            Text mainTitleTxt = CreateLabel(titlePanel.transform, "GameTitle", font, "PIXELSaur", 86, FontStyle.Bold, Color.white,
                TextAnchor.MiddleRight, new Vector2(0f, 0.38f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            Outline titleOutline = mainTitleTxt.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            titleOutline.effectDistance = new Vector2(3f, -3f);

            Text subTitleTxt = CreateLabel(titlePanel.transform, "SubTitle", font, "像素侏罗纪 · 3D 生存岛屿演示", 30, FontStyle.Bold, new Color(0.86f, 0.96f, 1.0f, 1f),
                TextAnchor.MiddleRight, new Vector2(0f, 0f), new Vector2(1f, 0.36f), Vector2.zero, Vector2.zero);
            Outline subOutline = subTitleTxt.gameObject.AddComponent<Outline>();
            subOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            subOutline.effectDistance = new Vector2(2f, -2f);

            // 3. 右下角主动作区：【开始游戏】 + 图鉴/公告/任务/背包/商店
            Button startBtn = CreateTextPillButton(safeRT, "StartGameButton", pillSprite, font, "开 始 游 戏",
                new Color(0.18f, 0.68f, 0.34f, 0.98f), 38,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-64f, 64f), new Vector2(380f, 108f));

            Button codexBtn = CreateTextPillButton(safeRT, "CodexButton", pillSprite, font, "恐龙图鉴",
                new Color(0.16f, 0.24f, 0.34f, 0.94f), 25,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-466f, 74f), new Vector2(168f, 84f));

            Button noticeBtn = CreateTextPillButton(safeRT, "NoticeButton", pillSprite, font, "岛屿公告",
                new Color(0.16f, 0.24f, 0.34f, 0.94f), 25,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-652f, 74f), new Vector2(168f, 84f));

            Button questBtn = CreateTextPillButton(safeRT, "QuestButton", pillSprite, font, "每日任务",
                new Color(0.20f, 0.34f, 0.50f, 0.96f), 25,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-838f, 74f), new Vector2(168f, 84f));

            Button inventoryBtn = CreateTextPillButton(safeRT, "InventoryButton", pillSprite, font, "恐龙仓库",
                new Color(0.18f, 0.44f, 0.48f, 0.96f), 25,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-1024f, 74f), new Vector2(168f, 84f));

            Button shopBtn = CreateTextPillButton(safeRT, "ShopButton", pillSprite, font, "恐龙商店",
                new Color(0.54f, 0.36f, 0.16f, 0.96f), 25,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-1210f, 74f), new Vector2(168f, 84f));

            // 4. 面板管理器测试弹窗：占位信息窗口 (初始隐藏)
            GameObject modalGo = CreatePillCard(safeRT, "InfoModalPanel", panelSprite, new Color(0.08f, 0.12f, 0.18f, 0.98f));
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

            // 4.5 任务系统弹窗面板（初始隐藏，受 UIPanelManager 管理）
            GameObject questObj = new GameObject("QuestPanel", typeof(RectTransform), typeof(QuestPanelHUD));
            questObj.transform.SetParent(safeRT, false);
            QuestPanelHUD questHud = questObj.GetComponent<QuestPanelHUD>();
            SerializedObject qSo = new SerializedObject(questHud);
            qSo.FindProperty("buttonPillSprite").objectReferenceValue = pillSprite;
            qSo.ApplyModifiedPropertiesWithoutUndo();
            questHud.EnsureUI();
            questObj.SetActive(false);

            // 4.6 可互动公告与 QQ 反馈群弹窗面板（初始隐藏，受 UIPanelManager 管理）
            GameObject noticeObj = new GameObject("NoticePanel", typeof(RectTransform), typeof(NoticePanelHUD));
            noticeObj.transform.SetParent(safeRT, false);
            NoticePanelHUD noticeHud = noticeObj.GetComponent<NoticePanelHUD>();
            SerializedObject nSo = new SerializedObject(noticeHud);
            nSo.FindProperty("whitePillSprite").objectReferenceValue = panelSprite;
            nSo.FindProperty("buttonPillSprite").objectReferenceValue = pillSprite;
            nSo.FindProperty("qqCardSprite").objectReferenceValue = qqCardSprite;
            nSo.ApplyModifiedPropertiesWithoutUndo();
            noticeHud.EnsureUI();
            noticeObj.SetActive(false);

            // 4.7 恐龙仓库弹窗面板（初始隐藏，受 UIPanelManager 管理）
            InventoryPanelHUD inventoryHud = AttachInventoryPanelWithSprites(safeRT);
            GameObject inventoryObj = inventoryHud.gameObject;
            inventoryObj.SetActive(false);

            // 4.8 恐龙商店与琥珀充值弹窗面板（初始隐藏，受 UIPanelManager 管理）
            GameObject shopObj = new GameObject("ShopPanel", typeof(RectTransform), typeof(ShopPanelHUD));
            shopObj.transform.SetParent(safeRT, false);
            ShopPanelHUD shopHud = shopObj.GetComponent<ShopPanelHUD>();
            SerializedObject sSo = new SerializedObject(shopHud);
            sSo.FindProperty("whitePillSprite").objectReferenceValue = panelSprite;
            sSo.FindProperty("buttonPillSprite").objectReferenceValue = pillSprite;
            sSo.FindProperty("iconDinoSneak").objectReferenceValue = dinoSprite;
            sSo.FindProperty("iconDinoBite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_bite_32x32.png");
            sSo.FindProperty("iconDinoJump").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_jump_32x32.png");
            sSo.FindProperty("iconDinoSkill1").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_skill1.png");
            sSo.FindProperty("iconDinoSkill2").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_skill2.png");
            sSo.FindProperty("iconPterosaurRun").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_pterosaur_run_32x32.png");
            sSo.FindProperty("iconFood").objectReferenceValue = foodSprite;
            sSo.FindProperty("iconWater").objectReferenceValue = waterSprite;
            sSo.FindProperty("iconHeart").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_heart_8x8.png");
            sSo.ApplyModifiedPropertiesWithoutUndo();
            shopHud.EnsureUI();
            shopObj.SetActive(false);

            // 4.9 查找朋友与好友列表弹窗面板（初始隐藏，受 UIPanelManager 管理）
            GameObject friendObj = new GameObject("FriendPanel", typeof(RectTransform), typeof(FriendPanelHUD));
            friendObj.transform.SetParent(safeRT, false);
            FriendPanelHUD friendHud = friendObj.GetComponent<FriendPanelHUD>();
            SerializedObject fSo = new SerializedObject(friendHud);
            fSo.FindProperty("whitePillSprite").objectReferenceValue = panelSprite;
            fSo.FindProperty("buttonPillSprite").objectReferenceValue = pillSprite;
            fSo.FindProperty("iconPlayers").objectReferenceValue = playersSprite;
            fSo.FindProperty("iconDinoSneak").objectReferenceValue = dinoSprite;
            fSo.ApplyModifiedPropertiesWithoutUndo();
            friendHud.EnsureUI();
            friendObj.SetActive(false);

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

            HudLayoutEditorHUD startLayoutEditor = AttachHudLayoutEditorWithSprites(canvasGo.transform);
            startLayoutEditor.gameObject.SetActive(false);

            // 6. 绑定 StartMenuController 与 UIPanelManager 序列化引用
            UIPanelManager panelMgr = canvasGo.GetComponent<UIPanelManager>();
            panelMgr.RegisterPanel("InfoModal", modalGo, true);
            panelMgr.RegisterPanel("QuestPanel", questObj, true);
            panelMgr.RegisterPanel("NoticePanel", noticeObj, true);
            panelMgr.RegisterPanel("InventoryPanel", inventoryObj, true);
            panelMgr.RegisterPanel("ShopPanel", shopObj, true);
            panelMgr.RegisterPanel("FriendPanel", friendObj, true);
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
            so.FindProperty("questButton").objectReferenceValue = questBtn;
            so.FindProperty("inventoryButton").objectReferenceValue = inventoryBtn;
            so.FindProperty("shopButton").objectReferenceValue = shopBtn;
            so.FindProperty("friendsButton").objectReferenceValue = friendsBtn;
            so.FindProperty("friendsButtonLabel").objectReferenceValue = friendsBtnTxt;
            so.FindProperty("goldAmountText").objectReferenceValue = goldAddBtn.transform.parent.Find("Amount").GetComponent<Text>();
            so.FindProperty("amberAmountText").objectReferenceValue = amberAddBtn.transform.parent.Find("Amount").GetComponent<Text>();
            so.FindProperty("questPanel").objectReferenceValue = questHud;
            so.FindProperty("noticePanel").objectReferenceValue = noticeHud;
            so.FindProperty("inventoryPanel").objectReferenceValue = inventoryHud;
            so.FindProperty("shopPanel").objectReferenceValue = shopHud;
            so.FindProperty("friendPanel").objectReferenceValue = friendHud;
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

            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.70f, 0.85f, 1.0f, 1f);
            cb.selectedColor = Color.white;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            Text txt = CreateLabel(go.transform, "Text", font, label, fontSize, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Outline outline = txt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.90f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return btn;
        }

        private static InventoryPanelHUD AttachInventoryPanelWithSprites(Transform parent)
        {
            GameObject inventoryObj = new GameObject("InventoryPanel", typeof(RectTransform), typeof(InventoryPanelHUD));
            inventoryObj.transform.SetParent(parent, false);
            InventoryPanelHUD inventoryHud = inventoryObj.GetComponent<InventoryPanelHUD>();
            SerializedObject invSo = new SerializedObject(inventoryHud);
            invSo.FindProperty("whitePillSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/ui_white_pill_64x64.png");
            invSo.FindProperty("buttonPillSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/ui_bevel_pill_64x64.png");
            invSo.FindProperty("iconDinoSneak").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_sneak_32x32.png");
            invSo.FindProperty("iconDinoBite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_bite_32x32.png");
            invSo.FindProperty("iconDinoJump").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_jump_32x32.png");
            invSo.FindProperty("iconDinoSkill1").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_skill1.png");
            invSo.FindProperty("iconDinoSkill2").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_skill2.png");
            invSo.FindProperty("iconPterosaurRun").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_pterosaur_run_32x32.png");
            invSo.FindProperty("iconFood").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_food_8x8.png");
            invSo.FindProperty("iconWater").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_water_8x8.png");
            invSo.FindProperty("iconHeart").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_heart_8x8.png");
            invSo.ApplyModifiedPropertiesWithoutUndo();
            inventoryHud.EnsureUI();
            return inventoryHud;
        }

        private static void ApplySkillButtonSprites(Transform hudRoot)
        {
            if (hudRoot == null) return;
            Sprite s1 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill1.png");
            Sprite s2 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill2.png");
            Sprite s3 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill3.png");
            Sprite s4 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill4.png");
            Sprite s5 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill5.png");

            Transform jumpTr = hudRoot.Find("DinoJumpButton");
            if (jumpTr != null && s5 != null)
            {
                var comp = jumpTr.GetComponent<DinoJumpButton>();
                if (comp != null)
                {
                    SerializedObject so = new SerializedObject(comp);
                    so.FindProperty("jumpIconSprite").objectReferenceValue = s5;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    comp.SetIconSprite(s5);
                    EditorUtility.SetDirty(comp);
                }
            }

            Transform atkTr = hudRoot.Find("DinoAttackButton");
            if (atkTr != null && s4 != null)
            {
                var comp = atkTr.GetComponent<DinoAttackButton>();
                if (comp != null)
                {
                    SerializedObject so = new SerializedObject(comp);
                    so.FindProperty("biteIconSprite").objectReferenceValue = s4;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    comp.SetIconSprite(s4);
                    EditorUtility.SetDirty(comp);
                }
            }

            Transform sneakTr = hudRoot.Find("DinoSneakButton");
            if (sneakTr != null && s2 != null)
            {
                var comp = sneakTr.GetComponent<DinoSneakButton>();
                if (comp != null)
                {
                    SerializedObject so = new SerializedObject(comp);
                    so.FindProperty("sneakIconSprite").objectReferenceValue = s2;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    comp.SetIconSprite(s2);
                    EditorUtility.SetDirty(comp);
                }
            }

            Transform sk1Tr = hudRoot.Find("DinoSkill1Button");
            if (sk1Tr != null && s3 != null)
            {
                var comp = sk1Tr.GetComponent<DinoSkillButton>();
                if (comp != null)
                {
                    SerializedObject so = new SerializedObject(comp);
                    so.FindProperty("skillIconSprite").objectReferenceValue = s3;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    comp.SetIconSprite(s3);
                    EditorUtility.SetDirty(comp);
                }
            }

            Transform sk2Tr = hudRoot.Find("DinoSkill2Button");
            if (sk2Tr != null && s1 != null)
            {
                var comp = sk2Tr.GetComponent<DinoSkillButton>();
                if (comp != null)
                {
                    SerializedObject so = new SerializedObject(comp);
                    so.FindProperty("skillIconSprite").objectReferenceValue = s1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    comp.SetIconSprite(s1);
                    EditorUtility.SetDirty(comp);
                }
            }
        }

        private static HudLayoutEditorHUD AttachHudLayoutEditorWithSprites(Transform parent)
        {
            GameObject go = new GameObject("HudLayoutEditorPanel", typeof(RectTransform), typeof(HudLayoutEditorHUD));
            go.transform.SetParent(parent, false);
            HudLayoutEditorHUD hud = go.GetComponent<HudLayoutEditorHUD>();
            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("skill1Sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill1.png");
            so.FindProperty("skill2Sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill2.png");
            so.FindProperty("skill3Sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill3.png");
            so.FindProperty("skill4Sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill4.png");
            so.FindProperty("skill5Sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill5.png");
            so.FindProperty("dialogueSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dialogue_32x32.png");
            so.FindProperty("eyeSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/eyes.png");
            so.FindProperty("heartSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_heart_8x8.png");
            so.FindProperty("foodSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_food_8x8.png");
            so.FindProperty("waterSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_water_8x8.png");
            so.FindProperty("pillBgSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/hud_pill_bg_64x64.png");
            so.FindProperty("gearSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_gear_32x32.png");
            so.FindProperty("playersSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_players_32x32.png");
            so.ApplyModifiedPropertiesWithoutUndo();
            hud.EnsureUIHierarchy();
            return hud;
        }

        private static void EnsureFreeLookEyeButton(Transform hudRoot)
        {
            if (hudRoot == null) return;
            Sprite eyeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/eyes.png");
            Transform existing = hudRoot.Find("FreeLookEyeButton");
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject("FreeLookEyeButton", typeof(RectTransform), typeof(Image), typeof(FreeLookEyeButton));
            go.transform.SetParent(hudRoot, false);

            FreeLookEyeButton eyeBtn = go.GetComponent<FreeLookEyeButton>();
            if (eyeBtn == null) eyeBtn = go.AddComponent<FreeLookEyeButton>();

            SerializedObject so = new SerializedObject(eyeBtn);
            if (eyeSprite != null) so.FindProperty("eyeIconSprite").objectReferenceValue = eyeSprite;
            so.FindProperty("buttonPosition").vector2Value = new Vector2(442f, -54f);
            so.FindProperty("buttonSize").vector2Value = new Vector2(80f, 80f);
            so.FindProperty("eyeCameraSensitivity").floatValue = 0.35f;
            so.FindProperty("cameraResetSpeed").floatValue = 8f;
            so.ApplyModifiedPropertiesWithoutUndo();
            eyeBtn.EnsureUIHierarchy();

            // 放置在 TouchLookArea 之后、弹窗面板（SettingsMenuPanel / InventoryPanel）之前，避免被右屏视角区抢占触控
            Transform settingsPanel = hudRoot.Find("SettingsMenuPanel");
            if (settingsPanel != null)
            {
                go.transform.SetSiblingIndex(Mathf.Max(0, settingsPanel.GetSiblingIndex()));
            }
            EditorUtility.SetDirty(eyeBtn);
        }

        private static void ConfigureVirtualJoystickSprint(Transform hudRoot)
        {
            if (hudRoot == null) return;
            Transform joyTr = hudRoot.Find("VirtualJoystick");
            if (joyTr == null) return;
            VirtualJoystick joy = joyTr.GetComponent<VirtualJoystick>();
            if (joy == null) return;

            SerializedObject so = new SerializedObject(joy);
            so.FindProperty("sprintAngleLimit").floatValue = 35f;
            so.FindProperty("sprintMagnitude").floatValue = 0.85f;
            so.FindProperty("lockMagnitude").floatValue = 1.25f;
            so.FindProperty("sprintNormalColor").colorValue = new Color(0.10f, 0.14f, 0.20f, 0.72f);
            so.FindProperty("sprintActiveColor").colorValue = Color.yellow;
            so.ApplyModifiedPropertiesWithoutUndo();
            joy.SetSprintActiveColor(Color.yellow);
            joy.EnsureCircularAppearance();
            EditorUtility.SetDirty(joy);
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
