using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PixelSaur.Player
{
    /// <summary>
    /// 恐龙原地进化高燃特效控制器（零外部美术依赖，100% 程序生成并兼容 URP 移动端）：
    /// 1. 模型纯白高发光过渡（平滑变白发光并在演出结束后恢复原材质）；
    /// 2. 冲天光柱（《我的世界》信标风格双层旋转光柱，按目标阶段白/绿/蓝/紫着色）；
    /// 3. 神秘符号环（程序绘制 8 种发光远古像素符文组成旋转浮空光环 + 脚底法阵光环）；
    /// 4. 底部向上旋转喷射粒子（ParticleSystem 从恐龙脚底向四周与高空旋转喷涌发光粒子）。
    /// </summary>
    public class DinoEvolutionVfx : MonoBehaviour
    {
        private struct SavedMaterialState
        {
            public Renderer renderer;
            public Material[] originalMaterials;
            public Material[] instanceMaterials;
            public Color[] originalBaseColors;
            public Color[] originalEmissionColors;
            public bool[] hadEmissionKeyword;
        }

        private readonly List<SavedMaterialState> _savedMaterials = new List<SavedMaterialState>();
        private GameObject _vfxRoot;
        private Transform _innerBeamTr;
        private Transform _outerBeamTr;
        private Material _innerBeamMat;
        private Material _outerBeamMat;

        private Transform _runeRingRoot;
        private readonly List<Transform> _runeQuads = new List<Transform>();
        private Material _runeMat;

        private Transform _groundHaloTr;
        private Material _groundHaloMat;

        private ParticleSystem _fountainPS;
        private Material _particleMat;

        private Color _themeColor = Color.white;
        private bool _isPlaying;
        private bool _burstTriggered;

        private static Texture2D _cachedRuneAtlas;
        private static Texture2D _cachedHaloTexture;
        private static Texture2D _cachedSparkTexture;
        private static Mesh _cachedOpenCylinderMesh;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        public bool IsPlaying => _isPlaying;

        public void BeginSpectacle(Transform dinosaurRoot, Color targetThemeColor, float initialScaleMult)
        {
            CleanupVfx();
            RestoreOriginalMaterials();

            _themeColor = targetThemeColor;
            _isPlaying = true;
            _burstTriggered = false;

            CaptureDinosaurMaterials(dinosaurRoot);
            CreateVfxHierarchy(dinosaurRoot, initialScaleMult);
        }

        /// <summary>
        /// 按归一化进度 [0, 1] 更新 3 秒演出全部视觉表现（光柱、符文环、喷射粒子、模型白光）
        /// </summary>
        public void UpdateSpectacle(Transform dinosaurRoot, float normalizedTime, float currentScaleMult, float deltaTime)
        {
            if (!_isPlaying || dinosaurRoot == null) return;

            float t = Mathf.Clamp01(normalizedTime);
            Vector3 feetPos = GetFeetWorldPosition(dinosaurRoot);

            if (_vfxRoot != null)
            {
                _vfxRoot.transform.position = feetPos;
            }

            // 1. 模型纯白高发光曲线：0~0.42 升至最亮，0.42~0.72 保持纯白高光（同步体型放大），0.72~1.0 平滑还原原材质
            float whiteGlow;
            if (t < 0.42f)
            {
                whiteGlow = Mathf.SmoothStep(0f, 1f, t / 0.42f);
            }
            else if (t < 0.72f)
            {
                whiteGlow = 1f;
            }
            else
            {
                whiteGlow = 1f - Mathf.SmoothStep(0f, 1f, (t - 0.72f) / 0.28f);
            }
            ApplyWhiteGlowToDinosaur(whiteGlow);

            // 2. 冲天信标光柱动画（随目标阶段主题色着色，在峰值爆发膨胀，结尾收束淡出）
            float beamAlpha = t < 0.15f
                ? Mathf.SmoothStep(0f, 1f, t / 0.15f)
                : (t > 0.75f ? 1f - Mathf.SmoothStep(0f, 1f, (t - 0.75f) / 0.25f) : 1f);
            float burstPulse = Mathf.Exp(-Mathf.Pow((t - 0.50f) / 0.14f, 2f));
            float beamRadiusScale = Mathf.Lerp(0.85f, 1.45f, burstPulse) * Mathf.Lerp(0.8f, 1.55f, (currentScaleMult - 1f) / 2.5f);

            if (_innerBeamTr != null)
            {
                _innerBeamTr.Rotate(Vector3.up, 110f * deltaTime, Space.Self);
                float rIn = 0.36f * beamRadiusScale * Mathf.Max(0.15f, beamAlpha);
                _innerBeamTr.localScale = new Vector3(rIn, 68f, rIn);
            }
            if (_outerBeamTr != null)
            {
                _outerBeamTr.Rotate(Vector3.up, -70f * deltaTime, Space.Self);
                float rOut = 0.82f * beamRadiusScale * Mathf.Max(0.15f, beamAlpha);
                _outerBeamTr.localScale = new Vector3(rOut, 72f, rOut);
            }
            if (_innerBeamMat != null)
            {
                Color coreCol = Color.Lerp(_themeColor, Color.white, 0.72f);
                coreCol.a = 0.85f * beamAlpha;
                SetMatColor(_innerBeamMat, coreCol);
            }
            if (_outerBeamMat != null)
            {
                Color auraCol = _themeColor;
                auraCol.a = (0.48f + 0.28f * burstPulse) * beamAlpha;
                SetMatColor(_outerBeamMat, auraCol);
            }

            // 3. 神秘符号环旋转与呼吸浮沉
            if (_runeRingRoot != null)
            {
                float spinSpeed = Mathf.Lerp(95f, 340f, burstPulse);
                _runeRingRoot.Rotate(Vector3.up, spinSpeed * deltaTime, Space.Self);
                float ringRadius = (1.25f + 0.48f * (currentScaleMult - 1f)) * Mathf.Lerp(0.85f, 1.25f, t);
                float ringY = 0.75f + 0.35f * currentScaleMult + Mathf.Sin(t * Mathf.PI * 3f) * 0.18f;
                _runeRingRoot.localPosition = new Vector3(0f, ringY, 0f);

                for (int i = 0; i < _runeQuads.Count; i++)
                {
                    Transform q = _runeQuads[i];
                    if (q == null) continue;
                    float ang = (i / (float)_runeQuads.Count) * Mathf.PI * 2f;
                    float bob = Mathf.Sin(t * Mathf.PI * 6f + i * 0.7f) * 0.16f;
                    q.localPosition = new Vector3(Mathf.Cos(ang) * ringRadius, bob, Mathf.Sin(ang) * ringRadius);
                    q.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)), Vector3.up);
                    float qScale = (0.48f + 0.16f * currentScaleMult) * (0.85f + 0.3f * burstPulse);
                    q.localScale = new Vector3(qScale, qScale, qScale);
                }
            }
            if (_runeMat != null)
            {
                Color rCol = Color.Lerp(_themeColor, Color.white, 0.25f + 0.45f * burstPulse);
                rCol.a = 0.96f * beamAlpha;
                SetMatColor(_runeMat, rCol);
            }

            // 4. 脚底法阵光环
            if (_groundHaloTr != null)
            {
                _groundHaloTr.Rotate(Vector3.up, -140f * deltaTime, Space.Self);
                float haloSize = (2.8f + 1.15f * (currentScaleMult - 1f)) * (0.9f + 0.35f * burstPulse);
                _groundHaloTr.localScale = new Vector3(haloSize, haloSize, 1f);
            }
            if (_groundHaloMat != null)
            {
                Color hCol = _themeColor;
                hCol.a = 0.85f * beamAlpha;
                SetMatColor(_groundHaloMat, hCol);
            }

            // 5. 峰值爆发瞬间触发大量上升发光粒子喷涌
            if (!_burstTriggered && t >= 0.44f && _fountainPS != null)
            {
                _burstTriggered = true;
                _fountainPS.Emit(65);
            }
            if (t >= 0.78f && _fountainPS != null)
            {
                var em = _fountainPS.emission;
                em.enabled = false;
            }
        }

        public void EndSpectacle()
        {
            _isPlaying = false;
            RestoreOriginalMaterials();
            CleanupVfx();
        }

        private void OnDisable()
        {
            EndSpectacle();
        }

        private void OnDestroy()
        {
            EndSpectacle();
        }

        private static Vector3 GetFeetWorldPosition(Transform dinosaurRoot)
        {
            // Player_Capsule 的 CharacterController 底部位于本地 Y = -1.0
            float bottomOffset = dinosaurRoot.localScale.y * 1.0f;
            return dinosaurRoot.position - Vector3.up * bottomOffset + Vector3.up * 0.03f;
        }

        private void CaptureDinosaurMaterials(Transform dinosaurRoot)
        {
            _savedMaterials.Clear();
            if (dinosaurRoot == null) return;

            Renderer[] renderers = dinosaurRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled) continue;
                if (r.gameObject.name.Contains("Indicator")) continue;

                Material[] origShared = r.sharedMaterials;
                if (origShared == null || origShared.Length == 0) continue;

                Material[] instances = new Material[origShared.Length];
                Color[] origBases = new Color[origShared.Length];
                Color[] origEmissions = new Color[origShared.Length];
                bool[] hadEmis = new bool[origShared.Length];

                for (int i = 0; i < origShared.Length; i++)
                {
                    if (origShared[i] == null) continue;
                    Material inst = new Material(origShared[i]);
                    instances[i] = inst;
                    origBases[i] = inst.HasProperty(BaseColorId) ? inst.GetColor(BaseColorId) :
                                   (inst.HasProperty(ColorId) ? inst.GetColor(ColorId) : Color.white);
                    origEmissions[i] = inst.HasProperty(EmissionColorId) ? inst.GetColor(EmissionColorId) : Color.black;
                    hadEmis[i] = inst.IsKeywordEnabled("_EMISSION");
                }

                r.materials = instances;
                _savedMaterials.Add(new SavedMaterialState
                {
                    renderer = r,
                    originalMaterials = origShared,
                    instanceMaterials = instances,
                    originalBaseColors = origBases,
                    originalEmissionColors = origEmissions,
                    hadEmissionKeyword = hadEmis
                });
            }
        }

        private void ApplyWhiteGlowToDinosaur(float glow01)
        {
            float g = Mathf.Clamp01(glow01);
            Color hdrWhite = new Color(2.6f, 2.6f, 2.6f, 1f);

            for (int s = 0; s < _savedMaterials.Count; s++)
            {
                var state = _savedMaterials[s];
                if (state.instanceMaterials == null) continue;

                for (int i = 0; i < state.instanceMaterials.Length; i++)
                {
                    Material mat = state.instanceMaterials[i];
                    if (mat == null) continue;

                    Color baseCol = Color.Lerp(state.originalBaseColors[i], Color.white, g);
                    if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, baseCol);
                    if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, baseCol);

                    if (mat.HasProperty(EmissionColorId))
                    {
                        if (g > 0.01f) mat.EnableKeyword("_EMISSION");
                        else if (!state.hadEmissionKeyword[i]) mat.DisableKeyword("_EMISSION");
                        mat.SetColor(EmissionColorId, Color.Lerp(state.originalEmissionColors[i], hdrWhite, g));
                    }
                }
            }
        }

        private void RestoreOriginalMaterials()
        {
            for (int s = 0; s < _savedMaterials.Count; s++)
            {
                var state = _savedMaterials[s];
                if (state.renderer != null && state.originalMaterials != null)
                {
                    state.renderer.sharedMaterials = state.originalMaterials;
                }
                if (state.instanceMaterials != null)
                {
                    for (int i = 0; i < state.instanceMaterials.Length; i++)
                    {
                        if (state.instanceMaterials[i] != null) Destroy(state.instanceMaterials[i]);
                    }
                }
            }
            _savedMaterials.Clear();

            // 同步恢复当前已装备皮肤贴图（若存在）
            DinoAnimatorController.Instance?.ApplyEquippedVisualTint();
        }

        private void CreateVfxHierarchy(Transform dinosaurRoot, float initialScaleMult)
        {
            EnsureSharedAssets();

            _vfxRoot = new GameObject("DinoEvolutionVfxRoot");
            _vfxRoot.transform.position = GetFeetWorldPosition(dinosaurRoot);

            // 1. 冲天光柱（双层《我的世界》信标垂直光柱）
            _innerBeamMat = CreateAdditiveEffectMaterial(null, Color.white);
            _outerBeamMat = CreateAdditiveEffectMaterial(null, _themeColor);

            GameObject innerGo = CreateMeshObject("BeaconInnerBeam", _vfxRoot.transform, _cachedOpenCylinderMesh, _innerBeamMat);
            _innerBeamTr = innerGo.transform;
            _innerBeamTr.localPosition = Vector3.zero;
            _innerBeamTr.localScale = new Vector3(0.35f, 68f, 0.35f);

            GameObject outerGo = CreateMeshObject("BeaconOuterBeam", _vfxRoot.transform, _cachedOpenCylinderMesh, _outerBeamMat);
            _outerBeamTr = outerGo.transform;
            _outerBeamTr.localPosition = Vector3.zero;
            _outerBeamTr.localScale = new Vector3(0.80f, 72f, 0.80f);

            // 2. 神秘符号环（12 枚发光远古符文围绕旋转）
            _runeMat = CreateAdditiveEffectMaterial(_cachedRuneAtlas, _themeColor);
            GameObject ringGo = new GameObject("MysticRuneRing");
            ringGo.transform.SetParent(_vfxRoot.transform, false);
            _runeRingRoot = ringGo.transform;
            _runeRingRoot.localPosition = new Vector3(0f, 1.0f, 0f);

            _runeQuads.Clear();
            const int runeCount = 12;
            for (int i = 0; i < runeCount; i++)
            {
                Mesh glyphMesh = CreateRuneQuadMesh(i % 8);
                GameObject qGo = CreateMeshObject("Rune_" + i, _runeRingRoot, glyphMesh, _runeMat);
                _runeQuads.Add(qGo.transform);
            }

            // 3. 脚底发光魔法环
            _groundHaloMat = CreateAdditiveEffectMaterial(_cachedHaloTexture, _themeColor);
            GameObject haloGo = CreateMeshObject("GroundMagicHalo", _vfxRoot.transform, CreateUnitQuadMesh(), _groundHaloMat);
            _groundHaloTr = haloGo.transform;
            _groundHaloTr.localPosition = new Vector3(0f, 0.05f, 0f);
            _groundHaloTr.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _groundHaloTr.localScale = new Vector3(2.8f * initialScaleMult, 2.8f * initialScaleMult, 1f);

            // 4. 底部向上旋转喷射粒子系统
            CreateUpwardParticleFountain(_vfxRoot.transform, initialScaleMult);
        }

        private void CreateUpwardParticleFountain(Transform parent, float scaleMult)
        {
            GameObject psGo = new GameObject("EvolutionParticleFountain");
            psGo.transform.SetParent(parent, false);
            psGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);

            _fountainPS = psGo.AddComponent<ParticleSystem>();
            _fountainPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _fountainPS.main;
            main.playOnAwake = false;
            main.duration = 3.0f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 11.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.44f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(_themeColor, Color.white, 0.45f),
                _themeColor);
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _fountainPS.emission;
            emission.enabled = true;
            emission.rateOverTime = 75f;

            var shape = _fountainPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.85f * Mathf.Max(1f, scaleMult);
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var vel = _fountainPS.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(3.8f);
            vel.radial = new ParticleSystem.MinMaxCurve(0.9f);

            var sizeOverLife = _fountainPS.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.4f),
                new Keyframe(0.25f, 1.0f),
                new Keyframe(1f, 0.05f));
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLife = _fountainPS.colorOverLifetime;
            colorOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(_themeColor, 0.4f), new GradientColorKey(_themeColor, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = grad;

            _particleMat = CreateAdditiveEffectMaterial(_cachedSparkTexture, Color.white);
            var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
            psRenderer.sharedMaterial = _particleMat;
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            _fountainPS.Play();
        }

        private void CleanupVfx()
        {
            if (_vfxRoot != null)
            {
                Destroy(_vfxRoot);
                _vfxRoot = null;
            }
            if (_innerBeamMat != null) { Destroy(_innerBeamMat); _innerBeamMat = null; }
            if (_outerBeamMat != null) { Destroy(_outerBeamMat); _outerBeamMat = null; }
            if (_runeMat != null) { Destroy(_runeMat); _runeMat = null; }
            if (_groundHaloMat != null) { Destroy(_groundHaloMat); _groundHaloMat = null; }
            if (_particleMat != null) { Destroy(_particleMat); _particleMat = null; }
            _runeQuads.Clear();
            _fountainPS = null;
        }

        private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material mat)
        {
            GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        private static Material CreateAdditiveEffectMaterial(Texture2D tex, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader);
            // 配置透明叠加（Additive）并关闭背面剔除与深度写入，确保从任意视角观察光柱与符文均通透发光
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f); // Additive
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.One);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent + 10;

            if (tex != null)
            {
                if (mat.HasProperty(BaseMapId)) mat.SetTexture(BaseMapId, tex);
                if (mat.HasProperty(MainTexId)) mat.SetTexture(MainTexId, tex);
            }
            SetMatColor(mat, color);
            return mat;
        }

        private static void SetMatColor(Material mat, Color col)
        {
            if (mat == null) return;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, col);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, col);
        }

        private static void EnsureSharedAssets()
        {
            if (_cachedOpenCylinderMesh == null) _cachedOpenCylinderMesh = CreateOpenCylinderMesh(18);
            if (_cachedRuneAtlas == null) _cachedRuneAtlas = CreateRuneAtlasTexture();
            if (_cachedHaloTexture == null) _cachedHaloTexture = CreateHaloRingTexture(128);
            if (_cachedSparkTexture == null) _cachedSparkTexture = CreateSparkDotTexture(64);
        }

        private static Mesh CreateOpenCylinderMesh(int segments)
        {
            Mesh mesh = new Mesh { name = "EvolutionBeaconCylinder" };
            Vector3[] verts = new Vector3[(segments + 1) * 2];
            Vector2[] uvs = new Vector2[verts.Length];
            int[] tris = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                float ang = u * Mathf.PI * 2f;
                float x = Mathf.Cos(ang);
                float z = Mathf.Sin(ang);

                verts[i * 2] = new Vector3(x, 0f, z);
                verts[i * 2 + 1] = new Vector3(x, 1f, z);
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                int t = i * 6;
                tris[t] = b;
                tris[t + 1] = b + 1;
                tris[t + 2] = b + 3;
                tris[t + 3] = b;
                tris[t + 4] = b + 3;
                tris[t + 5] = b + 2;
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh CreateUnitQuadMesh()
        {
            Mesh m = new Mesh { name = "EvolutionUnitQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            return m;
        }

        private static Mesh CreateRuneQuadMesh(int glyphIndex)
        {
            int col = glyphIndex % 4;
            int row = glyphIndex / 4;
            float u0 = col * 0.25f;
            float u1 = u0 + 0.25f;
            float v0 = row * 0.5f;
            float v1 = v0 + 0.5f;

            Mesh m = new Mesh { name = "EvolutionRuneQuad_" + glyphIndex };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            m.uv = new[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            return m;
        }

        /// <summary>
        /// 程序生成 4x2 远古神秘像素符文贴图集（8 种清晰发光符号）
        /// </summary>
        private static Texture2D CreateRuneAtlasTexture()
        {
            const int cell = 32;
            const int w = cell * 4;
            const int h = cell * 2;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "ProceduralMysticRuneAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            void Plot(int gx, int gy, int x, int y, float a = 1f)
            {
                if (x < 0 || x >= cell || y < 0 || y >= cell) return;
                int pxIdx = (gy * cell + y) * w + (gx * cell + x);
                px[pxIdx] = new Color(1f, 1f, 1f, Mathf.Max(px[pxIdx].a, a));
            }

            void DrawLine(int gx, int gy, int x0, int y0, int x1, int y1)
            {
                int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
                if (steps == 0) { Plot(gx, gy, x0, y0); return; }
                for (int s = 0; s <= steps; s++)
                {
                    float f = s / (float)steps;
                    int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, f));
                    int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, f));
                    Plot(gx, gy, x, y, 1f);
                    Plot(gx, gy, x + 1, y, 0.9f);
                    Plot(gx, gy, x, y + 1, 0.9f);
                }
            }

            // Glyph 0: 菱形龙眼符文
            DrawLine(0, 0, 16, 4, 27, 16); DrawLine(0, 0, 27, 16, 16, 27);
            DrawLine(0, 0, 16, 27, 4, 16); DrawLine(0, 0, 4, 16, 16, 4);
            DrawLine(0, 0, 16, 10, 16, 21);

            // Glyph 1: 三爪进化印记
            DrawLine(1, 0, 8, 6, 11, 25); DrawLine(1, 0, 16, 4, 16, 27); DrawLine(1, 0, 24, 6, 21, 25);
            DrawLine(1, 0, 7, 14, 24, 14);

            // Glyph 2: 远古六芒星芒
            DrawLine(2, 0, 16, 4, 26, 22); DrawLine(2, 0, 26, 22, 6, 22); DrawLine(2, 0, 6, 22, 16, 4);
            DrawLine(2, 0, 16, 27, 26, 9); DrawLine(2, 0, 26, 9, 6, 9); DrawLine(2, 0, 6, 9, 16, 27);

            // Glyph 3: 升华双翼纹
            DrawLine(3, 0, 16, 5, 16, 27); DrawLine(3, 0, 6, 12, 16, 22); DrawLine(3, 0, 26, 12, 16, 22);
            DrawLine(3, 0, 9, 7, 16, 14); DrawLine(3, 0, 23, 7, 16, 14);

            // Glyph 4: 螺旋 DNA 符
            DrawLine(0, 1, 9, 5, 22, 12); DrawLine(0, 1, 22, 12, 9, 19); DrawLine(0, 1, 9, 19, 22, 26);
            DrawLine(0, 1, 22, 5, 9, 12); DrawLine(0, 1, 9, 12, 22, 19); DrawLine(0, 1, 22, 19, 9, 26);

            // Glyph 5: 王冠尖碑符
            DrawLine(1, 1, 6, 8, 25, 8); DrawLine(1, 1, 6, 8, 9, 24); DrawLine(1, 1, 25, 8, 22, 24);
            DrawLine(1, 1, 9, 24, 16, 15); DrawLine(1, 1, 22, 24, 16, 15); DrawLine(1, 1, 16, 15, 16, 27);

            // Glyph 6: 太阳十字符
            DrawLine(2, 1, 16, 4, 16, 27); DrawLine(2, 1, 4, 16, 27, 16);
            DrawLine(2, 1, 10, 10, 21, 10); DrawLine(2, 1, 21, 10, 21, 21);
            DrawLine(2, 1, 21, 21, 10, 21); DrawLine(2, 1, 10, 21, 10, 10);

            // Glyph 7: 进化尖牙箭头
            DrawLine(3, 1, 16, 27, 6, 13); DrawLine(3, 1, 16, 27, 26, 13);
            DrawLine(3, 1, 16, 20, 9, 6); DrawLine(3, 1, 16, 20, 23, 6);

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateHaloRingTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ProceduralEvolutionHalo",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] px = new Color[size * size];
            float c = (size - 1) * 0.5f;
            float maxR = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / maxR;
                    float outerRing = Mathf.Exp(-Mathf.Pow((r - 0.88f) / 0.055f, 2f));
                    float innerRing = Mathf.Exp(-Mathf.Pow((r - 0.62f) / 0.045f, 2f)) * 0.75f;
                    float ang = Mathf.Atan2(dy, dx);
                    float ticks = (r > 0.62f && r < 0.88f) ? (Mathf.Pow(Mathf.Abs(Mathf.Sin(ang * 8f)), 10f) * 0.65f) : 0f;
                    float a = Mathf.Clamp01(outerRing + innerRing + ticks);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateSparkDotTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ProceduralEvolutionSpark",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] px = new Color[size * size];
            float c = (size - 1) * 0.5f;
            float maxR = size * 0.48f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / maxR;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
