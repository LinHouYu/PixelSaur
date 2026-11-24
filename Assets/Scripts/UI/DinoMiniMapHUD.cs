using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PixelSaur.Creatures;

namespace PixelSaur.UI
{
    /// <summary>
    /// 右上角正方形 2D 小地图（Game 场景专用）：
    /// 1. 自动扫描场景中的地形网格（陆地 sand_main、草地 grass_center）与海面（WaterSurface_Plane），
    ///    程序生成 2D 地形底图：海=淡青蓝色、陆地=淡黄色、草地=淡绿色，并用灰色线条绘制海岸线/草地分界线、参考网格与正方形外框；
    /// 2. 玩家自身固定在地图正中心，以旋转方向箭头实时指示朝向；
    /// 3. 实时追踪场景生物：
    ///    - Creature_GreenPrey：存活显示绿色圆点，死亡变为尸体时显示黄色圆点，被完全吃完消失时隐藏；
    ///    - Creature_RedHazard：显示红色圆点。
    /// </summary>
    [ExecuteAlways]
    public class DinoMiniMapHUD : MonoBehaviour
    {
        public static DinoMiniMapHUD Instance { get; private set; }

        [Header("小地图位置与尺寸（右上角正方形）")]
        [SerializeField] private Vector2 mapPosition = new Vector2(-24f, -20f);
        [SerializeField] private float mapSize = 220f;
        [SerializeField] private float borderThickness = 4f;

        [Header("2D 视野与烘焙范围（米）")]
        [Tooltip("小地图半视野半径（米）：设为 7.5 表示小地图展示玩家周围 15m × 15m")]
        [Range(4f, 30f)]
        [SerializeField] private float viewHalfSize = 7.5f;

        [Tooltip("地形底图烘焙半宽（米，超出区域自动延展海面颜色）")]
        [Range(12f, 60f)]
        [SerializeField] private float bakedWorldHalfExtent = 24f;

        [Tooltip("2D 地形底图分辨率")]
        [SerializeField] private int mapTextureResolution = 256;

        [Tooltip("灰色地图网格间距（米）")]
        [Range(1f, 10f)]
        [SerializeField] private float gridIntervalMeters = 2.5f;

        [Header("2D 地形配色（海/陆/草 + 灰色线条）")]
        [SerializeField] private Color seaColor = new Color(0.60f, 0.87f, 0.89f, 1f);        // 淡青蓝色
        [SerializeField] private Color landColor = new Color(0.95f, 0.90f, 0.68f, 1f);       // 淡黄色
        [SerializeField] private Color grassColor = new Color(0.67f, 0.88f, 0.60f, 1f);      // 淡绿色
        [SerializeField] private Color contourLineColor = new Color(0.45f, 0.49f, 0.54f, 1f);// 灰色分界线
        [SerializeField] private Color gridLineColor = new Color(0.48f, 0.52f, 0.57f, 0.28f);// 浅灰网格线
        [SerializeField] private Color borderColor = new Color(0.44f, 0.48f, 0.54f, 0.96f);  // 灰色外边框

        [Header("目标标记配色与尺寸")]
        [SerializeField] private Color playerArrowColor = Color.white;
        [SerializeField] private Color preyAliveColor = new Color(0.12f, 0.82f, 0.24f, 1f);   // 绿色圆点
        [SerializeField] private Color preyCorpseColor = new Color(0.98f, 0.86f, 0.14f, 1f); // 尸体黄色圆点
        [SerializeField] private Color hazardColor = new Color(0.94f, 0.18f, 0.18f, 1f);     // 红色圆点
        [SerializeField] private float playerArrowSize = 24f;
        [SerializeField] private float creatureDotSize = 14f;

        [Header("内部节点引用")]
        [SerializeField] private RawImage terrainMapRawImage;
        [SerializeField] private RectTransform markersContainer;
        [SerializeField] private RectTransform playerArrowRect;

        private Transform _playerTransform;
        private Texture2D _bakedMapTexture;
        private Vector2 _bakedCenterXZ = Vector2.zero;
        private float _entityScanTimer = 0f;

        private readonly List<GreenPreyCapsule> _greenPreys = new List<GreenPreyCapsule>();
        private readonly List<RedHazardCapsule> _redHazards = new List<RedHazardCapsule>();
        private readonly List<Image> _preyDotPool = new List<Image>();
        private readonly List<Image> _hazardDotPool = new List<Image>();

