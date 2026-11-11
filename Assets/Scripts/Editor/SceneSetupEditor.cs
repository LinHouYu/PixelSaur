#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using PixelSaur.Player;
using PixelSaur.Cam;
using PixelSaur.UI;
using PixelSaur.Environment;
using PixelSaur.Grass;
using PixelSaur.Water;
using PixelSaur.Combat;
using PixelSaur.Creatures;

namespace PixelSaur.Editor
{
    /// <summary>
    /// 场景一键组装器：自动创建并关联角色、摄像机、圆形虚拟摇杆UI、右屏触控、64分辨率交互草地、256分辨率MinionsArt风格海面与防穿模地形碰撞。
    /// </summary>
    public static class SceneSetupEditor
    {
        [MenuItem("PixelSaur/仅刷新UI状态条")]
        public static void RefreshOnlyUI()
        {
            var panel = UnityEngine.GameObject.Find("StatusBarsPanel");
            if (panel != null)
            {
                var ui = panel.GetComponent<PixelSaur.UI.PlayerStatusUI>();
                if (ui != null)
                {
                    ui.EnsureUIHierarchy();
                    UnityEditor.EditorUtility.SetDirty(panel);
                    UnityEditor.EditorUtility.SetDirty(ui);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                    Debug.Log("<color=#00FF00>[PixelSaur] UI 状态条已单独刷新完毕！未触碰草地和水体管理器。</color>");
                }
            }
        }

        [MenuItem("PixelSaur/一键构建与配置完整游戏场景")]
        public static void SetupFullScene()
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            // 1. 创建或更新核心材质库
            Material grassMat = CreateOrUpdateMaterial("Assets/Materials/M_Grass.mat", "PixelSaur/GrassURP", mat =>
            {
                mat.enableInstancing = true;
                mat.SetColor("_BaseColor", new Color(0.10f, 0.36f, 0.08f, 1f));
                mat.SetColor("_TipColor", new Color(0.48f, 0.84f, 0.18f, 1f));
                mat.SetColor("_PressedColor", new Color(0.22f, 0.52f, 0.12f, 1f));
                mat.SetFloat("_WindSpeed", 2.5f);
                mat.SetFloat("_WindStrength", 0.22f);
                mat.SetFloat("_WindFrequency", 0.6f);
                mat.SetFloat("_MaxBendAngle", 45.0f);
                mat.SetFloat("_InteractionStrength", 1.0f);
            });

            Material waterMat = CreateOrUpdateMaterial("Assets/Materials/M_Water.mat", "PixelSaur/WaterRippleURP", mat =>
            {
                mat.SetColor("_WaterColor", new Color(0.22f, 0.78f, 0.88f, 0.78f));
                mat.SetColor("_DeepWaterColor", new Color(0.06f, 0.34f, 0.68f, 0.94f));
                mat.SetColor("_FoamColor", new Color(1.0f, 1.0f, 1.0f, 0.95f));
                mat.SetColor("_RippleColor", Color.white);
                mat.SetFloat("_DepthMaxDistance", 0.42f);
                mat.SetFloat("_FoamDistance", 0.22f);
                mat.SetFloat("_FoamCutoff", 0.52f);
                mat.SetFloat("_WaveSpeed", 0.65f);
                mat.SetFloat("_WaveFrequency", 1.6f);
                mat.SetFloat("_WaveHeight", 0.002f);
                mat.SetFloat("_Smoothness", 0.85f);
            });

