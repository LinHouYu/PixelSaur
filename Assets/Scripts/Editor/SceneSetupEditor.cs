#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PixelSaur.Player;
using PixelSaur.Cam;
using PixelSaur.UI;
using PixelSaur.Environment;
using PixelSaur.Grass;
using PixelSaur.Water;

namespace PixelSaur.Editor
{
    /// <summary>
    /// 场景一键组装器：自动创建并关联角色、摄像机、虚拟摇杆UI、右屏触控、草地系统、水面系统与地形碰撞。
    /// </summary>
    public static class SceneSetupEditor
    {
        [MenuItem("PixelSaur/一键构建与配置完整游戏场景")]
        public static void SetupFullScene()
        {
            // 确保保存当前场景状态
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            // 1. 创建材质库
            Material grassMat = CreateOrGetMaterial("Assets/Materials/M_Grass.mat", "PixelSaur/GrassURP", mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.12f, 0.38f, 0.08f, 1f));
                mat.SetColor("_TipColor", new Color(0.48f, 0.82f, 0.18f, 1f));
                mat.SetFloat("_WindSpeed", 2.2f);
                mat.SetFloat("_WindStrength", 0.3f);
                mat.SetFloat("_InteractionStrength", 1.3f);
            });

            Material waterMat = CreateOrGetMaterial("Assets/Materials/M_Water.mat", "PixelSaur/WaterRippleURP", mat =>
            {
                mat.SetColor("_WaterColor", new Color(0.2f, 0.68f, 0.88f, 0.78f));
                mat.SetColor("_DeepWaterColor", new Color(0.04f, 0.22f, 0.52f, 0.95f));
                mat.SetColor("_RippleColor", Color.white);
                mat.SetFloat("_WaveSpeed", 1.2f);
                mat.SetFloat("_Smoothness", 0.88f);
            });

            Material playerMat = CreateOrGetMaterial("Assets/Materials/M_Player.mat", "Universal Render Pipeline/Lit", mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.95f, 0.45f, 0.15f, 1f)); // 活力恐龙橙
                mat.SetFloat("_Smoothness", 0.4f);
            });

            // 2. 配置 Island_Terrain 碰撞体
            GameObject terrainObj = GameObject.Find("Island_Terrain");
            if (terrainObj != null)
            {
                IslandTerrainColliderSetup colSetup = terrainObj.GetComponent<IslandTerrainColliderSetup>();
                if (colSetup == null) colSetup = terrainObj.AddComponent<IslandTerrainColliderSetup>();
                colSetup.SetupColliders();
            }

            // 3. 创建/配置胶囊体角色 (Player)
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player_Capsule";
                player.tag = "Player";

                // 移除自带的 CapsuleCollider，使用 CharacterController
                Collider defaultCol = player.GetComponent<Collider>();
                if (defaultCol != null) Object.DestroyImmediate(defaultCol);

                CharacterController cc = player.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.5f;
                cc.center = new Vector3(0, 1f, 0);

                // 赋予材质
                MeshRenderer mr = player.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = playerMat;

                // 放置在岛屿中心草地上方
                player.transform.position = new Vector3(0, 3f, 0);

                // 射线向下检测地面精准着陆
                if (Physics.Raycast(new Vector3(0, 20f, 0), Vector3.down, out RaycastHit hit, 50f))
                {
                    player.transform.position = hit.point + Vector3.up * 0.1f;
                }
            }

            PlayerInputProxy inputProxy = player.GetComponent<PlayerInputProxy>();
            if (inputProxy == null) inputProxy = player.AddComponent<PlayerInputProxy>();

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null) controller = player.AddComponent<PlayerController>();

            // 4. 配置第三人称相机 (Main Camera)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                ThirdPersonOrbitCamera orbitCam = mainCam.GetComponent<ThirdPersonOrbitCamera>();
                if (orbitCam == null) orbitCam = mainCam.gameObject.AddComponent<ThirdPersonOrbitCamera>();
                orbitCam.SetTarget(player.transform);
            }

            // 5. 配置草地系统 (GrassManager)
            GameObject grassMgrObj = GameObject.Find("GrassManager");
            if (grassMgrObj == null)
            {
                grassMgrObj = new GameObject("GrassManager");
            }
            GrassInteractionManager grassInterMgr = grassMgrObj.GetComponent<GrassInteractionManager>();
            if (grassInterMgr == null) grassInterMgr = grassMgrObj.AddComponent<GrassInteractionManager>();

            ProceduralGrassRenderer grassRenderer = grassMgrObj.GetComponent<ProceduralGrassRenderer>();
            if (grassRenderer == null) grassRenderer = grassMgrObj.AddComponent<ProceduralGrassRenderer>();

            // 关联草地材质与目标网格
            SerializedObject grassRendererSO = new SerializedObject(grassRenderer);
            grassRendererSO.FindProperty("grassMaterial").objectReferenceValue = grassMat;
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

            // 6. 配置水面系统 (WaterManager & WaterSurface)
            GameObject waterObj = GameObject.Find("WaterSurface_Plane");
            if (waterObj == null)
            {
                waterObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
                waterObj.name = "WaterSurface_Plane";
                waterObj.transform.position = new Vector3(0f, 0.4f, 0f); // 适中水面高度
                waterObj.transform.localScale = new Vector3(20f, 1f, 20f); // 200x200m 水域

                // 移除水体刚性碰撞（保留触发器或无碰撞）
                Collider waterCol = waterObj.GetComponent<Collider>();
                if (waterCol != null) Object.DestroyImmediate(waterCol);
            }

            MeshRenderer waterMR = waterObj.GetComponent<MeshRenderer>();
            if (waterMR != null) waterMR.sharedMaterial = waterMat;

            WaterSurface waterSurface = waterObj.GetComponent<WaterSurface>();
            if (waterSurface == null) waterSurface = waterObj.AddComponent<WaterSurface>();

            GameObject waterMgrObj = GameObject.Find("WaterRippleManager");
            if (waterMgrObj == null) waterMgrObj = new GameObject("WaterRippleManager");
            WaterRippleManager rippleMgr = waterMgrObj.GetComponent<WaterRippleManager>();
            if (rippleMgr == null) rippleMgr = waterMgrObj.AddComponent<WaterRippleManager>();

            // 7. 配置 UI Canvas（虚拟摇杆与右屏滑动区域）
            SetupUI(inputProxy);

            // 保存场景
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("<color=#00FF00>[PixelSaur] 完整游戏场景配置成功！已包含角色、摇杆、触控、64分辨率草地互动与256水面涟漪。</color>");
        }

        private static void SetupUI(PlayerInputProxy inputProxy)
        {
            // 确保 EventSystem 存在
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

            // 1. 左下角虚拟摇杆
            Transform joystickTr = canvas.transform.Find("VirtualJoystick");
            if (joystickTr == null)
            {
                GameObject joystickObj = new GameObject("VirtualJoystick", typeof(RectTransform));
                joystickObj.transform.SetParent(canvas.transform, false);

                RectTransform joyRT = joystickObj.GetComponent<RectTransform>();
                joyRT.anchorMin = new Vector2(0f, 0f);
                joyRT.anchorMax = new Vector2(0f, 0f);
                joyRT.pivot = new Vector2(0.5f, 0.5f);
                joyRT.anchoredPosition = new Vector2(220f, 220f);
                joyRT.sizeDelta = new Vector2(220f, 220f);

                Image bgImg = joystickObj.AddComponent<Image>();
                bgImg.color = new Color(0f, 0f, 0f, 0.35f);

                // 操纵钮 Handle
                GameObject handleObj = new GameObject("Handle", typeof(RectTransform));
                handleObj.transform.SetParent(joystickObj.transform, false);
                RectTransform handleRT = handleObj.GetComponent<RectTransform>();
                handleRT.sizeDelta = new Vector2(85f, 85f);
                Image handleImg = handleObj.AddComponent<Image>();
                handleImg.color = new Color(1f, 1f, 1f, 0.75f);

                VirtualJoystick vj = joystickObj.AddComponent<VirtualJoystick>();

                SerializedObject vjSO = new SerializedObject(vj);
                vjSO.FindProperty("background").objectReferenceValue = joyRT;
                vjSO.FindProperty("handle").objectReferenceValue = handleRT;
                vjSO.FindProperty("handleRange").floatValue = 75f;
                vjSO.ApplyModifiedProperties();
            }

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

                // 透明但接收射线检测
                Image touchImg = touchObj.AddComponent<Image>();
                touchImg.color = Color.clear;
                touchImg.raycastTarget = true;

                touchObj.AddComponent<TouchLookArea>();
            }
        }

        private static Material CreateOrGetMaterial(string assetPath, string shaderName, System.Action<Material> initAction)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogError($"[SceneSetupEditor] 未找到着色器: {shaderName}");
                    return null;
                }
                mat = new Material(shader);
                initAction?.Invoke(mat);
                AssetDatabase.CreateAsset(mat, assetPath);
                AssetDatabase.SaveAssets();
            }
            return mat;
        }
    }
}
#endif