        private static Sprite _arrowSprite;
        private static Sprite _dotSprite;

        public Vector2 MapPosition => mapPosition;
        public float MapSize => mapSize;

        private void Awake()
        {
            if (Application.isPlaying) Instance = this;
            EnsureSprites();
            EnsureUIHierarchy();
        }

        private void OnEnable()
        {
            if (Application.isPlaying) Instance = this;
            EnsureSprites();
            EnsureUIHierarchy();
            BakeTerrainMapTexture();
            RefreshSceneEntities();
            UpdateMapVisuals();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_bakedMapTexture != null)
            {
                if (Application.isPlaying) Destroy(_bakedMapTexture);
                else DestroyImmediate(_bakedMapTexture);
                _bakedMapTexture = null;
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                UpdateMapVisuals();
                return;
            }

            _entityScanTimer -= Time.unscaledDeltaTime;
            if (_entityScanTimer <= 0f || _playerTransform == null)
            {
                _entityScanTimer = 0.5f;
                RefreshSceneEntities();
            }

            UpdateMapVisuals();
        }

        public void EnsureUIHierarchy()
        {
            EnsureSprites();

            RectTransform rootRT = GetComponent<RectTransform>();
            if (rootRT == null) rootRT = gameObject.AddComponent<RectTransform>();
            rootRT.anchorMin = new Vector2(1f, 1f);
            rootRT.anchorMax = new Vector2(1f, 1f);
            rootRT.pivot = new Vector2(1f, 1f);
            rootRT.anchoredPosition = mapPosition;
            rootRT.sizeDelta = new Vector2(mapSize, mapSize);
            rootRT.localScale = Vector3.one;

            // 外层灰色正方形边框底板
            Image frameImg = GetComponent<Image>();
            if (frameImg == null) frameImg = gameObject.AddComponent<Image>();
            frameImg.sprite = null;
            frameImg.color = borderColor;
            frameImg.raycastTarget = true;

            Outline outerOutline = GetComponent<Outline>();
            if (outerOutline == null) outerOutline = gameObject.AddComponent<Outline>();
            outerOutline.effectColor = new Color(0.14f, 0.16f, 0.20f, 0.90f);
            outerOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // 内层正方形裁剪视野容器（RectMask2D）
            Transform viewportTr = transform.Find("MapViewport");
            GameObject viewportGo = viewportTr != null ? viewportTr.gameObject : new GameObject("MapViewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(transform, false);
            RectTransform vpRT = viewportGo.GetComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = new Vector2(borderThickness, borderThickness);
            vpRT.offsetMax = new Vector2(-borderThickness, -borderThickness);
            vpRT.localScale = Vector3.one;

            if (viewportGo.GetComponent<RectMask2D>() == null)
            {
                viewportGo.AddComponent<RectMask2D>();
            }

            // 2D 地形纹理 RawImage
            Transform mapImgTr = viewportGo.transform.Find("TerrainMapImage");
            GameObject mapImgGo = mapImgTr != null ? mapImgTr.gameObject : new GameObject("TerrainMapImage", typeof(RectTransform), typeof(RawImage));
            mapImgGo.transform.SetParent(viewportGo.transform, false);
            RectTransform mapImgRT = mapImgGo.GetComponent<RectTransform>();
            mapImgRT.anchorMin = Vector2.zero;
            mapImgRT.anchorMax = Vector2.one;
            mapImgRT.offsetMin = Vector2.zero;
            mapImgRT.offsetMax = Vector2.zero;
            mapImgRT.localScale = Vector3.one;

            terrainMapRawImage = mapImgGo.GetComponent<RawImage>();
            terrainMapRawImage.color = Color.white;
            terrainMapRawImage.raycastTarget = false;
            if (_bakedMapTexture != null)
            {
                terrainMapRawImage.texture = _bakedMapTexture;
            }

            // 生物标记层
            Transform markersTr = viewportGo.transform.Find("MarkersLayer");
            GameObject markersGo = markersTr != null ? markersTr.gameObject : new GameObject("MarkersLayer", typeof(RectTransform));
            markersGo.transform.SetParent(viewportGo.transform, false);
            markersContainer = markersGo.GetComponent<RectTransform>();
            markersContainer.anchorMin = new Vector2(0.5f, 0.5f);
            markersContainer.anchorMax = new Vector2(0.5f, 0.5f);
            markersContainer.pivot = new Vector2(0.5f, 0.5f);
            markersContainer.anchoredPosition = Vector2.zero;
            markersContainer.sizeDelta = Vector2.zero;
            markersContainer.localScale = Vector3.one;

            // 玩家中心方向箭头（固定于正中心，按朝向旋转）
            Transform arrowTr = viewportGo.transform.Find("PlayerCenterArrow");
            GameObject arrowGo = arrowTr != null ? arrowTr.gameObject : new GameObject("PlayerCenterArrow", typeof(RectTransform), typeof(Image));
            arrowGo.transform.SetParent(viewportGo.transform, false);
            playerArrowRect = arrowGo.GetComponent<RectTransform>();
            playerArrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            playerArrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            playerArrowRect.pivot = new Vector2(0.5f, 0.5f);
            playerArrowRect.anchoredPosition = Vector2.zero;
            playerArrowRect.sizeDelta = new Vector2(playerArrowSize, playerArrowSize);
            playerArrowRect.localScale = Vector3.one;

            Image arrowImg = arrowGo.GetComponent<Image>();
            arrowImg.sprite = _arrowSprite;
            arrowImg.color = playerArrowColor;
            arrowImg.preserveAspect = true;
            arrowImg.raycastTarget = false;
            arrowGo.transform.SetAsLastSibling();

            // 右上角细微灰色 N 方向标（简洁不遮挡）
            Transform northTr = viewportGo.transform.Find("NorthTag");
            GameObject northGo = northTr != null ? northTr.gameObject : new GameObject("NorthTag", typeof(RectTransform), typeof(Text), typeof(Outline));
            northGo.transform.SetParent(viewportGo.transform, false);
            RectTransform nRT = northGo.GetComponent<RectTransform>();
            nRT.anchorMin = new Vector2(0.5f, 1f);
            nRT.anchorMax = new Vector2(0.5f, 1f);
            nRT.pivot = new Vector2(0.5f, 1f);
            nRT.anchoredPosition = new Vector2(0f, -3f);
            nRT.sizeDelta = new Vector2(24f, 18f);
            nRT.localScale = Vector3.one;
            Text nTxt = northGo.GetComponent<Text>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nTxt.font = font;
            nTxt.fontSize = 13;
            nTxt.fontStyle = FontStyle.Bold;
            nTxt.alignment = TextAnchor.MiddleCenter;
            nTxt.text = "N";
            nTxt.color = new Color(0.28f, 0.32f, 0.36f, 0.92f);
            nTxt.raycastTarget = false;
            Outline nOut = northGo.GetComponent<Outline>();
            nOut.effectColor = new Color(1f, 1f, 1f, 0.72f);
            nOut.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>
        /// 扫描场景中 Island_Terrain 的陆地（sand）与草地（grass）真实范围，烘焙 2D 地形底图 + 灰色轮廓线 + 浅灰参考网格
        /// </summary>
        public void BakeTerrainMapTexture()
        {
            int res = Mathf.Clamp(mapTextureResolution, 64, 512);
            if (_bakedMapTexture == null || _bakedMapTexture.width != res || _bakedMapTexture.height != res)
            {
                if (_bakedMapTexture != null)
                {
                    if (Application.isPlaying) Destroy(_bakedMapTexture);
                    else DestroyImmediate(_bakedMapTexture);
                }
                _bakedMapTexture = new Texture2D(res, res, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            List<Bounds> landBounds = new List<Bounds>();
            List<Bounds> grassBounds = new List<Bounds>();
            List<Collider> landColliders = new List<Collider>();
            List<Collider> grassColliders = new List<Collider>();

            GameObject island = GameObject.Find("Island_Terrain");
            if (island != null)
            {
                MeshRenderer[] mrs = island.GetComponentsInChildren<MeshRenderer>(true);
                foreach (MeshRenderer mr in mrs)
                {
                    if (mr == null) continue;
                    string n = mr.gameObject.name.ToLowerInvariant();
                    if (n.Contains("ocean") || n.Contains("seabed") || n.Contains("water")) continue;

                    Collider col = mr.GetComponent<MeshCollider>();
                    if (col == null) col = mr.GetComponent<Collider>();

                    if (n.Contains("grass"))
                    {
                        grassBounds.Add(mr.bounds);
                        if (col != null) grassColliders.Add(col);
                    }
                    else
                    {
                        landBounds.Add(mr.bounds);
                        if (col != null) landColliders.Add(col);
                    }
                }
            }

            // 兜底：若场景中暂未找到 Island_Terrain，采用默认岛屿范围
            if (landBounds.Count == 0)
            {
                landBounds.Add(new Bounds(Vector3.zero, new Vector3(9.08f, 0.2f, 9.08f)));
            }
            if (grassBounds.Count == 0)
            {
                grassBounds.Add(new Bounds(Vector3.zero, new Vector3(6.38f, 0.2f, 6.38f)));
            }

            _bakedCenterXZ = Vector2.zero;
            float half = Mathf.Max(8f, bakedWorldHalfExtent);
            float worldSpan = half * 2f;

            // 第一遍：分类每个像素为 0=海、1=陆地、2=草地
            byte[] gridClass = new byte[res * res];
            for (int py = 0; py < res; py++)
            {
                float wz = _bakedCenterXZ.y - half + ((py + 0.5f) / res) * worldSpan;
                for (int px = 0; px < res; px++)
                {
                    float wx = _bakedCenterXZ.x - half + ((px + 0.5f) / res) * worldSpan;
                    byte c = 0; // 默认海面

                    if (SampleInRegion(wx, wz, grassBounds, grassColliders))
                    {
                        c = 2; // 草地
                    }
                    else if (SampleInRegion(wx, wz, landBounds, landColliders))
                    {
                        c = 1; // 陆地
                    }

                    gridClass[py * res + px] = c;
                }
            }

            // 第二遍：根据类别着色，并在不同地形交界处绘制灰色线条 + 淡灰网格线
            Color[] pixels = new Color[res * res];
            float gridStep = Mathf.Max(1f, gridIntervalMeters);
            float pixelWorldSize = worldSpan / res;

            for (int py = 0; py < res; py++)
            {
                float wz = _bakedCenterXZ.y - half + ((py + 0.5f) / res) * worldSpan;
                bool nearEdgeY = py <= 2 || py >= res - 3;

                for (int px = 0; px < res; px++)
                {
                    float wx = _bakedCenterXZ.x - half + ((px + 0.5f) / res) * worldSpan;
                    bool nearEdgeX = px <= 2 || px >= res - 3;
                    byte c = gridClass[py * res + px];

                    Color col = c == 2 ? grassColor : (c == 1 ? landColor : seaColor);

                    // 淡灰参考网格线（外圈留 3px 纯海面保证 Clamp 延展无条纹）
                    if (!nearEdgeX && !nearEdgeY)
                    {
                        float modX = Mathf.Abs(wx - Mathf.Round(wx / gridStep) * gridStep);
                        float modZ = Mathf.Abs(wz - Mathf.Round(wz / gridStep) * gridStep);
                        if (modX < pixelWorldSize * 0.45f || modZ < pixelWorldSize * 0.45f)
                        {
                            col = Color.Lerp(col, new Color(gridLineColor.r, gridLineColor.g, gridLineColor.b, 1f), gridLineColor.a);
                        }
                    }

                    // 地形边界灰色轮廓线（海岸线 & 陆地/草地分界线）
                    if (px > 0 && px < res - 1 && py > 0 && py < res - 1)
                    {
                        bool isBoundary =
                            gridClass[py * res + (px - 1)] != c ||
                            gridClass[py * res + (px + 1)] != c ||
                            gridClass[(py - 1) * res + px] != c ||
                            gridClass[(py + 1) * res + px] != c;
                        if (isBoundary)
                        {
                            col = contourLineColor;
                        }
                    }

                    pixels[py * res + px] = col;
                }
            }

            _bakedMapTexture.SetPixels(pixels);
            _bakedMapTexture.Apply(false);

            if (terrainMapRawImage != null)
            {
                terrainMapRawImage.texture = _bakedMapTexture;
            }
        }

        private static bool SampleInRegion(float wx, float wz, List<Bounds> boundsList, List<Collider> colliders)
        {
            bool insideAnyBounds = false;
            for (int i = 0; i < boundsList.Count; i++)
            {
                Bounds b = boundsList[i];
                if (wx >= b.min.x && wx <= b.max.x && wz >= b.min.z && wz <= b.max.z)
                {
                    insideAnyBounds = true;
                    break;
                }
            }
            if (!insideAnyBounds) return false;

            // 若存在精确 Collider，从正上方垂直射线判定不规则海岸线轮廓
            if (colliders != null && colliders.Count > 0)
            {
                Ray ray = new Ray(new Vector3(wx, 12f, wz), Vector3.down);
                for (int i = 0; i < colliders.Count; i++)
                {
                    Collider col = colliders[i];
                    if (col != null && col.enabled && col.Raycast(ray, out _, 25f))
                    {
                        return true;
                    }
                }
                return false;
            }

            return true;
        }

        private void RefreshSceneEntities()
        {
            if (_playerTransform == null)
            {
                GameObject playerGo = GameObject.Find("Player_Capsule");
                if (playerGo != null) _playerTransform = playerGo.transform;
            }

            _greenPreys.Clear();
            GreenPreyCapsule[] preys = FindObjectsByType<GreenPreyCapsule>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < preys.Length; i++)
            {
                if (preys[i] != null) _greenPreys.Add(preys[i]);
            }

            _redHazards.Clear();
            RedHazardCapsule[] hazards = FindObjectsByType<RedHazardCapsule>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < hazards.Length; i++)
            {
                if (hazards[i] != null) _redHazards.Add(hazards[i]);
            }
        }

        public void UpdateMapVisuals()
        {
            if (terrainMapRawImage == null || markersContainer == null || playerArrowRect == null) return;
            if (_bakedMapTexture == null) BakeTerrainMapTexture();
            if (_playerTransform == null)
            {
                GameObject p = GameObject.Find("Player_Capsule");
                if (p != null) _playerTransform = p.transform;
            }

            Vector2 playerXZ = _playerTransform != null
                ? new Vector2(_playerTransform.position.x, _playerTransform.position.z)
                : Vector2.zero;
            float playerYaw = _playerTransform != null ? _playerTransform.eulerAngles.y : 0f;

            // 1. 以玩家为正中心平移 2D 地形底图 UV
            float bakedSpan = Mathf.Max(16f, bakedWorldHalfExtent * 2f);
            float viewSpan = Mathf.Max(4f, viewHalfSize * 2f);
            float uvSize = Mathf.Clamp(viewSpan / bakedSpan, 0.05f, 1.0f);
            float centerU = (playerXZ.x - (_bakedCenterXZ.x - bakedWorldHalfExtent)) / bakedSpan;
            float centerV = (playerXZ.y - (_bakedCenterXZ.y - bakedWorldHalfExtent)) / bakedSpan;
            terrainMapRawImage.uvRect = new Rect(centerU - uvSize * 0.5f, centerV - uvSize * 0.5f, uvSize, uvSize);

            // 2. 玩家中心箭头朝向（Unity 顺时针 Yaw 对应 UI 逆时针负 Z 旋转）
            playerArrowRect.anchoredPosition = Vector2.zero;
            playerArrowRect.localEulerAngles = new Vector3(0f, 0f, -playerYaw);

            // 3. 更新生物圆点标记（绿猎物存活=绿点、死亡尸体=黄点；红危险体=红点）
            float innerPixels = Mathf.Max(32f, mapSize - borderThickness * 2f);
            float pixelsPerMeter = (innerPixels * 0.5f) / Mathf.Max(1f, viewHalfSize);

            EnsureDotPool(_preyDotPool, _greenPreys.Count, "PreyDot_");
            for (int i = 0; i < _preyDotPool.Count; i++)
            {
                Image dot = _preyDotPool[i];
                if (i >= _greenPreys.Count || _greenPreys[i] == null)
                {
                    dot.gameObject.SetActive(false);
                    continue;
                }

                GreenPreyCapsule prey = _greenPreys[i];
                bool isCompletelyGone = !prey.gameObject.activeInHierarchy || (prey.IsDead && prey.RemainingFeedCount <= 0);
                if (isCompletelyGone)
                {
                    dot.gameObject.SetActive(false);
                    continue;
                }

                Vector2 rel = new Vector2(prey.transform.position.x - playerXZ.x, prey.transform.position.z - playerXZ.y);
                if (dot.sprite == null) dot.sprite = _dotSprite;
                dot.rectTransform.anchoredPosition = rel * pixelsPerMeter;
                dot.rectTransform.sizeDelta = new Vector2(creatureDotSize, creatureDotSize);
                dot.color = prey.IsDead ? preyCorpseColor : preyAliveColor;
                dot.gameObject.SetActive(true);
            }

            EnsureDotPool(_hazardDotPool, _redHazards.Count, "HazardDot_");
            for (int i = 0; i < _hazardDotPool.Count; i++)
            {
                Image dot = _hazardDotPool[i];
                if (i >= _redHazards.Count || _redHazards[i] == null)
                {
                    dot.gameObject.SetActive(false);
                    continue;
                }

                RedHazardCapsule hazard = _redHazards[i];
                if (!hazard.gameObject.activeInHierarchy || hazard.IsDead)
                {
                    dot.gameObject.SetActive(false);
                    continue;
                }

                Vector2 rel = new Vector2(hazard.transform.position.x - playerXZ.x, hazard.transform.position.z - playerXZ.y);
                if (dot.sprite == null) dot.sprite = _dotSprite;
                dot.rectTransform.anchoredPosition = rel * pixelsPerMeter;
                dot.rectTransform.sizeDelta = new Vector2(creatureDotSize, creatureDotSize);
                dot.color = hazardColor;
                dot.gameObject.SetActive(true);
            }
        }

        private void EnsureDotPool(List<Image> pool, int needed, string prefix)
        {
            EnsureSprites();
            // 先回收场景中已有的同名子节点，避免 Editor 重复创建，并补赋运行时圆形贴图
            if (pool.Count == 0 && markersContainer != null)
            {
                for (int c = 0; c < markersContainer.childCount; c++)
                {
                    Transform ch = markersContainer.GetChild(c);
                    if (ch.name.StartsWith(prefix))
                    {
                        Image existingImg = ch.GetComponent<Image>();
                        if (existingImg != null)
                        {
                            existingImg.sprite = _dotSprite;
                            existingImg.preserveAspect = true;
                            existingImg.raycastTarget = false;
                            pool.Add(existingImg);
                        }
                    }
                }
            }

            while (pool.Count < needed && markersContainer != null)
            {
                int idx = pool.Count;
                GameObject go = new GameObject(prefix + idx, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(markersContainer, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(creatureDotSize, creatureDotSize);
                rt.localScale = Vector3.one;

                Image img = go.GetComponent<Image>();
                img.sprite = _dotSprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                pool.Add(img);
            }
        }

        private static void EnsureSprites()
        {
            if (_dotSprite == null) _dotSprite = CreateOutlinedDotSprite(64);
            if (_arrowSprite == null) _arrowSprite = CreatePlayerArrowSprite(64);
        }

        /// <summary>
        /// 带深灰描边的高对比度圆形标记贴图（保证绿色圆点在淡绿草地上依然一眼清晰）
        /// </summary>
        private static Sprite CreateOutlinedDotSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float half = size * 0.5f;
            float outerR = half - 2f;
            float innerR = outerR - 5.5f;
            Color[] px = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) - half;
                    float dy = (y + 0.5f) - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= innerR)
                    {
                        px[y * size + x] = Color.white;
                    }
                    else if (dist <= outerR)
                    {
                        float aa = Mathf.Clamp01(outerR - dist + 0.5f);
                        px[y * size + x] = new Color(0.14f, 0.16f, 0.20f, aa);
                    }
                    else
                    {
                        px[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        /// <summary>
        /// 经典导航尖箭头贴图（朝上为 0° 北向，白色本体 + 深色描边，清晰醒目）
        /// </summary>
        private static Sprite CreatePlayerArrowSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            // 归一化 [-1, 1] 坐标系中的燕尾箭头四顶点：顶(0, 0.85)、右翼(0.68, -0.72)、尾凹(0, -0.34)、左翼(-0.68, -0.72)
            Vector2 tip = new Vector2(0f, 0.84f);
            Vector2 right = new Vector2(0.66f, -0.72f);
            Vector2 notch = new Vector2(0f, -0.32f);
            Vector2 left = new Vector2(-0.66f, -0.72f);

            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = ((y + 0.5f) / size) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    Vector2 p = new Vector2(nx, ny);

                    bool insideInner = PointInTriangle(p, tip * 0.82f, right * 0.80f, notch * 0.82f)
                                    || PointInTriangle(p, tip * 0.82f, notch * 0.82f, left * 0.80f);
                    bool insideOuter = PointInTriangle(p, tip, right, notch)
                                    || PointInTriangle(p, tip, notch, left);

                    if (insideInner)
                    {
                        px[y * size + x] = Color.white;
                    }
                    else if (insideOuter)
                    {
                        px[y * size + x] = new Color(0.12f, 0.14f, 0.18f, 0.96f);
                    }
                    else
                    {
                        px[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
            bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }
    }
}
