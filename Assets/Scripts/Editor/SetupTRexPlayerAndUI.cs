#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PixelSaur.Player;
using PixelSaur.UI;

namespace PixelSaur.Editor
{
    /// <summary>
    /// 一次性场景与资产配置工具：
    /// 1. 配置霸王龙贴图 Point 采样、FBX 11 套动画循环属性与 AnimatorController；
    /// 2. 将 Player_Capsule 视觉模型替换为 Minecraft_T_Rex 并绑定 DinoAnimatorController；
    /// 3. 在 HUD_Canvas 场景中实体化 DinoSkill1Button / DinoSkill2Button，并为右侧所有虚拟按钮添加灰色像素圆环。
    /// </summary>
    public static class SetupTRexPlayerAndUI
    {
        private const string FbxPath = "Assets/Model/霸王龙/Minecraft_T_Rex.fbx";
        private const string HealthyTexPath = "Assets/Model/霸王龙/trex_128_atlas.png";
        private const string InjuredTexPath = "Assets/Model/霸王龙/trex_128_atlas_injured.png";
        private const string ControllerPath = "Assets/Model/霸王龙/TRexAnimatorController.controller";
        private const string HealthyMatPath = "Assets/Model/霸王龙/M_TRex_Healthy.mat";
        private const string GrayRingTexPath = "Assets/Textures/UI/ui_pixel_gray_ring_64x64.png";

        private static readonly HashSet<string> LoopClips = new HashSet<string>
        {
            "animation.idle",
            "animation.walk",
            "animation.run",
            "animation.sprint",
            "animation.sneak",
            "animation.sneak_walk"
        };

        [MenuItem("PixelSaur/应用霸王龙模型与右侧按钮配置")]
        public static string Main()
        {
            ConfigureTextures();
            ConfigureFbxAnimations();
            AnimatorController controller = BuildAnimatorController();
            Material healthyMat = EnsureHealthyMaterial();

            SetupPlayerModel(controller, healthyMat);
            SetupRightSideButtons();

            var activeScene = EditorSceneManager.GetActiveScene();
            bool saved = EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"OK: Saved={saved}, Controller={controller.name}, Mat={healthyMat != null}";
        }

        private static void ConfigureTextures()
        {
            ConfigurePointSprite(GrayRingTexPath);
            ConfigurePointTexture(HealthyTexPath);
            ConfigurePointTexture(InjuredTexPath);
        }

        private static void ConfigurePointSprite(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }

        private static void ConfigurePointTexture(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }

        private static void ConfigureFbxAnimations()
        {
            var mi = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (mi == null) return;

            mi.animationType = ModelImporterAnimationType.Generic;
            var clips = mi.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = mi.defaultClipAnimations;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                bool shouldLoop = LoopClips.Contains(clips[i].name);
                clips[i].loopTime = shouldLoop;
                clips[i].loopPose = shouldLoop;
            }
            mi.clipAnimations = clips;
            mi.SaveAndReimport();
        }

        private static AnimatorController BuildAnimatorController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            AnimatorState idleState = null;

