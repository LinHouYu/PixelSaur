using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelSaur.UI
{
    [Serializable]
    public class HudElementLayoutData
    {
        public string id;
        public float posX;
        public float posY;
        public float width;
        public float height;

        public HudElementLayoutData Clone()
        {
            return new HudElementLayoutData
            {
                id = id,
                posX = posX,
                posY = posY,
                width = width,
                height = height
            };
        }

        public bool ApproximatelyEquals(HudElementLayoutData other)
        {
            if (other == null) return false;
            return id == other.id
                && Mathf.Abs(posX - other.posX) < 0.5f
                && Mathf.Abs(posY - other.posY) < 0.5f
                && Mathf.Abs(width - other.width) < 0.5f
                && Mathf.Abs(height - other.height) < 0.5f;
        }
    }

    [Serializable]
    public class HudLayoutSavePayload
    {
        public List<HudElementLayoutData> items = new List<HudElementLayoutData>();
    }

    public class HudElementMeta
    {
        public string id;
        public string displayName;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 defaultPos;
        public Vector2 defaultSize;
        public Vector2 minSize;
        public Vector2 maxSize;
        public bool lockSquareAspect;
    }

    /// <summary>
    /// 跨场景 HUD 布局管理器：
    /// 1. 统一管理除 TopBarStatusHUD 外所有 9 个游戏局内 UI 的默认放大（1.5x）无重叠布局与自定义布局；
    /// 2. 所有尺寸调整均直接作用于 RectTransform.sizeDelta，严格保持 localScale 固定为 (1, 1, 1)；
    /// 3. StartMenu 与 Game 共享同一份 PlayerPrefs JSON 持久化数据，切入 Game 场景时自动同步生效。
    /// </summary>
    public static class HudLayoutManager
    {
        public const string PrefsKey = "PIXELSAUR_HUD_LAYOUT_V1";

        public const string IdVirtualJoystick = "VirtualJoystick";
        public const string IdDialogueButton = "DialogueButton";
        public const string IdStatusBarsPanel = "StatusBarsPanel";
        public const string IdInteractionProgressBar = "InteractionProgressBar";
        public const string IdDinoAttackButton = "DinoAttackButton";
        public const string IdDinoJumpButton = "DinoJumpButton";
        public const string IdDinoSneakButton = "DinoSneakButton";
        public const string IdDinoSkill1Button = "DinoSkill1Button";
        public const string IdDinoSkill2Button = "DinoSkill2Button";

        public static readonly HudElementMeta[] EditableElements =
        {
            new HudElementMeta
            {
                id = IdVirtualJoystick,
                displayName = "虚拟摇杆",
                anchorMin = new Vector2(0f, 0f),
                anchorMax = new Vector2(0f, 0f),
                pivot = new Vector2(0.5f, 0.5f),
                defaultPos = new Vector2(335f, 330f),
                defaultSize = new Vector2(320f, 320f),
                minSize = new Vector2(140f, 140f),
                maxSize = new Vector2(480f, 480f),
                lockSquareAspect = true
            },
            new HudElementMeta
            {
                id = IdDialogueButton,
                displayName = "对话按钮",
                anchorMin = new Vector2(0f, 0f),
                anchorMax = new Vector2(0f, 0f),
                pivot = new Vector2(0.5f, 0.5f),
                defaultPos = new Vector2(90f, 90f),
                defaultSize = new Vector2(144f, 144f),
                minSize = new Vector2(72f, 72f),
                maxSize = new Vector2(260f, 260f),
                lockSquareAspect = true
            },
            new HudElementMeta
            {
                id = IdStatusBarsPanel,
                displayName = "生存状态条",
                anchorMin = new Vector2(0.5f, 0f),
                anchorMax = new Vector2(0.5f, 0f),
                pivot = new Vector2(0.5f, 0f),
                defaultPos = new Vector2(0f, 22f),
                defaultSize = new Vector2(1080f, 58f),
                minSize = new Vector2(540f, 34f),
                maxSize = new Vector2(1560f, 110f),
                lockSquareAspect = false
            },
            new HudElementMeta
            {
                id = IdInteractionProgressBar,
                displayName = "进食/喝水条",
                anchorMin = new Vector2(0.5f, 1f),
                anchorMax = new Vector2(0.5f, 1f),
                pivot = new Vector2(0.5f, 1f),
                defaultPos = new Vector2(0f, -26f),
                defaultSize = new Vector2(540f, 54f),
                minSize = new Vector2(260f, 32f),
                maxSize = new Vector2(920f, 96f),
                lockSquareAspect = false
            },
            new HudElementMeta
            {
                id = IdDinoAttackButton,
                displayName = "攻击/交互键",
                anchorMin = new Vector2(1f, 0f),
                anchorMax = new Vector2(1f, 0f),
                pivot = new Vector2(0.5f, 0.5f),
                defaultPos = new Vector2(-165f, 225f),
                defaultSize = new Vector2(270f, 270f),
                minSize = new Vector2(110f, 110f),
                maxSize = new Vector2(400f, 400f),
                lockSquareAspect = true
            },
            new HudElementMeta
            {
                id = IdDinoJumpButton,
                displayName = "跳跃键",
                anchorMin = new Vector2(1f, 0f),
                anchorMax = new Vector2(1f, 0f),
                pivot = new Vector2(0.5f, 0.5f),
                defaultPos = new Vector2(-155f, 495f),
                defaultSize = new Vector2(210f, 210f),
                minSize = new Vector2(90f, 90f),
                maxSize = new Vector2(340f, 340f),
                lockSquareAspect = true
            },
            new HudElementMeta
            {
                id = IdDinoSneakButton,
                displayName = "潜行键",
                anchorMin = new Vector2(1f, 0f),
                anchorMax = new Vector2(1f, 0f),
                pivot = new Vector2(0.5f, 0.5f),
                defaultPos = new Vector2(-435f, 200f),
                defaultSize = new Vector2(210f, 210f),
                minSize = new Vector2(90f, 90f),
                maxSize = new Vector2(340f, 340f),
                lockSquareAspect = true
            },
            new HudElementMeta
            {
                id = IdDinoSkill1Button,
                displayName = "冲刺技能键",
                anchorMin = new Vector2(1f, 0f),
                anchorMax = new Vector2(1f, 0f),
                pivot = new Vector2(0.5f, 0.5f),
                defaultPos = new Vector2(-405f, 435f),
                defaultSize = new Vector2(198f, 198f),
                minSize = new Vector2(90f, 90f),
                maxSize = new Vector2(340f, 340f),
                lockSquareAspect = true
            },
            new HudElementMeta
            {
                id = IdDinoSkill2Button,
                displayName = "吼叫技能键",
                anchorMin = new Vector2(1f, 0f),
                anchorMax = new Vector2(1f, 0f),
                pivot = new Vector2(0.5f, 0.5f),
                defaultPos = new Vector2(-655f, 340f),
                defaultSize = new Vector2(198f, 198f),
                minSize = new Vector2(90f, 90f),
                maxSize = new Vector2(340f, 340f),
                lockSquareAspect = true
            }
        };

        private static readonly Dictionary<string, HudElementMeta> MetaById = new Dictionary<string, HudElementMeta>();

        static HudLayoutManager()
        {
            foreach (var m in EditableElements)
            {
                MetaById[m.id] = m;
            }
        }

        public static HudElementMeta GetMeta(string id)
        {
            return MetaById.TryGetValue(id, out var m) ? m : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneSync()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplySavedLayoutToCurrentScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplySavedLayoutToCurrentScene();
        }

        public static Dictionary<string, HudElementLayoutData> GetDefaultLayout()
        {
            var dict = new Dictionary<string, HudElementLayoutData>();
            foreach (var m in EditableElements)
            {
                dict[m.id] = new HudElementLayoutData
                {
                    id = m.id,
                    posX = m.defaultPos.x,
                    posY = m.defaultPos.y,
                    width = m.defaultSize.x,
                    height = m.defaultSize.y
                };
            }
            return dict;
        }

        public static Dictionary<string, HudElementLayoutData> LoadSavedLayout()
        {
            var result = GetDefaultLayout();
            string raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return result;
            }

            try
            {
                HudLayoutSavePayload payload = JsonUtility.FromJson<HudLayoutSavePayload>(raw);
                if (payload != null && payload.items != null)
                {
                    foreach (var item in payload.items)
                    {
                        if (item == null || !MetaById.TryGetValue(item.id, out var meta)) continue;
                        result[item.id] = new HudElementLayoutData
                        {
                            id = item.id,
                            posX = item.posX,
                            posY = item.posY,
                            width = Mathf.Clamp(item.width, meta.minSize.x, meta.maxSize.x),
                            height = Mathf.Clamp(item.height, meta.minSize.y, meta.maxSize.y)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HudLayoutManager] 读取布局配置失败，回退默认布局: {ex.Message}");
            }

            return result;
        }

        public static void SaveLayout(Dictionary<string, HudElementLayoutData> layout)
        {
            if (layout == null) return;
            var payload = new HudLayoutSavePayload();
            foreach (var meta in EditableElements)
            {
                if (layout.TryGetValue(meta.id, out var d) && d != null)
                {
                    payload.items.Add(new HudElementLayoutData
                    {
                        id = meta.id,
                        posX = d.posX,
                        posY = d.posY,
                        width = Mathf.Clamp(d.width, meta.minSize.x, meta.maxSize.x),
                        height = Mathf.Clamp(d.height, meta.minSize.y, meta.maxSize.y)
                    });
                }
            }

            string json = JsonUtility.ToJson(payload);
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
            ApplyLayoutToGameHud(layout);
        }

        public static void ApplySavedLayoutToCurrentScene()
        {
            GameObject hudCanvas = GameObject.Find("HUD_Canvas");
            if (hudCanvas == null) return;
            ApplyLayoutToGameHud(LoadSavedLayout());
        }

        public static void ApplyLayoutToGameHud(Dictionary<string, HudElementLayoutData> layout)
        {
            GameObject hudCanvas = GameObject.Find("HUD_Canvas");
            if (hudCanvas == null || layout == null) return;

            Transform root = hudCanvas.transform;
            foreach (var meta in EditableElements)
            {
                if (!layout.TryGetValue(meta.id, out var data) || data == null) continue;
                ApplySingleElementToGameHud(root, meta, data);
            }
        }

        public static void ApplySingleElementToGameHud(Transform hudRoot, HudElementMeta meta, HudElementLayoutData data)
        {
            if (hudRoot == null || meta == null || data == null) return;

            Vector2 pos = new Vector2(data.posX, data.posY);
            Vector2 size = new Vector2(
                Mathf.Clamp(data.width, meta.minSize.x, meta.maxSize.x),
                Mathf.Clamp(data.height, meta.minSize.y, meta.maxSize.y));

            if (meta.id == IdInteractionProgressBar)
            {
                InteractionProgressHUD progressHud = hudRoot.GetComponent<InteractionProgressHUD>();
                if (progressHud != null)
                {
                    progressHud.ApplyCustomLayout(pos, size);
                }
                return;
            }

            Transform tr = hudRoot.Find(meta.id);
            if (tr == null) return;

            RectTransform rt = tr.GetComponent<RectTransform>();
            if (rt == null) return;

            rt.anchorMin = meta.anchorMin;
            rt.anchorMax = meta.anchorMax;
            rt.pivot = meta.pivot;
            rt.localScale = Vector3.one; // 严格锁定 scale 不可变，仅调整 sizeDelta
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            if (meta.id == IdStatusBarsPanel)
            {
                var statusUI = tr.GetComponent<PlayerStatusUI>();
                if (statusUI != null) statusUI.ApplyCustomLayout(pos, size);
            }
            else if (meta.id == IdVirtualJoystick)
            {
                var joy = tr.GetComponent<VirtualJoystick>();
                if (joy != null) joy.ApplyCustomLayout(pos, size);
            }
            else if (meta.id == IdDialogueButton)
            {
                var dlg = tr.GetComponent<DialogueButtonHUD>();
                if (dlg != null)
                {
                    dlg.SetPosition(pos);
                    dlg.SetSize(size);
                }
            }
        }

        public static bool LayoutsMatch(Dictionary<string, HudElementLayoutData> a, Dictionary<string, HudElementLayoutData> b)
        {
            if (a == null || b == null) return false;
            foreach (var meta in EditableElements)
            {
                a.TryGetValue(meta.id, out var da);
                b.TryGetValue(meta.id, out var db);
                if (da == null || db == null || !da.ApproximatelyEquals(db))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
