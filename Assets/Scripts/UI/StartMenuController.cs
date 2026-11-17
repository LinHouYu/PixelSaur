using UnityEngine;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    /// <summary>
    /// 开始场景大厅主控制器：
    /// 管理顶部状态栏占位（头像/货币/齿轮设置）、【开始游戏】加载跳转至 3D 岛屿场景（Game），
    /// 并通过 UIPanelManager 控制任务系统、功能弹窗与设置面板的层级遮挡与开关。
    /// </summary>
    public class StartMenuController : MonoBehaviour
    {
        public const string GameSceneName = "Game";

        [Header("交互按钮绑定")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button gearSettingsButton;
        [SerializeField] private Button profileButton;
        [SerializeField] private Button goldAddButton;
        [SerializeField] private Button amberAddButton;
        [SerializeField] private Button codexButton;
        [SerializeField] private Button noticeButton;
        [SerializeField] private Button questButton;
        [SerializeField] private Button inventoryButton;

        [Header("货币文本引用")]
        [SerializeField] private Text goldAmountText;
        [SerializeField] private Text amberAmountText;

        [Header("任务、公告、背包与占位弹窗面板")]
        [SerializeField] private QuestPanelHUD questPanel;
        [SerializeField] private NoticePanelHUD noticePanel;
        [SerializeField] private InventoryPanelHUD inventoryPanel;
        [SerializeField] private GameObject infoModalPanel;
        [SerializeField] private Text infoModalTitle;
        [SerializeField] private Text infoModalBody;
        [SerializeField] private Button infoModalCloseButton;

        [Header("同步设置菜单引用")]
        [SerializeField] private SettingsMenuPanelHUD settingsMenuPanel;

        private void Awake()
        {
            SubSettingsPageHUD.ApplyAllSavedSettingsAtStartup();
            UISoundManager.ApplySavedAudioVolumes();
            QuestManager.EnsureInitialized();
            InventoryManager.EnsureInitialized();
            InventoryPanelHUD.SyncLobbyDinoPreview();
        }

        private void OnEnable()
        {
            BindListeners();
            RegisterManagedPanels();
            QuestManager.OnDataChanged += RefreshCurrencyLabels;
            RefreshCurrencyLabels();
        }

        private void OnDisable()
        {
            UnbindListeners();
            QuestManager.OnDataChanged -= RefreshCurrencyLabels;
        }

        private void RegisterManagedPanels()
        {
            if (UIPanelManager.Instance == null) return;
            if (infoModalPanel != null)
            {
                UIPanelManager.Instance.RegisterPanel("InfoModal", infoModalPanel, useModalDimmer: true);
            }
            if (questPanel != null)
            {
                UIPanelManager.Instance.RegisterPanel("QuestPanel", questPanel.gameObject, useModalDimmer: true);
            }
            if (noticePanel != null)
            {
                UIPanelManager.Instance.RegisterPanel("NoticePanel", noticePanel.gameObject, useModalDimmer: true);
            }
            if (inventoryPanel != null)
            {
                UIPanelManager.Instance.RegisterPanel("InventoryPanel", inventoryPanel.gameObject, useModalDimmer: true);
            }
        }

        private void RefreshCurrencyLabels()
        {
            if (goldAmountText != null) goldAmountText.text = QuestManager.Gold.ToString("N0");
            if (amberAmountText != null) amberAmountText.text = QuestManager.Amber.ToString("N0");
        }

        private void BindListeners()
        {
            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveListener(HandleStartGame);
                startGameButton.onClick.AddListener(HandleStartGame);
            }
            if (gearSettingsButton != null)
            {
                gearSettingsButton.onClick.RemoveListener(HandleOpenSettings);
                gearSettingsButton.onClick.AddListener(HandleOpenSettings);
            }
            if (profileButton != null)
            {
                profileButton.onClick.RemoveListener(HandleOpenProfile);
                profileButton.onClick.AddListener(HandleOpenProfile);
            }
            if (goldAddButton != null)
            {
                goldAddButton.onClick.RemoveListener(HandleOpenCurrencyHint);
                goldAddButton.onClick.AddListener(HandleOpenCurrencyHint);
            }
            if (amberAddButton != null)
            {
                amberAddButton.onClick.RemoveListener(HandleOpenCurrencyHint);
                amberAddButton.onClick.AddListener(HandleOpenCurrencyHint);
            }
            if (codexButton != null)
            {
                codexButton.onClick.RemoveListener(HandleOpenCodex);
                codexButton.onClick.AddListener(HandleOpenCodex);
            }
            if (noticeButton != null)
            {
                noticeButton.onClick.RemoveListener(HandleOpenNotice);
                noticeButton.onClick.AddListener(HandleOpenNotice);
            }
            if (questButton != null)
            {
                questButton.onClick.RemoveListener(HandleOpenQuests);
                questButton.onClick.AddListener(HandleOpenQuests);
            }
            if (inventoryButton != null)
            {
                inventoryButton.onClick.RemoveListener(HandleOpenInventory);
                inventoryButton.onClick.AddListener(HandleOpenInventory);
            }
            if (infoModalCloseButton != null)
            {
                infoModalCloseButton.onClick.RemoveListener(HandleCloseInfoModal);
                infoModalCloseButton.onClick.AddListener(HandleCloseInfoModal);
            }
        }

        private void UnbindListeners()
        {
            if (startGameButton != null) startGameButton.onClick.RemoveListener(HandleStartGame);
            if (gearSettingsButton != null) gearSettingsButton.onClick.RemoveListener(HandleOpenSettings);
            if (profileButton != null) profileButton.onClick.RemoveListener(HandleOpenProfile);
            if (goldAddButton != null) goldAddButton.onClick.RemoveListener(HandleOpenCurrencyHint);
            if (amberAddButton != null) amberAddButton.onClick.RemoveListener(HandleOpenCurrencyHint);
            if (codexButton != null) codexButton.onClick.RemoveListener(HandleOpenCodex);
            if (noticeButton != null) noticeButton.onClick.RemoveListener(HandleOpenNotice);
            if (questButton != null) questButton.onClick.RemoveListener(HandleOpenQuests);
            if (inventoryButton != null) inventoryButton.onClick.RemoveListener(HandleOpenInventory);
            if (infoModalCloseButton != null) infoModalCloseButton.onClick.RemoveListener(HandleCloseInfoModal);
        }

        private void HandleStartGame()
        {
            UISoundManager.PlaySave();
            UIPanelManager.Instance?.CloseAllPanels();
            SceneTransitionLoader.LoadSceneWithTransition(GameSceneName, "正在加载 3D 恐龙岛屿场景...");
        }

        private void HandleOpenSettings()
        {
            if (settingsMenuPanel == null)
            {
                var found = Resources.FindObjectsOfTypeAll<SettingsMenuPanelHUD>();
                if (found.Length > 0) settingsMenuPanel = found[0];
            }
            if (settingsMenuPanel != null)
            {
                settingsMenuPanel.ToggleMenu();
            }
        }

        private void HandleOpenQuests()
        {
            if (questPanel != null)
            {
                questPanel.OpenDefault();
            }
        }

        private void HandleOpenInventory()
        {
            if (inventoryPanel != null)
            {
                inventoryPanel.OpenDefault();
            }
        }

        private void HandleOpenProfile()
        {
            ShowInfoModal("驯龙者档案 (占位)", "当前角色：霸王龙 (T-Rex)\n生存等级：Lv.1\n说明：此处为顶部头像状态栏占位弹窗，可接入账号与恐龙成长数据。");
        }

        private void HandleOpenCurrencyHint()
        {
            ShowInfoModal("资源货币栏 (占位)", $"龙肉金币：{QuestManager.Gold:N0}\n远古琥珀：{QuestManager.Amber:N0}\n说明：完成每日/主线任务领取奖励后将实时累加至此货币栏！");
        }

        private void HandleOpenCodex()
        {
            ShowInfoModal("恐龙图鉴 (占位)", "• 霸王龙 (T-Rex)：强力陆地掠食者，拥有撕咬、潜行、狂暴冲刺与震慑吼叫技能。\n• 绿色猎物 / 红色危险生物：岛屿生态测试体。");
        }

        private void HandleOpenNotice()
        {
            if (noticePanel != null)
            {
                noticePanel.OpenPanel();
            }
            else
            {
                ShowInfoModal("岛屿生存公告", "• 在此处齿轮设置修改的画质、帧率、草地密度、音量与镜头灵敏度均会自动保存，进入 3D 岛屿后立即同步生效！");
            }
        }

        private void ShowInfoModal(string title, string body)
        {
            if (infoModalTitle != null) infoModalTitle.text = title;
            if (infoModalBody != null) infoModalBody.text = body;
            UIPanelManager.Instance?.OpenPanel("InfoModal");
        }

        private void HandleCloseInfoModal()
        {
            UIPanelManager.Instance?.ClosePanel("InfoModal");
        }
    }
}