            foreach (Object asset in allAssets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    string stateName = clip.name.StartsWith("animation.")
                        ? clip.name.Substring("animation.".Length)
                        : clip.name;
                    AnimatorState st = sm.AddState(stateName);
                    st.motion = clip;
                    if (stateName == "idle")
                    {
                        idleState = st;
                    }
                }
            }

            if (idleState != null)
            {
                sm.defaultState = idleState;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static Material EnsureHealthyMaterial()
        {
            Texture2D healthyTex = AssetDatabase.LoadAssetAtPath<Texture2D>(HealthyTexPath);
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(HealthyMatPath);

            Shader shader = null;
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            foreach (Object asset in allAssets)
            {
                if (asset is Material fbxMat && fbxMat.shader != null)
                {
                    shader = fbxMat.shader;
                    break;
                }
            }
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
            }

            if (existing == null)
            {
                existing = new Material(shader);
                AssetDatabase.CreateAsset(existing, HealthyMatPath);
            }
            else
            {
                existing.shader = shader;
            }

            if (healthyTex != null)
            {
                if (existing.HasProperty("_BaseMap")) existing.SetTexture("_BaseMap", healthyTex);
                if (existing.HasProperty("_MainTex")) existing.SetTexture("_MainTex", healthyTex);
            }
            if (existing.HasProperty("_Smoothness")) existing.SetFloat("_Smoothness", 0.1f);

            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void SetupPlayerModel(AnimatorController controller, Material healthyMat)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Player_Capsule");
            if (player == null)
            {
                Debug.LogError("[SetupTRexPlayerAndUI] 未找到 Player_Capsule!");
                return;
            }

            // 隐藏旧胶囊网格但保留 CharacterController 与所有组件
            MeshRenderer capsuleMr = player.GetComponent<MeshRenderer>();
            if (capsuleMr != null)
            {
                capsuleMr.enabled = false;
                EditorUtility.SetDirty(capsuleMr);
            }

            Transform oldModel = player.transform.Find("TRexModel");
            if (oldModel != null)
            {
                Object.DestroyImmediate(oldModel.gameObject);
            }

            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            GameObject modelInst = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab, player.transform);
            modelInst.name = "TRexModel";
            // 胶囊体 CharacterController 高度 2.0，底部在 y = -1.0；霸王龙嘴部朝向 -Z，故绕 Y 旋转 180 度对齐 +Z
            modelInst.transform.localPosition = new Vector3(0f, -1.0f, 0f);
            modelInst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            modelInst.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);

            Animator anim = modelInst.GetComponent<Animator>();
            if (anim == null) anim = modelInst.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            EditorUtility.SetDirty(anim);

            // 统一替换为健康原皮材质，消除 FBX 内部残留受伤材质槽
            foreach (Renderer r in modelInst.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = healthyMat;
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }

            DinoAnimatorController dinoAnim = player.GetComponent<DinoAnimatorController>();
            if (dinoAnim == null) dinoAnim = player.AddComponent<DinoAnimatorController>();
            SerializedObject so = new SerializedObject(dinoAnim);
            so.FindProperty("modelAnimator").objectReferenceValue = anim;
            so.FindProperty("healthyTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(HealthyTexPath);
            so.FindProperty("injuredTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(InjuredTexPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dinoAnim);
        }

        private static void SetupRightSideButtons()
        {
            GameObject canvas = GameObject.Find("HUD_Canvas");
            if (canvas == null)
            {
                Debug.LogError("[SetupTRexPlayerAndUI] 未找到 HUD_Canvas!");
                return;
            }

            // 创建两个可在编辑器直接微调的场景技能按钮
            GameObject skill1 = EnsureSceneSkillButton(
                canvas.transform,
                "DinoSkill1Button",
                new Vector2(-365f, 365f),
                new Vector2(136f, 136f),
                DinoSkillButton.SkillSlot.ChargeDash,
                "Assets/Textures/UI/icon_dino_skill1.png"
            );

            GameObject skill2 = EnsureSceneSkillButton(
                canvas.transform,
                "DinoSkill2Button",
                new Vector2(-530f, 310f),
                new Vector2(136f, 136f),
                DinoSkillButton.SkillSlot.Roar,
                "Assets/Textures/UI/icon_dino_skill2.png"
            );

            // 为右侧全部 5 个虚拟按钮添加一圈灰色像素圆环
            Sprite ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GrayRingTexPath);
            string[] rightButtons =
            {
                "DinoAttackButton",
                "DinoJumpButton",
                "DinoSneakButton",
                "DinoSkill1Button",
                "DinoSkill2Button"
            };

            foreach (string btnName in rightButtons)
            {
                Transform btnTr = canvas.transform.Find(btnName);
                if (btnTr == null) continue;
                EnsureGrayPixelRing(btnTr, ringSprite);
            }

            if (skill1 != null) EditorUtility.SetDirty(skill1);
            if (skill2 != null) EditorUtility.SetDirty(skill2);
        }

        private static GameObject EnsureSceneSkillButton(
            Transform canvasTransform,
            string name,
            Vector2 anchoredPos,
            Vector2 size,
            DinoSkillButton.SkillSlot slot,
            string iconPath)
        {
            Transform existing = canvasTransform.Find(name);
            GameObject btnObj = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(DinoSkillButton));

            if (existing == null)
            {
                btnObj.transform.SetParent(canvasTransform, false);
            }

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            Image img = btnObj.GetComponent<Image>();
            if (img == null) img = btnObj.AddComponent<Image>();
            img.sprite = iconSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white;
            img.raycastTarget = true;

            DinoSkillButton skillBtn = btnObj.GetComponent<DinoSkillButton>();
            if (skillBtn == null) skillBtn = btnObj.AddComponent<DinoSkillButton>();
            SerializedObject so = new SerializedObject(skillBtn);
            so.FindProperty("skillSlot").enumValueIndex = (int)slot - 1;
            so.FindProperty("skillIconSprite").objectReferenceValue = iconSprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            skillBtn.EnsureAppearance();

            return btnObj;
        }

        private static void EnsureGrayPixelRing(Transform btnTr, Sprite ringSprite)
        {
            Transform ringTr = btnTr.Find("PixelGrayRing");
            GameObject ringObj = ringTr != null
                ? ringTr.gameObject
                : new GameObject("PixelGrayRing", typeof(RectTransform), typeof(Image));

            if (ringTr == null)
            {
                ringObj.transform.SetParent(btnTr, false);
            }

            ringObj.transform.SetAsFirstSibling();

            RectTransform rt = ringObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-8f, -8f);
            rt.offsetMax = new Vector2(8f, 8f);

            Image img = ringObj.GetComponent<Image>();
            if (img == null) img = ringObj.AddComponent<Image>();
            img.sprite = ringSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white;
            img.raycastTarget = false;
            EditorUtility.SetDirty(ringObj);
        }
    }
}
#endif