            Material playerMat = CreateOrUpdateMaterial("Assets/Materials/M_Player.mat", "Universal Render Pipeline/Lit", mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.95f, 0.45f, 0.15f, 1f));
                mat.SetFloat("_Smoothness", 0.45f);
            });

            // 2. 配置 Island_Terrain 防穿模实心碰撞体与环岛浅海缓坡海床
            GameObject terrainObj = GameObject.Find("Island_Terrain");
            if (terrainObj != null)
            {
                IslandTerrainColliderSetup colSetup = terrainObj.GetComponent<IslandTerrainColliderSetup>();
                if (colSetup == null) colSetup = terrainObj.AddComponent<IslandTerrainColliderSetup>();
                colSetup.SetupColliders();
            }

            // 3. 创建/配置胶囊体角色 (Player_Capsule)
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player_Capsule";
                player.tag = "Player";
            }

            // 移除可能残留的 CapsuleCollider，统一使用与网格中心 (0,0,0) 完全对齐的 CharacterController
            Collider defaultCol = player.GetComponent<CapsuleCollider>();
            if (defaultCol != null) Object.DestroyImmediate(defaultCol);

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc == null) cc = player.AddComponent<CharacterController>();
            cc.height = 2.0f;
            cc.radius = 0.5f;
            cc.center = Vector3.zero; // 关键修复：与 Unity Capsule 网格中心 (0,0,0) 严格一致，防止半截身体入地穿模
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.03f;

            MeshRenderer playerMR = player.GetComponent<MeshRenderer>();
            if (playerMR != null) playerMR.sharedMaterial = playerMat;

            // 设定与海岛比例协调的角色尺寸，并精确放置在草地表面 (Y = 0.0781) 之上
            player.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            float capsuleHalfHeight = 1.0f * player.transform.localScale.y; // 0.55m
            float grassTopY = 0.0781f;
            player.transform.position = new Vector3(0f, grassTopY + capsuleHalfHeight + 0.03f, 0f);

            PlayerInputProxy inputProxy = player.GetComponent<PlayerInputProxy>();
            if (inputProxy == null) inputProxy = player.AddComponent<PlayerInputProxy>();

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null) controller = player.AddComponent<PlayerController>();

            // 3.1 恐龙近战战斗与交互组件 (DinoCombatController, DinoInteractionController, DinoAttackIndicator)
            DinoCombatController combat = player.GetComponent<DinoCombatController>();
            if (combat == null) combat = player.AddComponent<DinoCombatController>();

            DinoInteractionController interaction = player.GetComponent<DinoInteractionController>();
            if (interaction == null) interaction = player.AddComponent<DinoInteractionController>();

            Transform indicatorTr = player.transform.Find("AttackIndicator");
            if (indicatorTr == null)
            {
                GameObject indObj = new GameObject("AttackIndicator");
                indObj.transform.SetParent(player.transform, false);
                indObj.AddComponent<DinoAttackIndicator>();
            }

            // 3.2 创建双测试胶囊生物 (红色危险生物、绿色猎物)
            SetupTestCreatures(player.transform.position);

            // 4. 配置第三人称相机 (Main Camera) 并开启 Depth Texture 支持 MinionsArt 海岸线泡沫
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                UniversalAdditionalCameraData camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
                if (camData != null)
                {
                    camData.requiresDepthTexture = true;
                }

                ThirdPersonOrbitCamera orbitCam = mainCam.GetComponent<ThirdPersonOrbitCamera>();
                if (orbitCam == null) orbitCam = mainCam.gameObject.AddComponent<ThirdPersonOrbitCamera>();
                orbitCam.SetTarget(player.transform);
            }

            // 5. 配置草地系统 (GrassManager, 严格保护用户调好的参数，绝不覆盖恢复出厂设置)
            GameObject grassMgrObj = GameObject.Find("GrassManager");
            if (grassMgrObj == null)
            {
                grassMgrObj = new GameObject("GrassManager");
            }
            GrassInteractionManager grassInterMgr = grassMgrObj.GetComponent<GrassInteractionManager>();
            bool isNewInter = (grassInterMgr == null);
            if (isNewInter) grassInterMgr = grassMgrObj.AddComponent<GrassInteractionManager>();

            if (isNewInter)
            {
                SerializedObject grassInterSO = new SerializedObject(grassInterMgr);
                grassInterSO.FindProperty("targetPlayer").objectReferenceValue = player.transform;
                grassInterSO.FindProperty("rtResolution").intValue = 64;
                grassInterSO.FindProperty("interactionAreaSize").floatValue = 14f;
                grassInterSO.FindProperty("footprintRadius").floatValue = 0.4f;
                grassInterSO.FindProperty("footprintHardness").floatValue = 1.2f;
                grassInterSO.FindProperty("recoverySpeed").floatValue = 0.75f;
                grassInterSO.ApplyModifiedProperties();
            }
            else
            {
                var so = new SerializedObject(grassInterMgr);
                var p = so.FindProperty("targetPlayer");
                if (p.objectReferenceValue == null)
                {
                    p.objectReferenceValue = player.transform;
                    so.ApplyModifiedProperties();
                }
            }

            ProceduralGrassRenderer grassRenderer = grassMgrObj.GetComponent<ProceduralGrassRenderer>();
            bool isNewRenderer = (grassRenderer == null);
            if (isNewRenderer) grassRenderer = grassMgrObj.AddComponent<ProceduralGrassRenderer>();

            if (isNewRenderer)
            {
                SerializedObject grassRendererSO = new SerializedObject(grassRenderer);
                grassRendererSO.FindProperty("grassMaterial").objectReferenceValue = grassMat;
                grassRendererSO.FindProperty("grassCount").intValue = 1400;
                grassRendererSO.FindProperty("bladeHeight").floatValue = 0.72f;
                grassRendererSO.FindProperty("bladeWidth").floatValue = 0.12f;
                GameObject grassCenterObj = GameObject.Find("grass_center") ?? GameObject.Find("Grass_Plain");
                if (grassCenterObj != null)
                {
                    MeshFilter mf = grassCenterObj.GetComponentInChildren<MeshFilter>();
                    if (mf != null)
                    {
                        grassRendererSO.FindProperty("targetMeshFilter").objectReferenceValue = mf;
                    }
                }
                grassRendererSO.ApplyModifiedProperties();
                grassRenderer.RebuildGrass();
            }

            // 6. 配置水面系统 (WaterSurface_Plane & WaterRippleManager，严格保护用户调好的参数)
            // 关键：sand_main 顶面高度为 0.0625，grass_center 顶面为 0.0781，水面严格设在 Y = 0.038f 绝不没过岛屿！
            const float optimalWaterY = 0.038f;
            GameObject waterObj = GameObject.Find("WaterSurface_Plane");
            if (waterObj == null)
            {
                waterObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
                waterObj.name = "WaterSurface_Plane";
            }
            waterObj.transform.position = new Vector3(0f, optimalWaterY, 0f);
            waterObj.transform.localScale = new Vector3(20f, 1f, 20f);

            Collider waterCol = waterObj.GetComponent<Collider>();
            if (waterCol != null) Object.DestroyImmediate(waterCol);

            MeshRenderer waterMR = waterObj.GetComponent<MeshRenderer>();
            if (waterMR != null)
            {
                waterMR.sharedMaterial = waterMat;
                waterMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            WaterSurface waterSurface = waterObj.GetComponent<WaterSurface>();
            if (waterSurface == null) waterSurface = waterObj.AddComponent<WaterSurface>();
            waterSurface.ApplyProperties();

            GameObject waterMgrObj = GameObject.Find("WaterRippleManager");
            if (waterMgrObj == null) waterMgrObj = new GameObject("WaterRippleManager");
            WaterRippleManager rippleMgr = waterMgrObj.GetComponent<WaterRippleManager>();
            bool isNewRipple = (rippleMgr == null);
            if (isNewRipple) rippleMgr = waterMgrObj.AddComponent<WaterRippleManager>();

            if (isNewRipple)
            {
                SerializedObject rippleSO = new SerializedObject(rippleMgr);
                rippleSO.FindProperty("targetPlayer").objectReferenceValue = player.transform;
                rippleSO.FindProperty("waterLevel").floatValue = optimalWaterY;
                rippleSO.FindProperty("rtResolution").intValue = 512;
                rippleSO.FindProperty("orthographicCamSize").floatValue = 14f;
                rippleSO.FindProperty("rippleExpandSpeed").floatValue = 1.1f;
                rippleSO.FindProperty("maxRippleWorldRadius").floatValue = 2.6f;
                rippleSO.FindProperty("rippleRingThickness").floatValue = 0.008f;
                rippleSO.FindProperty("rippleFadeSpeed").floatValue = 3.2f;
                rippleSO.FindProperty("moveRippleInterval").floatValue = 0.2f;
                rippleSO.FindProperty("minMoveDistance").floatValue = 0.08f;
                rippleSO.FindProperty("enableIdleRipple").boolValue = false;
                rippleSO.FindProperty("idleRippleInterval").floatValue = 2.5f;
                rippleSO.ApplyModifiedProperties();
            }
            else
            {
                var so = new SerializedObject(rippleMgr);
                var p = so.FindProperty("targetPlayer");
                if (p.objectReferenceValue == null)
                {
                    p.objectReferenceValue = player.transform;
                    so.ApplyModifiedProperties();
                }
            }

            // 7. 配置 UI Canvas（正圆形虚拟摇杆与右屏滑动区域）
            SetupUI(inputProxy);

            EditorSceneManager.SaveOpenScenes();
            Debug.Log("<color=#00FF00>[PixelSaur] 场景已重新校准保存！碰撞体加固防穿模、MinionsArt海岸泡沫+256涉水涟漪、64草地互动与正圆形虚拟摇杆已全部就绪。</color>");
        }

        private static void SetupUI(PlayerInputProxy inputProxy)
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("HUD_Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // 1. 左下角正圆形虚拟摇杆
            Transform joystickTr = canvas.transform.Find("VirtualJoystick");
            GameObject joystickObj;
            if (joystickTr == null)
            {
                joystickObj = new GameObject("VirtualJoystick", typeof(RectTransform));
                joystickObj.transform.SetParent(canvas.transform, false);
            }
            else
            {
                joystickObj = joystickTr.gameObject;
            }

            RectTransform joyRT = joystickObj.GetComponent<RectTransform>();
            joyRT.anchorMin = new Vector2(0f, 0f);
            joyRT.anchorMax = new Vector2(0f, 0f);
            joyRT.pivot = new Vector2(0.5f, 0.5f);
            joyRT.anchoredPosition = new Vector2(220f, 220f);
            joyRT.sizeDelta = new Vector2(220f, 220f);

            Image bgImg = joystickObj.GetComponent<Image>();
            if (bgImg == null) bgImg = joystickObj.AddComponent<Image>();
            bgImg.color = new Color(0.10f, 0.14f, 0.20f, 0.72f);
            bgImg.sprite = null; // 交由 EnsureCircularAppearance 生成抗锯齿圆盘精灵

            Transform handleTr = joystickObj.transform.Find("Handle");
            GameObject handleObj;
            if (handleTr == null)
            {
                handleObj = new GameObject("Handle", typeof(RectTransform));
                handleObj.transform.SetParent(joystickObj.transform, false);
            }
            else
            {
                handleObj = handleTr.gameObject;
            }

            RectTransform handleRT = handleObj.GetComponent<RectTransform>();
            handleRT.anchorMin = new Vector2(0.5f, 0.5f);
            handleRT.anchorMax = new Vector2(0.5f, 0.5f);
            handleRT.pivot = new Vector2(0.5f, 0.5f);
            handleRT.anchoredPosition = Vector2.zero;
            handleRT.sizeDelta = new Vector2(88f, 88f);

            Image handleImg = handleObj.GetComponent<Image>();
            if (handleImg == null) handleImg = handleObj.AddComponent<Image>();
            handleImg.color = new Color(1f, 1f, 1f, 0.90f);
            handleImg.sprite = null;

            VirtualJoystick vj = joystickObj.GetComponent<VirtualJoystick>();
            if (vj == null) vj = joystickObj.AddComponent<VirtualJoystick>();

            SerializedObject vjSO = new SerializedObject(vj);
            vjSO.FindProperty("background").objectReferenceValue = joyRT;
            vjSO.FindProperty("handle").objectReferenceValue = handleRT;
            vjSO.FindProperty("handleRange").floatValue = 75f;
            vjSO.ApplyModifiedProperties();

            vj.EnsureCircularAppearance();

            // 2. 右半屏触控视角区域
            Transform touchAreaTr = canvas.transform.Find("TouchLookArea");
            if (touchAreaTr == null)
            {
                GameObject touchObj = new GameObject("TouchLookArea", typeof(RectTransform));
                touchObj.transform.SetParent(canvas.transform, false);

                RectTransform touchRT = touchObj.GetComponent<RectTransform>();
                touchRT.anchorMin = new Vector2(0.35f, 0f);
                touchRT.anchorMax = new Vector2(1f, 1f);
                touchRT.offsetMin = Vector2.zero;
                touchRT.offsetMax = Vector2.zero;

                Image touchImg = touchObj.AddComponent<Image>();
                touchImg.color = Color.clear;
                touchImg.raycastTarget = true;

                touchObj.AddComponent<TouchLookArea>();
            }

            // 3. 屏幕底部居中生存状态条 (生命、食物、口渴，顶部居中图标)
            Transform statusTr = canvas.transform.Find("StatusBarsPanel");
            GameObject statusObj;
            if (statusTr == null)
            {
                statusObj = new GameObject("StatusBarsPanel", typeof(RectTransform));
                statusObj.transform.SetParent(canvas.transform, false);
            }
            else
            {
                statusObj = statusTr.gameObject;
            }

            PlayerStatusUI statusUI = statusObj.GetComponent<PlayerStatusUI>();
            if (statusUI == null) statusUI = statusObj.AddComponent<PlayerStatusUI>();
            statusUI.EnsureUIHierarchy();

            // 4. 右下角圆形攻击与双击冲击技能按钮
            SetupAttackButton(canvas);
        }

        private static Material CreateOrUpdateMaterial(string assetPath, string shaderName, System.Action<Material> initAction)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[SceneSetupEditor] 未找到着色器: {shaderName}");
                return null;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(shader);
                initAction?.Invoke(mat);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else
            {
                mat.shader = shader;
                initAction?.Invoke(mat);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            return mat;
        }
        private static void SetupAttackButton(Canvas canvas)
        {
            Transform attackTr = canvas.transform.Find("DinoAttackButton");
            GameObject attackObj;
            if (attackTr == null)
            {
                attackObj = new GameObject("DinoAttackButton", typeof(RectTransform));
                attackObj.transform.SetParent(canvas.transform, false);
            }
            else
            {
                attackObj = attackTr.gameObject;
            }

            RectTransform attackRT = attackObj.GetComponent<RectTransform>();
            attackRT.anchorMin = new Vector2(1f, 0f);
            attackRT.anchorMax = new Vector2(1f, 0f);
            attackRT.pivot = new Vector2(0.5f, 0.5f);
            attackRT.anchoredPosition = new Vector2(-190f, 190f);
            attackRT.sizeDelta = new Vector2(140f, 140f);

            Image bgImg = attackObj.GetComponent<Image>();
            if (bgImg == null) bgImg = attackObj.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.16f, 0.22f, 0.75f);

            Transform iconTr = attackObj.transform.Find("Icon");
            GameObject iconObj = iconTr != null ? iconTr.gameObject : new GameObject("Icon", typeof(RectTransform), typeof(Image));
            RectTransform iconRT = iconObj.GetComponent<RectTransform>();
            iconRT.SetParent(attackRT, false);
            iconRT.anchorMin = new Vector2(0.5f, 0.5f);
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = Vector2.zero;
            iconRT.sizeDelta = new Vector2(64f, 64f);

            Image iconImg = iconObj.GetComponent<Image>();
            iconImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_bite_32x32.png");
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            DinoAttackButton btn = attackObj.GetComponent<DinoAttackButton>();
            if (btn == null) btn = attackObj.AddComponent<DinoAttackButton>();
            btn.EnsureAppearance();
        }

        private static void SetupTestCreatures(Vector3 playerPos)
        {
            // 1. 红色危险生物胶囊 (碰一下扣20血，0.4s间隔)
            if (Object.FindAnyObjectByType<RedHazardCapsule>() == null)
            {
                GameObject redObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                redObj.name = "Creature_RedHazard";
                redObj.transform.position = playerPos + new Vector3(3.5f, 0.35f, 2.5f);
                redObj.transform.localScale = Vector3.one * 1.0f;

                redObj.AddComponent<RedHazardCapsule>();

                // 设置醒目鲜红材质
                MeshRenderer mr = redObj.GetComponent<MeshRenderer>();
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    Material redMat = new Material(shader) { name = "M_RedHazard_Capsule" };
                    redMat.SetColor("_BaseColor", new Color(0.95f, 0.15f, 0.15f, 1f));
                    mr.sharedMaterial = redMat;
                }
            }

            // 2. 绿色猎物生物胶囊 (100血量，死后倒地可吃3次)
            if (Object.FindAnyObjectByType<GreenPreyCapsule>() == null)
            {
                GameObject greenObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                greenObj.name = "Creature_GreenPrey";
                greenObj.transform.position = playerPos + new Vector3(-3.2f, 0.35f, 2.2f);
                greenObj.transform.localScale = Vector3.one * 1.0f;

                greenObj.AddComponent<GreenPreyCapsule>();

                // 设置初始翠绿材质
                MeshRenderer mr = greenObj.GetComponent<MeshRenderer>();
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    Material greenMat = new Material(shader) { name = "M_GreenPrey_Capsule" };
                    greenMat.SetColor("_BaseColor", new Color(0.25f, 0.85f, 0.35f, 1f));
                    mr.sharedMaterial = greenMat;
                }
            }
        }
    }
}
#endif
