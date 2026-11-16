using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelSaur.UI
{
    /// <summary>
    /// 全局 UI 面板管理器：
    /// 统一负责各功能窗口的打开、关闭、历史返回栈、背景防穿透遮罩及层级置顶管理。
    /// </summary>
    public class UIPanelManager : MonoBehaviour
    {
        public static UIPanelManager Instance { get; private set; }

        [System.Serializable]
        public class PanelEntry
        {
            public string panelId;
            public GameObject panelObject;
            public bool useModalDimmer = true;
        }

        [Header("已注册面板列表")]
        [SerializeField] private List<PanelEntry> registeredPanels = new List<PanelEntry>();

        [Header("模态防穿透遮罩 (自动创建或绑定)")]
        [SerializeField] private Image modalDimmerImage;

        private readonly Dictionary<string, PanelEntry> _panelMap = new Dictionary<string, PanelEntry>();
        private readonly List<string> _openStack = new List<string>();

        public int OpenModalCount => _openStack.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            RebuildLookup();
            EnsureDimmer();
            RefreshDimmerAndOrder();
        }

        private void Update()
        {
            bool backPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                backPressed = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                backPressed = true;
            }
#endif
            if (backPressed && _openStack.Count > 0)
            {
                CloseTopPanel();
            }
        }

        public void RegisterPanel(string panelId, GameObject panelObject, bool useModalDimmer = true)
        {
            if (string.IsNullOrEmpty(panelId) || panelObject == null) return;

            PanelEntry entry = registeredPanels.Find(p => p.panelId == panelId);
            if (entry == null)
            {
                entry = new PanelEntry
                {
                    panelId = panelId,
                    panelObject = panelObject,
                    useModalDimmer = useModalDimmer
                };
                registeredPanels.Add(entry);
            }
            else
            {
                entry.panelObject = panelObject;
                entry.useModalDimmer = useModalDimmer;
            }

            _panelMap[panelId] = entry;
        }

        public void OpenPanel(string panelId)
        {
            RebuildLookup();
            if (!_panelMap.TryGetValue(panelId, out PanelEntry entry) || entry.panelObject == null)
            {
                Debug.LogWarning($"[UIPanelManager] 未找到已注册面板: {panelId}");
                return;
            }

            _openStack.Remove(panelId);
            _openStack.Add(panelId);

            if (modalDimmerImage != null && entry.useModalDimmer)
            {
                modalDimmerImage.gameObject.SetActive(true);
                modalDimmerImage.transform.SetAsLastSibling();
            }

            entry.panelObject.SetActive(true);
            entry.panelObject.transform.SetAsLastSibling();

            UISoundManager.PlayOpen();
        }

        public void ClosePanel(string panelId)
        {
            if (!_panelMap.TryGetValue(panelId, out PanelEntry entry) || entry.panelObject == null) return;

            _openStack.Remove(panelId);
            entry.panelObject.SetActive(false);
            RefreshDimmerAndOrder();
            UISoundManager.PlayClose();
        }

        public void CloseTopPanel()
        {
            if (_openStack.Count == 0) return;
            string topId = _openStack[_openStack.Count - 1];
            ClosePanel(topId);
        }

        public void CloseAllPanels()
        {
            for (int i = 0; i < registeredPanels.Count; i++)
            {
                if (registeredPanels[i].panelObject != null)
                {
                    registeredPanels[i].panelObject.SetActive(false);
                }
            }
            _openStack.Clear();
            RefreshDimmerAndOrder();
        }

        private void RebuildLookup()
        {
            _panelMap.Clear();
            for (int i = 0; i < registeredPanels.Count; i++)
            {
                PanelEntry e = registeredPanels[i];
                if (!string.IsNullOrEmpty(e.panelId) && e.panelObject != null)
                {
                    _panelMap[e.panelId] = e;
                }
            }
        }

        private void EnsureDimmer()
        {
            if (modalDimmerImage != null) return;

            Transform existing = transform.Find("PanelModalDimmer");
            GameObject dimmerObj = existing != null
                ? existing.gameObject
                : new GameObject("PanelModalDimmer", typeof(RectTransform), typeof(Image), typeof(Button));

            if (existing == null)
            {
                dimmerObj.transform.SetParent(transform, false);
            }

            RectTransform rt = dimmerObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            modalDimmerImage = dimmerObj.GetComponent<Image>();
            modalDimmerImage.color = new Color(0.03f, 0.05f, 0.08f, 0.72f);
            modalDimmerImage.raycastTarget = true;

            Button btn = dimmerObj.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveListener(CloseTopPanel);
            btn.onClick.AddListener(CloseTopPanel);
            dimmerObj.SetActive(false);
        }

        private void RefreshDimmerAndOrder()
        {
            EnsureDimmer();
            if (modalDimmerImage == null) return;

            string topModalId = null;
            for (int i = _openStack.Count - 1; i >= 0; i--)
            {
                if (_panelMap.TryGetValue(_openStack[i], out PanelEntry e) && e.useModalDimmer && e.panelObject != null && e.panelObject.activeSelf)
                {
                    topModalId = e.panelId;
                    break;
                }
            }

            if (topModalId == null)
            {
                modalDimmerImage.gameObject.SetActive(false);
            }
            else
            {
                modalDimmerImage.gameObject.SetActive(true);
                modalDimmerImage.transform.SetAsLastSibling();
                _panelMap[topModalId].panelObject.transform.SetAsLastSibling();
            }
        }
    }
}
