using System;
using System.Collections.Generic;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.PvP.Config;
using AIWarsIdle.PvP.Services;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AIWarsIdle.UI.Pvp
{
    [ExecuteAlways]
    public sealed class HexGridRenderer : MonoBehaviour
    {
        [Serializable]
        private sealed class DuelStyleConfig
        {
            public bool ShowAdjacencyGrid = false;
            public float HackLinkThickness = 4.5f;
            public float ControlRingThickness = 3f;
            public float ControlRingScale = 0.88f;
            public float IdlePanelSideInset = 32f;
            public float IdlePanelTopInset = 56f;
        }

        private const int ScoreboardPlayerColumnWidth = 14;
        private const int ScoreboardPowerColumnWidth = 7;
        private const int ScoreboardScoreColumnWidth = 5;

        private enum HudSourceMode
        {
            Generated = 0,
            Prefab = 1,
            SceneInstance = 2
        }

        private const string GeneratedRootName = "__hex_grid_generated";
        private const string GeneratedPanelName = "__hex_grid_panel";
        private const string GeneratedHudName = "__hex_grid_hud";
        private const string GeneratedToastName = "__hex_grid_toast";
        private const string GeneratedDismissLayerName = "__hex_grid_dismiss";
        private const string GeneratedFrontierName = "__hex_grid_frontier";
        private const string GeneratedFrontierGlowName = "__hex_grid_frontier_glow";
        private const string GeneratedNetworkName = "__hex_grid_network";
        private const string GeneratedHackLinksName = "__hex_grid_hack_links";
        private const string GeneratedControlRingsName = "__hex_grid_control_rings";
        private const string SceneHudName = "PvpHexGridHud";
        private const string SceneDetailPanelName = "PvpHexGridPanel";

        [Header("Context")]
        [SerializeField] private PvpScreenContext _context;

        [Header("Grid")]
        [SerializeField, Min(0)] private int _radius = 4;
        [SerializeField, Min(8f)] private float _hexRadius = 42f;
        [SerializeField, Range(0.5f, 1f)] private float _cellFill = 0.9f;
        [SerializeField] private bool _preferRuntimeMapCount = true;

        [Header("Labels")]
        [SerializeField] private bool _showCoordinates = true;
        [SerializeField] private bool _showHomeBadge = true;
        [SerializeField, Range(0.1f, 1f)] private float _ownedHexAlpha = 0.72f;
        [SerializeField] private bool _showOwnershipNetwork = true;
        [SerializeField, Range(0.05f, 1f)] private float _ownershipNetworkAlpha = 0.35f;
        [SerializeField] private bool _showFrontlines = true;
        [SerializeField] private Color _frontlineColor = new(0.72f, 0.96f, 0.78f, 1f);
        [SerializeField, Min(1f)] private float _frontlineThickness = 4f;
        [SerializeField] private bool _showConnectivityMarkers = true;
        [SerializeField] private bool _showLocalFrontlineGlow = true;
        [SerializeField, Range(0.05f, 1f)] private float _localFrontlineGlowAlpha = 0.3f;
        [SerializeField, Min(1f)] private float _localFrontlineGlowThickness = 10f;
        [SerializeField, Range(0f, 2f)] private float _localFrontlineGlowPulseSpeed = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _localFrontlineGlowPulseDepth = 0.3f;

        [Header("Refresh")]
        [SerializeField] private float _refreshIntervalSeconds = 0.25f;

        [Header("Editor Preview")]
        [SerializeField] private bool _previewInEditMode = true;

        [Header("HUD")]
        [SerializeField] private HudSourceMode _hudSourceMode = HudSourceMode.SceneInstance;
        [SerializeField] private PvpHudTemplateView _hudPrefab;
        [SerializeField] private PvpHudTemplateView _sceneHud;

        [Header("Detail Panel")]
        [SerializeField] private HudSourceMode _detailPanelSourceMode = HudSourceMode.Prefab;
        [SerializeField] private PvpSectorDetailPanelView _detailPanelPrefab;
        [SerializeField] private PvpSectorDetailPanelView _sceneDetailPanel;
        [SerializeField, Min(0f)] private float _detailPanelHorizontalOffset = 164f;
        [SerializeField, Min(0f)] private float _detailPanelVerticalOffset = 104f;
        [SerializeField, Min(1f)] private float _detailPanelDistanceMultiplier = 1.35f;
        [SerializeField, Min(0f)] private float _detailPanelEdgePadding = 12f;

        [Header("Hex Hack Duel Style")]
        [SerializeField] private DuelStyleConfig _duelStyle = new();

        private readonly List<HexCellView> _cells = new();
        private readonly Dictionary<int, HexCellView> _cellsBySectorId = new();
        private RectTransform _generatedRoot;
        private PvpSectorDetailPanelView _detailPanel;
        private bool _ownsDetailPanelInstance;
        private PvpHudTemplateView _hud;
        private bool _ownsHudInstance;
        private PvpToastView _toast;
        private readonly List<TMP_Text> _externalAttacksTexts = new();
        private readonly List<TMP_Text> _externalRegenTexts = new();
        private readonly List<TMP_Text> _externalPvpPowerTexts = new();
        private HexCellView _selected;
        private float _refreshCarry;
        private int _lastLayoutHash;
        private Vector2 _lastViewportSize;
        private AttackStrategy _selectedStrategy = AttackStrategy.Stable;
        private bool _powerInfoVisible;
        private long _cachedLiveScoreStamp = long.MinValue;
        private string _cachedLiveScoreTable;
        private long _cachedDetailStamp = long.MinValue;
        private int _cachedDetailSectorId = -1;
        private AttackStrategy _cachedDetailStrategy = AttackStrategy.Stable;
        private SectorDetailState _cachedDetailState;
        private DuelStyleConfig DuelStyle => _duelStyle ??= new DuelStyleConfig();

        public int Radius
        {
            get => _radius;
            set
            {
                value = Mathf.Max(0, value);
                if (_radius == value) return;
                _radius = value;
                Rebuild();
            }
        }

        private void Awake()
        {
            if (_context == null) _context = GetComponent<PvpScreenContext>();
            if (_context == null) _context = GetComponentInParent<PvpScreenContext>();
        }

        private void OnEnable()
        {
            _refreshCarry = 0f;
            Rebuild();
        }

        private void OnDisable()
        {
            DestroyGeneratedRoot();
            _cells.Clear();
            _cellsBySectorId.Clear();
            _detailPanel = null;
            _ownsDetailPanelInstance = false;
            _hud = null;
            _ownsHudInstance = false;
            _toast = null;
            _externalAttacksTexts.Clear();
            _externalRegenTexts.Clear();
            _externalPvpPowerTexts.Clear();
            _selected = null;
            _lastLayoutHash = 0;
            _lastViewportSize = Vector2.zero;
            _cachedLiveScoreStamp = long.MinValue;
            _cachedLiveScoreTable = null;
            _cachedDetailStamp = long.MinValue;
            _cachedDetailSectorId = -1;
            _cachedDetailStrategy = AttackStrategy.Stable;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (_refreshIntervalSeconds <= 0f)
            {
                RefreshFromSource(forceRebuild: false);
                return;
            }

            _refreshCarry += Time.unscaledDeltaTime;
            if (_refreshCarry < _refreshIntervalSeconds) return;
            _refreshCarry = 0f;
            RefreshFromSource(forceRebuild: false);
        }

        private void OnValidate()
        {
            _radius = Mathf.Max(0, _radius);
            _hexRadius = Mathf.Max(8f, _hexRadius);
            _cellFill = Mathf.Clamp(_cellFill, 0.5f, 1f);
            _ownedHexAlpha = Mathf.Clamp(_ownedHexAlpha, 0.1f, 1f);
            _ownershipNetworkAlpha = Mathf.Clamp(_ownershipNetworkAlpha, 0.05f, 1f);
            _frontlineThickness = Mathf.Max(1f, _frontlineThickness);
            _localFrontlineGlowAlpha = Mathf.Clamp01(_localFrontlineGlowAlpha);
            _localFrontlineGlowThickness = Mathf.Max(1f, _localFrontlineGlowThickness);
            _localFrontlineGlowPulseSpeed = Mathf.Clamp(_localFrontlineGlowPulseSpeed, 0f, 2f);
            _localFrontlineGlowPulseDepth = Mathf.Clamp01(_localFrontlineGlowPulseDepth);
            _refreshIntervalSeconds = Mathf.Max(0f, _refreshIntervalSeconds);
            _detailPanelHorizontalOffset = Mathf.Max(0f, _detailPanelHorizontalOffset);
            _detailPanelVerticalOffset = Mathf.Max(0f, _detailPanelVerticalOffset);
            _detailPanelDistanceMultiplier = Mathf.Max(1f, _detailPanelDistanceMultiplier);
            _detailPanelEdgePadding = Mathf.Max(0f, _detailPanelEdgePadding);
            DuelStyle.HackLinkThickness = Mathf.Max(1f, DuelStyle.HackLinkThickness);
            DuelStyle.ControlRingThickness = Mathf.Max(1f, DuelStyle.ControlRingThickness);
            DuelStyle.ControlRingScale = Mathf.Clamp(DuelStyle.ControlRingScale, 0.5f, 1.2f);
            DuelStyle.IdlePanelSideInset = Mathf.Max(0f, DuelStyle.IdlePanelSideInset);
            DuelStyle.IdlePanelTopInset = Mathf.Max(0f, DuelStyle.IdlePanelTopInset);

            if (!isActiveAndEnabled) return;
            Rebuild();
        }

        private void Rebuild()
        {
            RefreshFromSource(forceRebuild: true);
        }

        private void RefreshFromSource(bool forceRebuild)
        {
            if (!ShouldRender())
            {
                DestroyGeneratedRoot();
                _cells.Clear();
                _cellsBySectorId.Clear();
                _detailPanel = null;
                _ownsDetailPanelInstance = false;
                _hud = null;
                _toast = null;
                _selected = null;
                _lastLayoutHash = 0;
                _lastViewportSize = Vector2.zero;
                return;
            }

            var snapshot = BuildSnapshot();
            var layoutHash = snapshot.ComputeLayoutHash();
            var viewportSize = GetViewportSize();
            if (forceRebuild || layoutHash != _lastLayoutHash || viewportSize != _lastViewportSize)
            {
                RebuildCells(snapshot);
                _lastLayoutHash = layoutHash;
                _lastViewportSize = viewportSize;
                return;
            }

            ApplySnapshot(snapshot);
        }

        private bool ShouldRender()
        {
            if (Application.isPlaying) return true;
            return _previewInEditMode;
        }

        private GridSnapshot BuildSnapshot()
        {
            var loop = _context != null ? _context.Loop : null;
            if (loop == null && _context != null && _context.Bootstrapper != null)
            {
                _context.Bootstrapper.Initialize();
                loop = _context.Loop;
            }

            var duel = _context?.HexHackDuel ?? loop?.HexHackDuel;
            if (duel != null)
            {
                return BuildDuelSnapshot(duel);
            }

            var config = loop?.MapConfig;
            if (config == null && _context != null && _context.Bootstrapper != null)
            {
                config = _context.Bootstrapper.MapConfig;
            }

            var runtimeSectors = loop?.State?.MapState?.Sectors;
            var runtimeCount = runtimeSectors?.Length ?? 0;
            var configCount = config?.SectorDefinitions?.Length ?? 0;

            var resolvedRadius = ResolveRadius(configCount, runtimeCount);
            var coords = HexGridMath.EnumerateAxial(resolvedRadius);
            var previewCorners = HexGridMath.GetCornerCoords(resolvedRadius);
            var sectors = new List<DisplaySector>(coords.Count);

            var stateById = new Dictionary<int, SectorState>();
            if (runtimeSectors != null)
            {
                for (var i = 0; i < runtimeSectors.Length; i++)
                {
                    var sector = runtimeSectors[i];
                    if (sector == null) continue;
                    if (!stateById.ContainsKey(sector.SectorId))
                    {
                        stateById.Add(sector.SectorId, sector);
                    }
                }
            }

            var useConfigOrder = config != null && config.SectorDefinitions != null && config.SectorDefinitions.Length == coords.Count;
            var useRuntimeOrder = !useConfigOrder && runtimeSectors != null && runtimeSectors.Length == coords.Count;

            for (var i = 0; i < coords.Count; i++)
            {
                var coord = coords[i];
                var sector = new DisplaySector
                {
                    Coord = coord,
                    SectorIndex = i,
                    SectorId = i,
                    Label = $"S{i}",
                    IsHome = previewCorners.Contains(coord),
                    OwnerPlayerId = previewCorners.Contains(coord) ? i + 1 : 0,
                    Stability = 0f,
                    InfoText = string.Empty,
                };

                if (useConfigOrder)
                {
                    var def = config.SectorDefinitions[i];
                    if (def != null)
                    {
                        sector.SectorId = def.SectorId;
                        sector.Label = string.IsNullOrWhiteSpace(def.Name) ? $"S{def.SectorId}" : def.Name;
                        sector.ProductionBonusPercent = def.ProductionBonusPercent;
                    }

                    sector.IsHome = config.IsHomeSector(sector.SectorId);
                    if (stateById.TryGetValue(sector.SectorId, out var runtimeState))
                    {
                        sector.OwnerPlayerId = runtimeState.OwnerPlayerId;
                        sector.Stability = runtimeState.Stability;
                    }
                    else if (sector.IsHome)
                    {
                        sector.OwnerPlayerId = Mathf.Max(1, config.GetHomeOwnerPlayerId(sector.SectorId));
                        sector.Stability = config.HomeSectorStability;
                    }
                }
                else if (useRuntimeOrder)
                {
                    var runtimeState = runtimeSectors[i];
                    if (runtimeState != null)
                    {
                        sector.SectorId = runtimeState.SectorId;
                        sector.Label = $"S{runtimeState.SectorId}";
                        sector.OwnerPlayerId = runtimeState.OwnerPlayerId;
                        sector.Stability = runtimeState.Stability;
                        sector.IsHome = config != null && config.IsHomeSector(runtimeState.SectorId);
                    }
                }

                sectors.Add(sector);
            }

            for (var i = 0; i < sectors.Count; i++)
            {
                var sector = sectors[i];
                var shouldShowStability = sector.IsHome || sector.Stability < 100f;
                sector.InfoText = shouldShowStability ? $"{Mathf.RoundToInt(sector.Stability)}%" : string.Empty;
                sectors[i] = sector;
            }

            ApplyConnectivityMarkers(config, sectors);

            return new GridSnapshot(resolvedRadius, coords.Count, sectors);
        }

        private GridSnapshot BuildDuelSnapshot(HexHackDuelService duel)
        {
            var sectors = new List<DisplaySector>(duel.Nodes.Count);
            for (var i = 0; i < duel.Nodes.Count; i++)
            {
                var node = duel.Nodes[i];
                sectors.Add(new DisplaySector
                {
                    Coord = new HexCoord(node.Coord.Q, node.Coord.R),
                    SectorIndex = i,
                    SectorId = node.Id,
                    Label = node.Label,
                    IsHome = false,
                    IsCoreSector = node.IsCore,
                    OwnerPlayerId = node.OwnerPlayerId,
                    Stability = Mathf.Abs(node.Control),
                    ControlValue = node.Control,
                    ProductionBonusPercent = node.IsCore ? 30f : 0f,
                    InfoText = BuildDuelHexInfoText(node),
                    BadgeText = node.IsCore ? "CORE" : string.Empty,
                    IsDuelSector = true
                });
            }

            return new GridSnapshot(1, sectors.Count, sectors);
        }

        private int ResolveRadius(int configCount, int runtimeCount)
        {
            if (_preferRuntimeMapCount)
            {
                if (TryInferRadiusFromCellCount(configCount, out var fromConfig))
                {
                    return fromConfig;
                }

                if (TryInferRadiusFromCellCount(runtimeCount, out var fromRuntime))
                {
                    return fromRuntime;
                }
            }

            return _radius;
        }

        private void RebuildCells(GridSnapshot snapshot)
        {
            _cells.Clear();
            _cellsBySectorId.Clear();
            _selected = null;

            var root = GetOrCreateGeneratedRoot();
            ClearChildren(root);

            for (var i = 0; i < snapshot.Sectors.Count; i++)
            {
                CreateCell(root, snapshot.Sectors[i]);
            }

            UpdateOwnershipNetwork(root, snapshot);
            UpdateFrontlines(root, snapshot);
            UpdateDuelOverlays(root, snapshot);

            FitGridToViewport(root);
            EnsureOverlaySiblingOrder();

            UpdateHud(snapshot);
            UpdateIdleDuelPanel();
            UpdateDuelViewVisibility();
            if (_detailPanel != null)
            {
                var duel = _context?.HexHackDuel;
                if (duel == null || duel.IsMatchActive || _selected != null)
                {
                    _detailPanel.Hide();
                }
            }
        }

        private void ApplySnapshot(GridSnapshot snapshot)
        {
            for (var i = 0; i < snapshot.Sectors.Count; i++)
            {
                var sector = snapshot.Sectors[i];
                if (!_cellsBySectorId.TryGetValue(sector.SectorId, out var cell))
                {
                    RebuildCells(snapshot);
                    return;
                }

                cell.Apply(sector, _showCoordinates, _showHomeBadge, _ownedHexAlpha, _showConnectivityMarkers);
            }

            if (_selected != null)
            {
                if (_cellsBySectorId.TryGetValue(_selected.SectorId, out var selectedCell))
                {
                    UpdateDetailPanel(selectedCell);
                }
                else
                {
                    ClearSelection();
                }
            }

            UpdateOwnershipNetwork(_generatedRoot, snapshot);
            UpdateFrontlines(_generatedRoot, snapshot);
            UpdateDuelOverlays(_generatedRoot, snapshot);
            EnsureOverlaySiblingOrder();
            UpdateHud(snapshot);
            UpdateIdleDuelPanel();
            UpdateDuelViewVisibility();
        }

        private RectTransform GetOrCreateGeneratedRoot()
        {
            if (_generatedRoot != null) return _generatedRoot;

            var existing = transform.Find(GeneratedRootName) as RectTransform;
            if (existing != null)
            {
                _generatedRoot = existing;
                return _generatedRoot;
            }

            var go = new GameObject(GeneratedRootName, typeof(RectTransform));
            go.transform.SetParent(transform, worldPositionStays: false);

            if (!Application.isPlaying)
            {
                go.hideFlags = HideFlags.DontSaveInEditor;
            }

            _generatedRoot = go.GetComponent<RectTransform>();
            _generatedRoot.anchorMin = Vector2.zero;
            _generatedRoot.anchorMax = Vector2.one;
            _generatedRoot.offsetMin = Vector2.zero;
            _generatedRoot.offsetMax = Vector2.zero;
            _generatedRoot.localScale = Vector3.one;
            GetOrCreateDismissLayer(_generatedRoot);
            return _generatedRoot;
        }

        private Button GetOrCreateDismissLayer(RectTransform parent)
        {
            var existing = parent.Find(GeneratedDismissLayerName);
            if (existing != null && existing.TryGetComponent<Button>(out var existingButton))
            {
                existing.SetAsFirstSibling();
                return existingButton;
            }

            var go = new GameObject(
                GeneratedDismissLayerName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.SetAsFirstSibling();
            HideEditorObject(go);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(ClearSelection);
            return button;
        }

        private PvpSectorDetailPanelView GetOrCreateDetailPanel()
        {
            if (_detailPanel != null) return _detailPanel;

            if (_detailPanelSourceMode == HudSourceMode.SceneInstance)
            {
                if (_sceneDetailPanel == null)
                {
                    var scenePanel = transform.Find(SceneDetailPanelName);
                    if (scenePanel != null)
                    {
                        _sceneDetailPanel = scenePanel.GetComponent<PvpSectorDetailPanelView>();
                    }
                }

                if (_sceneDetailPanel != null)
                {
                    var generatedPanel = transform.Find(GeneratedPanelName);
                    if (generatedPanel != null)
                    {
                        DestroyObject(generatedPanel.gameObject);
                    }

                    _detailPanel = _sceneDetailPanel;
                    _detailPanel.SetActions(OnCycleStrategy, OnAttackSelectedSector);
                    _ownsDetailPanelInstance = false;
                    _detailPanel.transform.SetAsLastSibling();
                    return _detailPanel;
                }
            }

            var existing = transform.Find(GeneratedPanelName);
            if (existing != null)
            {
                _detailPanel = existing.GetComponent<PvpSectorDetailPanelView>();
                if (_detailPanel != null)
                {
                    _detailPanel.SetActions(OnCycleStrategy, OnAttackSelectedSector);
                    _ownsDetailPanelInstance = true;
                    _detailPanel.transform.SetAsLastSibling();
                    return _detailPanel;
                }
            }

            if (_detailPanelSourceMode == HudSourceMode.Prefab && _detailPanelPrefab != null)
            {
                _detailPanel = Instantiate(_detailPanelPrefab, transform, false);
                _detailPanel.name = GeneratedPanelName;
                HideEditorObject(_detailPanel.gameObject);
                _detailPanel.SetActions(OnCycleStrategy, OnAttackSelectedSector);
                _detailPanel.ShowEmpty();
                _detailPanel.Hide();
                _ownsDetailPanelInstance = true;
                _detailPanel.transform.SetAsLastSibling();
                return _detailPanel;
            }

            var panelGo = new GameObject(
                GeneratedPanelName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(PvpSectorDetailPanelView));
            panelGo.transform.SetParent(transform, worldPositionStays: false);
            HideEditorObject(panelGo);

            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(280f, 420f);
            rect.anchoredPosition = Vector2.zero;

            var bg = panelGo.GetComponent<Image>();
            bg.color = new Color32(26, 30, 43, 232);
            bg.raycastTarget = true;

            _detailPanel = panelGo.GetComponent<PvpSectorDetailPanelView>();
            _detailPanel.Initialize(
                CreatePanelTitle(rect),
                CreatePanelLine(rect, "owner", new Vector2(16f, -56f)),
                CreatePanelLine(rect, "bonus", new Vector2(16f, -90f)),
                CreatePanelLine(rect, "stability", new Vector2(16f, -124f)),
                CreatePanelLine(rect, "preview", new Vector2(16f, -158f), height: 34f),
                CreatePanelLine(rect, "position", new Vector2(16f, -198f)),
                CreatePanelLine(rect, "status", new Vector2(16f, -244f), height: 60f),
                CreatePanelSecondaryButton(rect, "strategy_button", new Vector2(16f, 102f), "Strategy: Stable"),
                CreatePanelActionButton(rect),
                CreatePanelHint(rect),
                OnCycleStrategy,
                OnAttackSelectedSector);
            _detailPanel.ShowEmpty();
            _detailPanel.Hide();
            _ownsDetailPanelInstance = true;
            _detailPanel.transform.SetAsLastSibling();
            return _detailPanel;
        }

        private PvpHudTemplateView GetOrCreateHud()
        {
            if (_hud != null) return _hud;

            if (_hudSourceMode == HudSourceMode.SceneInstance)
            {
                if (_sceneHud == null)
                {
                    var sceneHud = transform.Find(SceneHudName);
                    if (sceneHud != null)
                    {
                        _sceneHud = sceneHud.GetComponent<PvpHudTemplateView>();
                    }
                }

                if (_sceneHud != null)
                {
                    var generatedHud = transform.Find(GeneratedHudName);
                    if (generatedHud != null)
                    {
                        DestroyObject(generatedHud.gameObject);
                    }

                    _hud = _sceneHud;
                    _hud.SetPowerInfoActions(ShowPowerInfoTooltip, HidePowerInfoTooltip);
                    _ownsHudInstance = false;
                    _hud.transform.SetAsLastSibling();
                    return _hud;
                }
            }

            var existing = transform.Find(GeneratedHudName);
            if (existing != null)
            {
                _hud = existing.GetComponent<PvpHudTemplateView>();
                if (_hud != null)
                {
                    _hud.SetPowerInfoActions(ShowPowerInfoTooltip, HidePowerInfoTooltip);
                    _ownsHudInstance = true;
                    _hud.transform.SetAsLastSibling();
                    return _hud;
                }
            }

            if (_hudSourceMode == HudSourceMode.Prefab && _hudPrefab != null)
            {
                _hud = Instantiate(_hudPrefab, transform, false);
                _hud.name = GeneratedHudName;
                HideEditorObject(_hud.gameObject);
                _hud.SetPowerInfoActions(ShowPowerInfoTooltip, HidePowerInfoTooltip);
                _ownsHudInstance = true;
                _hud.transform.SetAsLastSibling();
                return _hud;
            }

            var hudGo = new GameObject(
                GeneratedHudName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(PvpHudTemplateView));
            hudGo.transform.SetParent(transform, worldPositionStays: false);
            HideEditorObject(hudGo);

            var rect = hudGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(360f, 270f);
            rect.anchoredPosition = new Vector2(12f, -12f);

            var bg = hudGo.GetComponent<Image>();
            bg.color = new Color32(18, 24, 34, 224);

            _hud = hudGo.GetComponent<PvpHudTemplateView>();
            _hud.Initialize(
                CreateHudText(rect, "attacks", new Vector2(16f, -18f), 22f),
                CreateHudText(rect, "match_ends", new Vector2(16f, -50f), 18f),
                CreateHudText(rect, "power", new Vector2(16f, -80f), 18f),
                CreateHudText(rect, "table_title", new Vector2(16f, -112f), 14f),
                CreateHudTable(rect, "live_score_table", new Vector2(16f, -138f), 118f),
                CreatePanelSecondaryButton(rect, "power_info_button", new Vector2(240f, 220f), "PvpPower ?"),
                CreateHudTooltip(rect),
                ShowPowerInfoTooltip);
            _ownsHudInstance = true;
            _hud.transform.SetAsLastSibling();
            return _hud;
        }

        private PvpToastView GetOrCreateToast()
        {
            if (_toast != null) return _toast;

            var existing = transform.Find(GeneratedToastName);
            if (existing != null)
            {
                _toast = existing.GetComponent<PvpToastView>();
                if (_toast != null) return _toast;
            }

            var go = new GameObject(
                GeneratedToastName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(PvpToastView));
            go.transform.SetParent(transform, worldPositionStays: false);
            HideEditorObject(go);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(360f, 52f);
            rect.anchoredPosition = new Vector2(0f, 20f);

            var bg = go.GetComponent<Image>();
            bg.color = new Color32(22, 31, 46, 238);

            _toast = go.GetComponent<PvpToastView>();
            _toast.Initialize(CreateHudText(rect, "toast", new Vector2(0f, -12f), 18f, centered: true));
            _toast.transform.SetAsLastSibling();
            return _toast;
        }

        private void EnsureOverlaySiblingOrder()
        {
            if (_generatedRoot != null)
            {
                _generatedRoot.SetAsFirstSibling();
                SetOverlayLastSibling(_generatedRoot, GeneratedNetworkName);
                SetOverlayLastSibling(_generatedRoot, GeneratedFrontierName);
                SetOverlayLastSibling(_generatedRoot, GeneratedFrontierGlowName);
                SetOverlayLastSibling(_generatedRoot, GeneratedHackLinksName);
                SetOverlayLastSibling(_generatedRoot, GeneratedControlRingsName);
            }

            if (_hud != null)
            {
                _hud.transform.SetAsLastSibling();
            }

            if (_toast != null)
            {
                _toast.transform.SetAsLastSibling();
            }

            if (_detailPanel != null)
            {
                _detailPanel.transform.SetAsLastSibling();
            }
        }

        private static void SetOverlayLastSibling(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName)) return;
            var child = parent.Find(childName);
            if (child != null)
            {
                child.SetAsLastSibling();
            }
        }

        private void DestroyGeneratedRoot()
        {
            if (_generatedRoot == null)
            {
                var existing = transform.Find(GeneratedRootName);
                if (existing != null)
                {
                    DestroyObject(existing.gameObject);
                }

                return;
            }

            DestroyObject(_generatedRoot.gameObject);
            _generatedRoot = null;

            var panel = transform.Find(GeneratedPanelName);
            if (panel != null && _ownsDetailPanelInstance)
            {
                DestroyObject(panel.gameObject);
            }

            var hud = transform.Find(GeneratedHudName);
            if (hud != null && _ownsHudInstance)
            {
                DestroyObject(hud.gameObject);
            }

            var toast = transform.Find(GeneratedToastName);
            if (toast != null)
            {
                DestroyObject(toast.gameObject);
            }
        }

        private void ClearChildren(RectTransform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child.name == GeneratedDismissLayerName) continue;
                DestroyObject(child.gameObject);
            }
        }

        private void CreateCell(RectTransform parent, DisplaySector sector)
        {
            var cellGo = new GameObject($"hex_{sector.Coord.Q}_{sector.Coord.R}", typeof(RectTransform), typeof(CanvasRenderer));
            cellGo.transform.SetParent(parent, worldPositionStays: false);

            if (!Application.isPlaying)
            {
                cellGo.hideFlags = HideFlags.DontSaveInEditor;
            }

            var rect = cellGo.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var visualRadius = _hexRadius * _cellFill;
            rect.sizeDelta = HexGridMath.GetCellSize(visualRadius);
            rect.anchoredPosition = HexGridMath.AxialToLocalPosition(sector.Coord, _hexRadius);

            var graphic = cellGo.AddComponent<HexCellGraphic>();
            var button = cellGo.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 0.88f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            button.colors = colors;

            var cell = cellGo.AddComponent<HexCellView>();
            cell.Initialize(
                graphic,
                button,
                CreateLabel(rect),
                CreateInfoLabel(rect),
                CreateHomeBadge(rect),
                CreateMarkerLabel(rect),
                HandleDuelCellDragBegin,
                HandleDuelCellDragEnd);
            cell.Apply(sector, _showCoordinates, _showHomeBadge, _ownedHexAlpha, _showConnectivityMarkers);

            button.onClick.AddListener(() => SelectCell(cell));
            _cells.Add(cell);
            _cellsBySectorId[sector.SectorId] = cell;
        }

        private Vector2 GetViewportSize()
        {
            var rect = transform as RectTransform;
            return rect != null ? rect.rect.size : Vector2.zero;
        }

        private void FitGridToViewport(RectTransform root)
        {
            if (root == null || root.childCount == 0) return;

            var hasBounds = false;
            var minX = 0f;
            var maxX = 0f;
            var minY = 0f;
            var maxY = 0f;

            for (var i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i) is not RectTransform child) continue;

                var pos = child.anchoredPosition;
                var size = child.sizeDelta;
                var halfWidth = size.x * 0.5f;
                var halfHeight = size.y * 0.5f;

                var left = pos.x - halfWidth;
                var right = pos.x + halfWidth;
                var bottom = pos.y - halfHeight;
                var top = pos.y + halfHeight;

                if (!hasBounds)
                {
                    minX = left;
                    maxX = right;
                    minY = bottom;
                    maxY = top;
                    hasBounds = true;
                    continue;
                }

                if (left < minX) minX = left;
                if (right > maxX) maxX = right;
                if (bottom < minY) minY = bottom;
                if (top > maxY) maxY = top;
            }

            if (!hasBounds) return;

            var viewport = root.rect;
            if (viewport.width <= 0f) return;

            var contentWidth = Mathf.Max(1f, maxX - minX);
            var scale = viewport.width / contentWidth;

            root.localScale = new Vector3(scale, scale, 1f);

            var contentCenterX = (minX + maxX) * 0.5f;
            var parentBottomY = -viewport.height * 0.5f;
            var scaledBottomY = minY * scale;

            root.anchoredPosition = new Vector2(
                -(contentCenterX * scale),
                parentBottomY - scaledBottomY);
        }

        private void UpdateFrontlines(RectTransform root, GridSnapshot snapshot)
        {
            if (root == null) return;

            var graphic = GetOrCreateFrontlineGraphic(root);
            var glowGraphic = GetOrCreateFrontlineGlowGraphic(root);
            if (graphic == null) return;

            if (!_showFrontlines)
            {
                graphic.SetSegments(Array.Empty<HexFrontierGraphic.LineSegment>(), _frontlineThickness);
                graphic.gameObject.SetActive(false);
                if (glowGraphic != null)
                {
                    glowGraphic.SetSegments(Array.Empty<HexFrontierGraphic.LineSegment>(), _localFrontlineGlowThickness);
                    glowGraphic.gameObject.SetActive(false);
                }
                return;
            }

            var segments = BuildFrontlineSegments(snapshot);
            graphic.gameObject.SetActive(segments.Count > 0);
            graphic.SetSegments(segments, _frontlineThickness);

            if (glowGraphic != null)
            {
                var glowSegments = _showLocalFrontlineGlow
                    ? BuildLocalFrontlineGlowSegments(snapshot)
                    : new List<HexFrontierGraphic.LineSegment>();
                glowGraphic.gameObject.SetActive(glowSegments.Count > 0);
                glowGraphic.SetSegments(glowSegments, _localFrontlineGlowThickness);
            }
        }

        private void UpdateOwnershipNetwork(RectTransform root, GridSnapshot snapshot)
        {
            if (root == null) return;

            var graphic = GetOrCreateNetworkGraphic(root);
            if (graphic == null) return;

            var isDuelSnapshot = ContainsDuelSectors(snapshot);
            var shouldShowOwnershipNetwork = _showOwnershipNetwork && (!isDuelSnapshot || DuelStyle.ShowAdjacencyGrid);
            if (!shouldShowOwnershipNetwork)
            {
                graphic.SetSegments(Array.Empty<HexFrontierGraphic.LineSegment>(), Mathf.Max(1f, _frontlineThickness * 0.8f));
                graphic.gameObject.SetActive(false);
                return;
            }

            var segments = isDuelSnapshot
                ? BuildDuelOwnershipNetworkSegments(snapshot)
                : BuildOwnershipNetworkSegments(snapshot);
            graphic.gameObject.SetActive(segments.Count > 0);
            graphic.SetSegments(segments, Mathf.Max(1f, _frontlineThickness * 0.8f));
        }

        private HexFrontierGraphic GetOrCreateFrontlineGraphic(RectTransform parent)
        {
            var existing = parent.Find(GeneratedFrontierName);
            if (existing != null)
            {
                return existing.GetComponent<HexFrontierGraphic>();
            }

            var go = new GameObject(GeneratedFrontierName, typeof(RectTransform), typeof(CanvasRenderer), typeof(HexFrontierGraphic));
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.SetSiblingIndex(Math.Min(1, parent.childCount - 1));
            HideEditorObject(go);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            var graphic = go.GetComponent<HexFrontierGraphic>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private HexFrontierGraphic GetOrCreateFrontlineGlowGraphic(RectTransform parent)
        {
            var existing = parent.Find(GeneratedFrontierGlowName);
            if (existing != null)
            {
                return existing.GetComponent<HexFrontierGraphic>();
            }

            var go = new GameObject(GeneratedFrontierGlowName, typeof(RectTransform), typeof(CanvasRenderer), typeof(HexFrontierGraphic));
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.SetSiblingIndex(Math.Min(1, parent.childCount - 1));
            HideEditorObject(go);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            var graphic = go.GetComponent<HexFrontierGraphic>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private HexFrontierGraphic GetOrCreateNetworkGraphic(RectTransform parent)
        {
            var existing = parent.Find(GeneratedNetworkName);
            if (existing != null)
            {
                return existing.GetComponent<HexFrontierGraphic>();
            }

            var go = new GameObject(GeneratedNetworkName, typeof(RectTransform), typeof(CanvasRenderer), typeof(HexFrontierGraphic));
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.SetSiblingIndex(Math.Min(1, parent.childCount - 1));
            HideEditorObject(go);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            var graphic = go.GetComponent<HexFrontierGraphic>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private HexFrontierGraphic GetOrCreateHackLinksGraphic(RectTransform parent)
        {
            var existing = parent.Find(GeneratedHackLinksName);
            if (existing != null)
            {
                return existing.GetComponent<HexFrontierGraphic>();
            }

            var go = new GameObject(GeneratedHackLinksName, typeof(RectTransform), typeof(CanvasRenderer), typeof(HexFrontierGraphic));
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.SetSiblingIndex(Math.Min(1, parent.childCount - 1));
            HideEditorObject(go);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            var graphic = go.GetComponent<HexFrontierGraphic>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private HexFrontierGraphic GetOrCreateControlRingsGraphic(RectTransform parent)
        {
            var existing = parent.Find(GeneratedControlRingsName);
            if (existing != null)
            {
                return existing.GetComponent<HexFrontierGraphic>();
            }

            var go = new GameObject(GeneratedControlRingsName, typeof(RectTransform), typeof(CanvasRenderer), typeof(HexFrontierGraphic));
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.SetSiblingIndex(Math.Min(1, parent.childCount - 1));
            HideEditorObject(go);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            var graphic = go.GetComponent<HexFrontierGraphic>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private void UpdateDuelOverlays(RectTransform root, GridSnapshot snapshot)
        {
            var linksGraphic = GetOrCreateHackLinksGraphic(root);
            var ringsGraphic = GetOrCreateControlRingsGraphic(root);
            var duel = _context?.HexHackDuel;
            if (duel == null || !ContainsDuelSectors(snapshot))
            {
                linksGraphic.SetSegments(Array.Empty<HexFrontierGraphic.LineSegment>(), Mathf.Max(2f, _frontlineThickness * 0.9f));
                linksGraphic.gameObject.SetActive(false);
                ringsGraphic.SetSegments(Array.Empty<HexFrontierGraphic.LineSegment>(), DuelStyle.ControlRingThickness);
                ringsGraphic.gameObject.SetActive(false);
                return;
            }

            var links = BuildDuelLinkSegments(snapshot, duel);
            linksGraphic.gameObject.SetActive(links.Count > 0);
            linksGraphic.SetSegments(links, DuelStyle.HackLinkThickness);

            var rings = BuildDuelControlRingSegments(snapshot);
            ringsGraphic.gameObject.SetActive(rings.Count > 0);
            ringsGraphic.SetSegments(rings, DuelStyle.ControlRingThickness);
        }

        private List<HexFrontierGraphic.LineSegment> BuildFrontlineSegments(GridSnapshot snapshot)
        {
            var segments = new List<HexFrontierGraphic.LineSegment>();
            if (snapshot.Sectors == null || snapshot.Sectors.Count == 0) return segments;

            var byCoord = new Dictionary<HexCoord, DisplaySector>(snapshot.Sectors.Count);
            var processedEdges = new HashSet<long>();
            for (var i = 0; i < snapshot.Sectors.Count; i++)
            {
                byCoord[snapshot.Sectors[i].Coord] = snapshot.Sectors[i];
            }

            for (var i = 0; i < snapshot.Sectors.Count; i++)
            {
                var sector = snapshot.Sectors[i];
                if (sector.OwnerPlayerId <= 0) continue;

                for (var neighborIndex = 0; neighborIndex < HexGridMath.NeighborDirections.Length; neighborIndex++)
                {
                    var neighborCoord = new HexCoord(
                        sector.Coord.Q + HexGridMath.NeighborDirections[neighborIndex].Q,
                        sector.Coord.R + HexGridMath.NeighborDirections[neighborIndex].R);

                    if (!byCoord.TryGetValue(neighborCoord, out var neighbor)) continue;
                    if (neighbor.OwnerPlayerId == sector.OwnerPlayerId) continue;

                    var edgeKey = GetEdgeKey(sector.SectorId, neighbor.SectorId);
                    if (!processedEdges.Add(edgeKey)) continue;

                    var a = HexGridMath.AxialToLocalPosition(sector.Coord, _hexRadius);
                    var b = HexGridMath.AxialToLocalPosition(neighbor.Coord, _hexRadius);
                    var edge = HexGridMath.GetSharedEdge(a, b, _hexRadius);
                    var centerDirection = (b - a).normalized;

                    if (sector.OwnerPlayerId > 0 && neighbor.OwnerPlayerId > 0)
                    {
                        var splitOffset = centerDirection * (_frontlineThickness * 0.65f);
                        segments.Add(new HexFrontierGraphic.LineSegment(
                            edge.start - splitOffset,
                            edge.end - splitOffset,
                            HexGridPalette.GetFrontlineColor(sector.OwnerPlayerId, _frontlineColor)));
                        segments.Add(new HexFrontierGraphic.LineSegment(
                            edge.start + splitOffset,
                            edge.end + splitOffset,
                            HexGridPalette.GetFrontlineColor(neighbor.OwnerPlayerId, _frontlineColor)));
                    }
                    else
                    {
                        var ownerPlayerId = sector.OwnerPlayerId > 0 ? sector.OwnerPlayerId : neighbor.OwnerPlayerId;
                        segments.Add(new HexFrontierGraphic.LineSegment(
                            edge.start,
                            edge.end,
                            HexGridPalette.GetFrontlineColor(ownerPlayerId, _frontlineColor)));
                    }
                }
            }

            return segments;
        }

        private List<HexFrontierGraphic.LineSegment> BuildOwnershipNetworkSegments(GridSnapshot snapshot)
        {
            var segments = new List<HexFrontierGraphic.LineSegment>();
            if (snapshot.Sectors == null || snapshot.Sectors.Count == 0) return segments;

            var config = GetActiveMapConfig();
            if (config == null) return segments;

            var byCoord = new Dictionary<HexCoord, DisplaySector>(snapshot.Sectors.Count);
            var byId = new Dictionary<int, DisplaySector>(snapshot.Sectors.Count);
            var processedEdges = new HashSet<long>();
            for (var i = 0; i < snapshot.Sectors.Count; i++)
            {
                byCoord[snapshot.Sectors[i].Coord] = snapshot.Sectors[i];
                byId[snapshot.Sectors[i].SectorId] = snapshot.Sectors[i];
            }

            var connectedToHome = BuildHomeConnectedSectorSet(snapshot, byCoord, byId, config);

            for (var i = 0; i < snapshot.Sectors.Count; i++)
            {
                var sector = snapshot.Sectors[i];
                if (sector.OwnerPlayerId <= 0) continue;
                if (!connectedToHome.Contains(sector.SectorId)) continue;

                for (var neighborIndex = 0; neighborIndex < HexGridMath.NeighborDirections.Length; neighborIndex++)
                {
                    var neighborCoord = new HexCoord(
                        sector.Coord.Q + HexGridMath.NeighborDirections[neighborIndex].Q,
                        sector.Coord.R + HexGridMath.NeighborDirections[neighborIndex].R);
                    if (!byCoord.TryGetValue(neighborCoord, out var neighbor)) continue;
                    if (neighbor.OwnerPlayerId != sector.OwnerPlayerId) continue;
                    if (!connectedToHome.Contains(neighbor.SectorId)) continue;

                    var edgeKey = GetEdgeKey(sector.SectorId, neighbor.SectorId);
                    if (!processedEdges.Add(edgeKey)) continue;

                    var start = HexGridMath.AxialToLocalPosition(sector.Coord, _hexRadius);
                    var end = HexGridMath.AxialToLocalPosition(neighbor.Coord, _hexRadius);
                    segments.Add(new HexFrontierGraphic.LineSegment(
                        start,
                        end,
                        HexGridPalette.GetNetworkColor(sector.OwnerPlayerId, _ownershipNetworkAlpha)));
                }
            }

            return segments;
        }

        private List<HexFrontierGraphic.LineSegment> BuildLocalFrontlineGlowSegments(GridSnapshot snapshot)
        {
            var segments = new List<HexFrontierGraphic.LineSegment>();
            if (snapshot.Sectors == null || snapshot.Sectors.Count == 0) return segments;

            var config = GetActiveMapConfig();
            var localPlayerId = config != null ? config.LocalPlayerId : 1;
            var glowAlpha = GetPulsedGlowAlpha();

            var byCoord = new Dictionary<HexCoord, DisplaySector>(snapshot.Sectors.Count);
            var processedEdges = new HashSet<long>();
            for (var i = 0; i < snapshot.Sectors.Count; i++)
            {
                byCoord[snapshot.Sectors[i].Coord] = snapshot.Sectors[i];
            }

            for (var i = 0; i < snapshot.Sectors.Count; i++)
            {
                var sector = snapshot.Sectors[i];
                if (sector.OwnerPlayerId <= 0) continue;

                for (var neighborIndex = 0; neighborIndex < HexGridMath.NeighborDirections.Length; neighborIndex++)
                {
                    var neighborCoord = new HexCoord(
                        sector.Coord.Q + HexGridMath.NeighborDirections[neighborIndex].Q,
                        sector.Coord.R + HexGridMath.NeighborDirections[neighborIndex].R);

                    if (!byCoord.TryGetValue(neighborCoord, out var neighbor)) continue;
                    if (neighbor.OwnerPlayerId == sector.OwnerPlayerId) continue;

                    var edgeKey = GetEdgeKey(sector.SectorId, neighbor.SectorId);
                    if (!processedEdges.Add(edgeKey)) continue;

                    var a = HexGridMath.AxialToLocalPosition(sector.Coord, _hexRadius);
                    var b = HexGridMath.AxialToLocalPosition(neighbor.Coord, _hexRadius);
                    var edge = HexGridMath.GetSharedEdge(a, b, _hexRadius);
                    var centerDirection = (b - a).normalized;
                    var glowColor = HexGridPalette.GetGlowColor(localPlayerId, glowAlpha);

                    if (sector.OwnerPlayerId == localPlayerId && neighbor.OwnerPlayerId > 0)
                    {
                        var splitOffset = centerDirection * (_frontlineThickness * 0.65f);
                        segments.Add(new HexFrontierGraphic.LineSegment(
                            edge.start - splitOffset,
                            edge.end - splitOffset,
                            glowColor));
                    }
                    else if (neighbor.OwnerPlayerId == localPlayerId && sector.OwnerPlayerId > 0)
                    {
                        var splitOffset = centerDirection * (_frontlineThickness * 0.65f);
                        segments.Add(new HexFrontierGraphic.LineSegment(
                            edge.start + splitOffset,
                            edge.end + splitOffset,
                            glowColor));
                    }
                    else if (sector.OwnerPlayerId == localPlayerId || neighbor.OwnerPlayerId == localPlayerId)
                    {
                        segments.Add(new HexFrontierGraphic.LineSegment(
                            edge.start,
                            edge.end,
                            glowColor));
                    }
                }
            }

            return segments;
        }

        private List<HexFrontierGraphic.LineSegment> BuildDuelLinkSegments(GridSnapshot snapshot, HexHackDuelService duel)
        {
            var segments = new List<HexFrontierGraphic.LineSegment>();
            var byId = new Dictionary<int, DisplaySector>(snapshot.Sectors.Count);
            for (var i = 0; i < snapshot.Sectors.Count; i++)
            {
                byId[snapshot.Sectors[i].SectorId] = snapshot.Sectors[i];
            }

            AppendDuelLinkSegments(duel.PlayerLinks, byId, segments);
            AppendDuelLinkSegments(duel.EnemyLinks, byId, segments);
            return segments;
        }

        private List<HexFrontierGraphic.LineSegment> BuildDuelOwnershipNetworkSegments(GridSnapshot snapshot)
        {
            var segments = new List<HexFrontierGraphic.LineSegment>();
            if (snapshot.Sectors == null || snapshot.Sectors.Count == 0) return segments;

            var byCoord = new Dictionary<HexCoord, DisplaySector>(snapshot.Sectors.Count);
            var processedEdges = new HashSet<long>();
            for (var i = 0; i < snapshot.Sectors.Count; i++)
            {
                byCoord[snapshot.Sectors[i].Coord] = snapshot.Sectors[i];
            }

            for (var i = 0; i < snapshot.Sectors.Count; i++)
            {
                var sector = snapshot.Sectors[i];
                if (!sector.IsDuelSector || sector.OwnerPlayerId <= 0) continue;

                for (var neighborIndex = 0; neighborIndex < HexGridMath.NeighborDirections.Length; neighborIndex++)
                {
                    var neighborCoord = new HexCoord(
                        sector.Coord.Q + HexGridMath.NeighborDirections[neighborIndex].Q,
                        sector.Coord.R + HexGridMath.NeighborDirections[neighborIndex].R);
                    if (!byCoord.TryGetValue(neighborCoord, out var neighbor)) continue;
                    if (neighbor.OwnerPlayerId != sector.OwnerPlayerId) continue;

                    var edgeKey = GetEdgeKey(sector.SectorId, neighbor.SectorId);
                    if (!processedEdges.Add(edgeKey)) continue;

                    var start = HexGridMath.AxialToLocalPosition(sector.Coord, _hexRadius);
                    var end = HexGridMath.AxialToLocalPosition(neighbor.Coord, _hexRadius);
                    segments.Add(new HexFrontierGraphic.LineSegment(
                        start,
                        end,
                        HexGridPalette.GetNetworkColor(sector.OwnerPlayerId, _ownershipNetworkAlpha)));
                }
            }

            return segments;
        }

        private void AppendDuelLinkSegments(
            IReadOnlyList<DuelLinkState> links,
            Dictionary<int, DisplaySector> byId,
            List<HexFrontierGraphic.LineSegment> segments)
        {
            if (links == null) return;

            for (var i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (!link.IsActive) continue;
                if (!byId.TryGetValue(link.SourceId, out var source)) continue;
                if (!byId.TryGetValue(link.TargetId, out var target)) continue;

                var start = HexGridMath.AxialToLocalPosition(source.Coord, _hexRadius);
                var end = HexGridMath.AxialToLocalPosition(target.Coord, _hexRadius);
                var color = HexGridPalette.GetFrontlineColor(link.OwnerPlayerId, Color.white);
                segments.Add(new HexFrontierGraphic.LineSegment(start, end, color));
            }
        }

        private List<HexFrontierGraphic.LineSegment> BuildDuelControlRingSegments(GridSnapshot snapshot)
        {
            var segments = new List<HexFrontierGraphic.LineSegment>();
            var visualRadius = _hexRadius * _cellFill * DuelStyle.ControlRingScale;

            for (var i = 0; i < snapshot.Sectors.Count; i++)
            {
                var sector = snapshot.Sectors[i];
                if (!sector.IsDuelSector) continue;

                var progress = Mathf.Clamp01(Mathf.Abs(sector.ControlValue) / 100f);
                if (progress <= 0.001f) continue;

                var color = sector.ControlValue >= 0f
                    ? HexGridPalette.GetFrontlineColor(1, Color.white)
                    : HexGridPalette.GetFrontlineColor(2, Color.white);

                var corners = HexGridMath.GetHexCorners(HexGridMath.AxialToLocalPosition(sector.Coord, _hexRadius), visualRadius);
                var edgeBudget = progress * 6f;
                for (var edgeIndex = 0; edgeIndex < 6; edgeIndex++)
                {
                    var remaining = edgeBudget - edgeIndex;
                    if (remaining <= 0f) break;

                    var start = corners[edgeIndex];
                    var end = corners[(edgeIndex + 1) % 6];
                    if (remaining < 1f)
                    {
                        end = Vector2.Lerp(start, end, remaining);
                    }

                    segments.Add(new HexFrontierGraphic.LineSegment(start, end, color));
                }
            }

            return segments;
        }

        private static bool ContainsDuelSectors(GridSnapshot snapshot)
        {
            if (snapshot.Sectors == null) return false;
            for (var i = 0; i < snapshot.Sectors.Count; i++)
            {
                if (snapshot.Sectors[i].IsDuelSector)
                {
                    return true;
                }
            }

            return false;
        }

        private float GetPulsedGlowAlpha()
        {
            var baseAlpha = Mathf.Clamp01(_localFrontlineGlowAlpha);
            if (_localFrontlineGlowPulseSpeed <= 0f || _localFrontlineGlowPulseDepth <= 0f)
            {
                return baseAlpha;
            }

            var time = Application.isPlaying ? Time.unscaledTime : 0f;
            var wave = (Mathf.Sin(time * Mathf.PI * 2f * _localFrontlineGlowPulseSpeed) + 1f) * 0.5f;
            var minFactor = Mathf.Clamp01(1f - _localFrontlineGlowPulseDepth);
            var factor = Mathf.Lerp(minFactor, 1f, wave);
            return Mathf.Clamp01(baseAlpha * factor);
        }

        private void ApplyConnectivityMarkers(MapConfig config, List<DisplaySector> sectors)
        {
            if (sectors == null || sectors.Count == 0) return;

            var sectorsById = new Dictionary<int, DisplaySector>(sectors.Count);
            for (var i = 0; i < sectors.Count; i++)
            {
                var sector = sectors[i];
                sector.IsAttackFrontier = false;
                sector.IsDisconnectedOwned = false;
                sectorsById[sector.SectorId] = sector;
                sectors[i] = sector;
            }

            if (config == null) return;

            var neighborsBySectorId = BuildNeighborMap(config);
            if (neighborsBySectorId.Count == 0) return;

            var localPlayerId = config.LocalPlayerId;
            var connectedOwnedSectorIds = BuildConnectedOwnedSectorSet(sectorsById, neighborsBySectorId, config, localPlayerId);
            if (connectedOwnedSectorIds.Count == 0) return;

            var attackableTargetIds = new HashSet<int>();
            foreach (var ownedSectorId in connectedOwnedSectorIds)
            {
                if (!neighborsBySectorId.TryGetValue(ownedSectorId, out var neighbors) || neighbors == null) continue;
                foreach (var neighborId in neighbors)
                {
                    if (!sectorsById.TryGetValue(neighborId, out var neighbor)) continue;
                    if (neighbor.OwnerPlayerId == localPlayerId) continue;
                    if (neighbor.IsHome) continue;
                    attackableTargetIds.Add(neighborId);
                }
            }

            for (var i = 0; i < sectors.Count; i++)
            {
                var sector = sectors[i];
                sector.IsAttackFrontier = attackableTargetIds.Contains(sector.SectorId);
                sector.IsDisconnectedOwned = sector.OwnerPlayerId == localPlayerId &&
                    !sector.IsHome &&
                    !connectedOwnedSectorIds.Contains(sector.SectorId);
                sectors[i] = sector;
            }
        }

        private static Dictionary<int, HashSet<int>> BuildNeighborMap(MapConfig config)
        {
            var map = new Dictionary<int, HashSet<int>>();
            if (config?.SectorDefinitions != null)
            {
                for (var i = 0; i < config.SectorDefinitions.Length; i++)
                {
                    var def = config.SectorDefinitions[i];
                    if (def == null) continue;
                    if (!map.ContainsKey(def.SectorId))
                    {
                        map.Add(def.SectorId, new HashSet<int>());
                    }
                }
            }

            if (config?.Adjacency == null) return map;
            for (var i = 0; i < config.Adjacency.Length; i++)
            {
                var edge = config.Adjacency[i];
                if (!map.TryGetValue(edge.A, out var aSet))
                {
                    aSet = new HashSet<int>();
                    map.Add(edge.A, aSet);
                }

                if (!map.TryGetValue(edge.B, out var bSet))
                {
                    bSet = new HashSet<int>();
                    map.Add(edge.B, bSet);
                }

                aSet.Add(edge.B);
                bSet.Add(edge.A);
            }

            return map;
        }

        private static HashSet<int> BuildConnectedOwnedSectorSet(
            Dictionary<int, DisplaySector> sectorsById,
            Dictionary<int, HashSet<int>> neighborsBySectorId,
            MapConfig config,
            int ownerPlayerId)
        {
            var connected = new HashSet<int>();
            if (ownerPlayerId <= 0 || config == null) return connected;

            var queue = new Queue<int>();
            var homeSectorIds = config.GetEffectiveHomeSectorIds();
            for (var i = 0; i < homeSectorIds.Length; i++)
            {
                var homeSectorId = homeSectorIds[i];
                if (config.GetHomeOwnerPlayerId(homeSectorId) != ownerPlayerId) continue;
                if (!sectorsById.TryGetValue(homeSectorId, out var homeSector)) continue;
                if (homeSector.OwnerPlayerId != ownerPlayerId) continue;
                if (!connected.Add(homeSectorId)) continue;
                queue.Enqueue(homeSectorId);
            }

            while (queue.Count > 0)
            {
                var sectorId = queue.Dequeue();
                if (!neighborsBySectorId.TryGetValue(sectorId, out var neighbors) || neighbors == null) continue;
                foreach (var neighborId in neighbors)
                {
                    if (connected.Contains(neighborId)) continue;
                    if (!sectorsById.TryGetValue(neighborId, out var neighbor)) continue;
                    if (neighbor.OwnerPlayerId != ownerPlayerId) continue;
                    connected.Add(neighborId);
                    queue.Enqueue(neighborId);
                }
            }

            return connected;
        }

        private HashSet<int> BuildHomeConnectedSectorSet(
            GridSnapshot snapshot,
            Dictionary<HexCoord, DisplaySector> byCoord,
            Dictionary<int, DisplaySector> byId,
            MapConfig config)
        {
            var result = new HashSet<int>();
            var queue = new Queue<DisplaySector>();

            var homeSectorIds = config.GetEffectiveHomeSectorIds();
            for (var i = 0; i < homeSectorIds.Length; i++)
            {
                if (!byId.TryGetValue(homeSectorIds[i], out var home)) continue;
                if (home.OwnerPlayerId <= 0) continue;
                if (!result.Add(home.SectorId)) continue;
                queue.Enqueue(home);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (var i = 0; i < HexGridMath.NeighborDirections.Length; i++)
                {
                    var neighborCoord = new HexCoord(
                        current.Coord.Q + HexGridMath.NeighborDirections[i].Q,
                        current.Coord.R + HexGridMath.NeighborDirections[i].R);
                    if (!byCoord.TryGetValue(neighborCoord, out var neighbor)) continue;
                    if (neighbor.OwnerPlayerId != current.OwnerPlayerId) continue;
                    if (!result.Add(neighbor.SectorId)) continue;
                    queue.Enqueue(neighbor);
                }
            }

            return result;
        }

        private MapConfig GetActiveMapConfig()
        {
            var loop = _context?.Loop;
            if (loop?.MapConfig != null) return loop.MapConfig;
            if (_context?.Bootstrapper?.MapConfig != null) return _context.Bootstrapper.MapConfig;
            return null;
        }

        private static long GetEdgeKey(int a, int b)
        {
            if (a > b)
            {
                (a, b) = (b, a);
            }

            return ((long)a << 32) | (uint)b;
        }

        private TMP_Text CreatePanelTitle(RectTransform parent)
        {
            var go = CreatePanelTextObject(parent, "title");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(16f, -40f);
            rect.offsetMax = new Vector2(-16f, -12f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.Left;
            text.color = new Color32(244, 239, 224, 255);
            text.text = "Sector";
            return text;
        }

        private TMP_Text CreatePanelLine(RectTransform parent, string name, Vector2 anchoredPosition, float height = 28f)
        {
            var go = CreatePanelTextObject(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(-32f, height);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = 16f;
            text.alignment = TextAlignmentOptions.Left;
            text.color = new Color32(221, 227, 235, 255);
            return text;
        }

        private Button CreatePanelActionButton(RectTransform parent)
        {
            var buttonGo = new GameObject("action_button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, worldPositionStays: false);
            HideEditorObject(buttonGo);

            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(16f, 16f);
            rect.offsetMax = new Vector2(-16f, 56f);

            var image = buttonGo.GetComponent<Image>();
            image.color = new Color32(77, 123, 191, 255);

            var button = buttonGo.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.95f);
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 0.9f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            button.colors = colors;

            var label = CreatePanelTextObject(rect, "button_label").GetComponent<TextMeshProUGUI>();
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.fontSize = 16f;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "Attack";
            label.color = Color.white;
            label.raycastTarget = false;

            return button;
        }

        private Button CreatePanelSecondaryButton(RectTransform parent, string name, Vector2 offset, string labelText)
        {
            var buttonGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, worldPositionStays: false);
            HideEditorObject(buttonGo);

            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.offsetMin = new Vector2(offset.x, offset.y);
            rect.offsetMax = new Vector2(-16f, offset.y + 36f);

            var image = buttonGo.GetComponent<Image>();
            image.color = new Color32(57, 72, 104, 255);

            var button = buttonGo.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.96f);
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 0.9f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            button.colors = colors;

            var label = CreatePanelTextObject(rect, $"{name}_label").GetComponent<TextMeshProUGUI>();
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.fontSize = 14f;
            label.alignment = TextAlignmentOptions.Center;
            label.text = labelText;
            label.color = Color.white;
            label.raycastTarget = false;

            return button;
        }

        private TMP_Text CreatePanelHint(RectTransform parent)
        {
            var go = CreatePanelTextObject(parent, "hint");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(16f, 60f);
            rect.offsetMax = new Vector2(-16f, 102f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = 12f;
            text.alignment = TextAlignmentOptions.Left;
            text.color = new Color32(168, 177, 188, 255);
            text.text = "Select a sector to inspect.";
            return text;
        }

        private TMP_Text CreateHudText(RectTransform parent, string name, Vector2 anchoredPosition, float fontSize, bool centered = false)
        {
            var go = CreatePanelTextObject(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(centered ? 0.5f : 0f, 1f);
            rect.anchorMax = new Vector2(centered ? 0.5f : 1f, 1f);
            rect.pivot = new Vector2(centered ? 0.5f : 0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = centered ? new Vector2(320f, 28f) : new Vector2(-32f, 28f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.alignment = centered ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
            text.color = new Color32(232, 238, 244, 255);
            return text;
        }

        private TMP_Text CreateHudTable(RectTransform parent, string name, Vector2 anchoredPosition, float height)
        {
            var text = CreateHudText(parent, name, anchoredPosition, 13f);
            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(-32f, height);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.lineSpacing = 2f;
            return text;
        }

        private TMP_Text CreateHudTooltip(RectTransform parent)
        {
            var go = CreatePanelTextObject(parent, "power_tooltip");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(16f, 16f);
            rect.offsetMax = new Vector2(-16f, 82f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = 13f;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Left;
            text.color = new Color32(201, 210, 224, 255);
            text.text = "PvpPower = prestige + permanent upgrades + sectors + small production bonus.\nSubscription boosts production, but does not directly raise PvpPower.";
            text.gameObject.SetActive(false);
            return text;
        }

        private GameObject CreatePanelTextObject(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, worldPositionStays: false);
            HideEditorObject(go);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 24f;
            text.raycastTarget = false;
            return go;
        }

        private TMP_Text CreateLabel(RectTransform parent)
        {
            var go = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, worldPositionStays: false);
            HideEditorObject(go);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(84f, 34f);
            rect.anchoredPosition = new Vector2(0f, 4f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 13f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = 13f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color32(245, 244, 239, 255);
            text.raycastTarget = false;
            return text;
        }

        private TMP_Text CreateInfoLabel(RectTransform parent)
        {
            var go = new GameObject("info", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, worldPositionStays: false);
            HideEditorObject(go);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(84f, 24f);
            rect.anchoredPosition = new Vector2(0f, 8f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 10f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 7f;
            text.fontSizeMax = 10f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color32(219, 226, 233, 220);
            text.raycastTarget = false;
            return text;
        }

        private TMP_Text CreateMarkerLabel(RectTransform parent)
        {
            var go = new GameObject("marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, worldPositionStays: false);
            HideEditorObject(go);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(72f, 18f);
            rect.anchoredPosition = new Vector2(0f, -8f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 10f;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color32(241, 246, 240, 245);
            text.raycastTarget = false;
            text.text = string.Empty;
            go.SetActive(false);
            return text;
        }

        private TMP_Text CreateHomeBadge(RectTransform parent)
        {
            var go = new GameObject("home_badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, worldPositionStays: false);
            HideEditorObject(go);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(56f, 18f);
            rect.anchoredPosition = new Vector2(0f, -8f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = "HOME";
            text.fontSize = 11f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color32(255, 245, 196, 255);
            text.raycastTarget = false;
            return text;
        }

        private void SelectCell(HexCellView cell)
        {
            if (_context?.HexHackDuel != null)
            {
                HandleDuelCellTap(cell);
                return;
            }

            GetOrCreateDetailPanel();

            if (_selected == cell)
            {
                ClearSelection();
                return;
            }

            if (_selected != null)
            {
                _selected.SetSelected(false);
            }

            _selected = cell;
            _selected.SetSelected(true);
            UpdateDetailPanel(cell);
        }

        private void ClearSelection()
        {
            if (_selected != null)
            {
                _selected.SetSelected(false);
            }

            _selected = null;
            if (_detailPanel != null)
            {
                if (_context?.HexHackDuel != null && (!_context.HexHackDuel.IsMatchActive || _context.HexHackDuel.IsMatchFinished))
                {
                    UpdateIdleDuelPanel();
                }
                else
                {
                    _detailPanel.Hide();
                }
            }
        }

        private void UpdateDetailPanel(HexCellView cell)
        {
            var panel = GetOrCreateDetailPanel();
            panel.ShowSector(BuildDetailState(cell.CurrentSector));
            PositionDetailPanel(panel.GetComponent<RectTransform>(), cell);
        }

        private void UpdateIdleDuelPanel()
        {
            var duel = _context?.HexHackDuel;
            if (duel == null || (duel.IsMatchActive && !duel.IsMatchFinished)) return;

            var panel = GetOrCreateDetailPanel();
            panel.ShowStatusCard(
                title: "Hex Hack Duel",
                owner: "Mode: 1vAI real-time",
                bonus: "Board: 2-3-2 ring",
                stability: "CORE: node D",
                status: duel.ResultSummary,
                hint: duel.IsMatchActive
                    ? BuildDuelActiveHintText(duel)
                    : $"Press PLAY to start a {duel.MatchDurationSeconds:0.#} second test duel. You start on C, AI starts on E.",
                actionLabel: duel.IsMatchFinished ? "PLAY AGAIN" : "PLAY",
                actionEnabled: !duel.IsMatchActive || duel.IsMatchFinished,
                strategyLabel: $"Difficulty: {duel.Difficulty}",
                strategyEnabled: !duel.IsMatchActive || duel.IsMatchFinished,
                previewText: duel.IsMatchActive ? string.Empty : "TEST MATCH");
            PositionIdleDuelPanel(panel.GetComponent<RectTransform>());
        }

        private void PositionIdleDuelPanel(RectTransform panelRect)
        {
            if (panelRect == null) return;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
        }

        private void UpdateDuelViewVisibility()
        {
            var duel = _context?.HexHackDuel;
            var showMatchView = duel != null && duel.IsMatchActive && !duel.IsMatchFinished;
            if (_generatedRoot != null)
            {
                _generatedRoot.gameObject.SetActive(showMatchView);
            }

            if (_hud != null)
            {
                _hud.gameObject.SetActive(showMatchView);
            }

            if (_detailPanel != null && !showMatchView)
            {
                _detailPanel.gameObject.SetActive(true);
            }
        }

        private void HandleDuelCellTap(HexCellView cell)
        {
            if (cell == null) return;

            var duel = _context?.HexHackDuel;
            if (duel == null) return;

            if (!duel.IsMatchActive)
            {
                GetOrCreateToast().Show("Press PLAY to start.");
                return;
            }

            var sector = cell.CurrentSector;
            if (duel.AttackMode == HexHackDuelAttackMode.ManualSwipeSources)
            {
                HandleManualDuelCellTap(sector, duel);
                return;
            }

            var isOperationalPlayerNode =
                HexHackDuelService.IsPlayerOwner(sector.OwnerPlayerId) &&
                sector.ControlValue > duel.AttackControlThreshold;
            if (isOperationalPlayerNode)
            {
                GetOrCreateToast().Show("Tap a neutral, enemy, or damaged allied node.");
                return;
            }

            var result = duel.AutoAssignPlayerLinksToTarget(sector.SectorId);
            GetOrCreateToast().Show(result.Message);
            RefreshFromSource(forceRebuild: false);
        }

        private void HandleManualDuelCellTap(DisplaySector sector, HexHackDuelService duel)
        {
            if (HexHackDuelService.IsPlayerOwner(sector.OwnerPlayerId))
            {
                if (duel.HasPlayerLinkFromSource(sector.SectorId))
                {
                    var cancel = duel.CancelPlayerLinkFromSource(sector.SectorId);
                    GetOrCreateToast().Show(cancel.Message);
                    RefreshFromSource(forceRebuild: false);
                    return;
                }

                GetOrCreateToast().Show($"Swipe from a player node above {duel.AttackControlThreshold:0} control into an adjacent target.");
                return;
            }

            GetOrCreateToast().Show("Swipe from a player node to create the link.");
        }

        private void HandleDuelCellDragBegin(HexCellView cell, PointerEventData eventData)
        {
            if (cell == null) return;

            var duel = _context?.HexHackDuel;
            if (duel == null || !duel.IsMatchActive || duel.AttackMode != HexHackDuelAttackMode.ManualSwipeSources) return;

            var sector = cell.CurrentSector;
            var isValidSource =
                HexHackDuelService.IsPlayerOwner(sector.OwnerPlayerId) &&
                sector.ControlValue > duel.AttackControlThreshold;
            if (!isValidSource)
            {
                GetOrCreateToast().Show($"Swipe must start from a player node above {duel.AttackControlThreshold:0} control.");
            }
        }

        private void HandleDuelCellDragEnd(HexCellView sourceCell, PointerEventData eventData)
        {
            if (sourceCell == null || eventData == null) return;

            var duel = _context?.HexHackDuel;
            if (duel == null || !duel.IsMatchActive || duel.AttackMode != HexHackDuelAttackMode.ManualSwipeSources) return;

            var sourceSector = sourceCell.CurrentSector;
            var isValidSource =
                HexHackDuelService.IsPlayerOwner(sourceSector.OwnerPlayerId) &&
                sourceSector.ControlValue > duel.AttackControlThreshold;
            if (!isValidSource) return;

            var targetCell = FindCellAtScreenPosition(eventData.position, eventData.pressEventCamera);
            if (targetCell == null || targetCell == sourceCell) return;

            var result = duel.TryAssignPlayerLinkFromSource(sourceSector.SectorId, targetCell.SectorId);
            GetOrCreateToast().Show(result.Message);
            RefreshFromSource(forceRebuild: false);
        }

        private HexCellView FindCellAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
        {
            for (var i = 0; i < _cells.Count; i++)
            {
                if (_cells[i].transform is not RectTransform rect) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera))
                {
                    return _cells[i];
                }
            }

            return null;
        }

        private void PositionDetailPanel(RectTransform panelRect, HexCellView cell)
        {
            if (panelRect == null || cell == null) return;

            var parentRect = transform as RectTransform;
            var cellRect = cell.transform as RectTransform;
            if (parentRect == null || cellRect == null) return;

            var worldCenter = cellRect.TransformPoint(cellRect.rect.center);
            var localCenter = (Vector2)parentRect.InverseTransformPoint(worldCenter);
            var panelSize = panelRect.rect.size;
            var parentBounds = parentRect.rect;
            var halfWidth = panelSize.x * 0.5f;
            var halfHeight = panelSize.y * 0.5f;
            var rightBias = localCenter.x <= 0f;
            var horizontalOffset = _detailPanelHorizontalOffset * _detailPanelDistanceMultiplier;
            var verticalOffset = _detailPanelVerticalOffset * _detailPanelDistanceMultiplier;

            var candidates = new[]
            {
                localCenter + new Vector2(rightBias ? horizontalOffset : -horizontalOffset, verticalOffset),
                localCenter + new Vector2(rightBias ? -horizontalOffset : horizontalOffset, verticalOffset),
                localCenter + new Vector2(rightBias ? horizontalOffset : -horizontalOffset, verticalOffset * 0.35f),
                localCenter + new Vector2(rightBias ? -horizontalOffset : horizontalOffset, verticalOffset * 0.35f),
                localCenter + new Vector2(rightBias ? horizontalOffset : -horizontalOffset, -verticalOffset * 0.55f),
                localCenter + new Vector2(rightBias ? -horizontalOffset : horizontalOffset, -verticalOffset * 0.55f),
                localCenter + new Vector2(0f, verticalOffset),
                localCenter + new Vector2(0f, -verticalOffset * 0.7f),
            };

            var bestPosition = ClampDetailPanelPosition(candidates[0], parentBounds, halfWidth, halfHeight);
            var bestScore = ScoreDetailPanelPosition(candidates[0], bestPosition, localCenter);

            for (var i = 1; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                var clamped = ClampDetailPanelPosition(candidate, parentBounds, halfWidth, halfHeight);
                var score = ScoreDetailPanelPosition(candidate, clamped, localCenter);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPosition = clamped;
                }
            }

            panelRect.anchoredPosition = bestPosition;
        }

        private Vector2 ClampDetailPanelPosition(Vector2 desired, Rect parentBounds, float halfWidth, float halfHeight)
        {
            return new Vector2(
                Mathf.Clamp(
                    desired.x,
                    parentBounds.xMin + halfWidth + _detailPanelEdgePadding,
                    parentBounds.xMax - halfWidth - _detailPanelEdgePadding),
                Mathf.Clamp(
                    desired.y,
                    parentBounds.yMin + halfHeight + _detailPanelEdgePadding,
                    parentBounds.yMax - halfHeight - _detailPanelEdgePadding));
        }

        private static float ScoreDetailPanelPosition(Vector2 desired, Vector2 clamped, Vector2 origin)
        {
            var clampPenalty = (desired - clamped).sqrMagnitude * 10f;
            var distancePenalty = (clamped - desired).magnitude;
            var directionPenalty = clamped.y < origin.y ? 60f : 0f;
            return clampPenalty + distancePenalty + directionPenalty;
        }

        private void UpdateHud(GridSnapshot snapshot)
        {
            var hud = GetOrCreateHud();
            var loop = _context?.Loop;
            var duel = _context?.HexHackDuel;

            if (loop == null)
            {
                hud.ShowPreview(snapshot.Count, _radius, _powerInfoVisible);
                return;
            }

            if (duel != null)
            {
                var countText = duel.IsMatchActive
                    ? $"Links: {CountActiveLinks(duel.PlayerLinks)}"
                    : "Press PLAY";
                var matchText = duel.IsMatchActive
                    ? FormatClockDuration(Mathf.CeilToInt(duel.RemainingSeconds))
                    : "00:30 test duel";
                var duelPowerText = $"YOU {duel.PlayerPower:0.#}  |  AI {duel.EnemyPower:0.#}";
                var table = BuildDuelScoreTable(duel);
                var duelAttacks = loop.PvpAttacks;
                var duelRemaining = duelAttacks != null ? Mathf.Clamp(loop.State.PvpAttacksRemaining, 0, duelAttacks.MaxAttacks) : 0;
                var externalCountText = duelAttacks != null ? $"{duelRemaining}/{Mathf.Max(duelAttacks.MaxAttacks, duelRemaining)}" : string.Empty;
                var externalRegenText = string.Empty;
                var externalPowerText = loop.Snapshot != null ? $"{loop.Snapshot.BuildSnapshot().PvpPower:0.##}" : string.Empty;
                if (duelAttacks != null && duelRemaining < duelAttacks.MaxAttacks)
                {
                    var duelNextRegenAt = loop.State.NextPvpAttackRegenAtUnixSeconds;
                    if (duelNextRegenAt > loop.NowUnixSeconds)
                    {
                        externalRegenText = FormatClockDuration(duelNextRegenAt - loop.NowUnixSeconds);
                    }
                }

                UpdateExternalAttackHud(externalCountText, externalRegenText, externalPowerText);
                hud.SetPowerTooltipContent("Hex Hack Duel", "Player push and AI push scale by the clamped power ratio. CORE adds +30% push from node D.");
                hud.ShowState(
                    _externalAttacksTexts.Count > 0 ? string.Empty : countText,
                    matchText,
                    duelPowerText,
                    duel.IsMatchFinished ? duel.ResultSummary : "Hex Hack Duel",
                    table,
                    _powerInfoVisible);
                return;
            }

            var attacks = loop.PvpAttacks;
            var snapshotService = loop.Snapshot;

            var now = loop.NowUnixSeconds;
            var remaining = attacks != null ? Mathf.Clamp(loop.State.PvpAttacksRemaining, 0, attacks.MaxAttacks) : 0;
            var max = attacks != null ? attacks.MaxAttacks : 0;
            var nextRegenAt = attacks != null ? loop.State.NextPvpAttackRegenAtUnixSeconds : 0;
            var attackCountText = $"{remaining}/{Mathf.Max(max, remaining)}";
            var regenTimerText = max > 0 && remaining < max && nextRegenAt > now
                ? FormatClockDuration(nextRegenAt - now)
                : string.Empty;

            var matchEndsText = loop.Map != null
                ? FormatClockDuration(Math.Max(0, loop.Map.GetCurrentSeasonEndsAtUnixSeconds(now) - now))
                : "--:--:--";

            var power = snapshotService != null ? snapshotService.BuildSnapshot().PvpPower : 0;
            var powerText = $"{power:0.##}";
            var powerTooltipBody = snapshotService != null
                ? BuildPowerTooltipBody(snapshotService.BuildPowerBreakdown())
                : string.Empty;
            var liveScoreTable = GetCachedLiveScoreTable(loop);

            UpdateExternalAttackHud(attackCountText, regenTimerText, powerText);
            hud.SetPowerTooltipContent("PvP Power", powerTooltipBody);
            hud.ShowState(_externalAttacksTexts.Count > 0 ? string.Empty : $"Attacks: {attackCountText}",
                matchEndsText,
                powerText,
                "Live score",
                liveScoreTable,
                _powerInfoVisible);
        }

        private void UpdateExternalAttackHud(string countText, string regenText, string powerText)
        {
            EnsureExternalHudRefs();

            for (var i = 0; i < _externalAttacksTexts.Count; i++)
            {
                if (_externalAttacksTexts[i] != null)
                {
                    _externalAttacksTexts[i].text = countText;
                }
            }

            for (var i = 0; i < _externalRegenTexts.Count; i++)
            {
                if (_externalRegenTexts[i] != null)
                {
                    _externalRegenTexts[i].text = string.IsNullOrEmpty(regenText) ? string.Empty : regenText;
                }
            }

            for (var i = 0; i < _externalPvpPowerTexts.Count; i++)
            {
                if (_externalPvpPowerTexts[i] != null)
                {
                    _externalPvpPowerTexts[i].text = powerText;
                }
            }
        }

        private void EnsureExternalHudRefs()
        {
            if (_externalAttacksTexts.Count > 0 && _externalRegenTexts.Count > 0 && _externalPvpPowerTexts.Count > 0) return;
            if (!CanQuerySceneHierarchy()) return;

            var attacksBarRoot = FindNamedChildInScene("res_bar");
            if (_externalAttacksTexts.Count == 0 && attacksBarRoot != null)
            {
                var energyBar = FindNamedChildRecursive(attacksBarRoot, "ResourceBar_Energy");
                CollectTextComponents(
                    energyBar,
                    _externalAttacksTexts,
                    text => text.name == "Text_Energy" || text.name == "Text (TMP)");
            }

            var timerRoot = FindNamedChildInScene("res_bar_2");
            if (_externalRegenTexts.Count == 0 && timerRoot != null)
            {
                var regenRoot = FindNamedChildRecursive(timerRoot, "energy_regen");
                CollectTextComponents(
                    regenRoot,
                    _externalRegenTexts,
                    text => text.name == "Text (TMP)");
            }

            if (_externalPvpPowerTexts.Count == 0)
            {
                var powerRoot = FindNamedChildInScene("PvP_power");
                CollectTextComponents(
                    powerRoot,
                    _externalPvpPowerTexts,
                    text => text.name == "pvp_power_text");
            }
        }

        private Transform FindNamedChildInScene(string name)
        {
            if (!CanQuerySceneHierarchy()) return null;

            var roots = gameObject.scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var found = FindNamedChildRecursive(roots[i].transform, name);
                if (found != null) return found;
            }

            return null;
        }

        private bool CanQuerySceneHierarchy()
        {
            var scene = gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static Transform FindNamedChildRecursive(Transform root, string name)
        {
            if (root.name == name) return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindNamedChildRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private static void CollectTextComponents(Transform root, List<TMP_Text> target, Predicate<TMP_Text> predicate)
        {
            if (root == null || target == null) return;

            var matches = new List<TMP_Text>();
            CollectTextComponentsRecursive(root, matches, predicate);
            if (matches.Count == 0) return;

            var hasActiveMatch = false;
            for (var i = 0; i < matches.Count; i++)
            {
                if (matches[i] != null && matches[i].gameObject.activeInHierarchy)
                {
                    hasActiveMatch = true;
                    break;
                }
            }

            for (var i = 0; i < matches.Count; i++)
            {
                var text = matches[i];
                if (text == null) continue;
                if (hasActiveMatch && !text.gameObject.activeInHierarchy) continue;
                target.Add(text);
            }
        }

        private static void CollectTextComponentsRecursive(Transform root, List<TMP_Text> target, Predicate<TMP_Text> predicate)
        {
            if (root == null) return;

            if (root.TryGetComponent<TMP_Text>(out var text) && (predicate == null || predicate(text)))
            {
                target.Add(text);
            }

            for (var i = 0; i < root.childCount; i++)
            {
                CollectTextComponentsRecursive(root.GetChild(i), target, predicate);
            }
        }

        private SectorDetailState BuildDetailState(DisplaySector sector)
        {
            var loop = _context?.Loop;
            var duel = _context?.HexHackDuel;
            if (duel != null && sector.IsDuelSector)
            {
                return BuildDuelDetailState(sector, duel);
            }

            if (loop != null)
            {
                var detailStamp = ComputeDetailStateStamp(loop, sector, _selectedStrategy);
                if (_cachedDetailSectorId == sector.SectorId &&
                    _cachedDetailStrategy == _selectedStrategy &&
                    _cachedDetailStamp == detailStamp)
                {
                    return _cachedDetailState;
                }
            }

            var ownerText = BuildOwnerText(sector);
            var bonusText = $"Bonus: +{sector.ProductionBonusPercent:0.#}% production";
            var stabilityText = $"Stability: {sector.Stability:0.#}%";
            var positionText = $"Coord: {sector.Coord.Q}, {sector.Coord.R}  |  Id: {sector.SectorId}";
            var previewText = "BREACH CHANCE: -";
            var statusText = "Sector ready.";
            var hintText = "Click Strategy to cycle Aggressive / Stable / Risky.";
            var actionLabel = $"ATTACK ({_selectedStrategy})";
            var canAttack = false;

            if (loop?.PvpCombat != null)
            {
                var now = loop.NowUnixSeconds;
                var evaluation = loop.PvpCombat.EvaluateAttack(sector.SectorId, now);
                var preview = loop.PvpCombat.GetAttackPreview(sector.SectorId, _selectedStrategy, now);

                if (evaluation.CanAttack)
                {
                    previewText = $"BREACH CHANCE: {FormatPreviewChance(preview)}";
                    statusText = "Attack available.";
                    canAttack = true;
                }
                else if (ShouldShowBlockedPreview(evaluation.BlockReason))
                {
                    previewText = $"BREACH CHANCE: {FormatPreviewChance(preview)}";
                    statusText = GetBlockReasonText(evaluation);
                }
                else
                {
                    previewText = "BREACH CHANCE: BLOCKED";
                    statusText = GetBlockReasonText(evaluation);
                }

                if (evaluation.BlockReason == PvpAttackBlockReason.CooldownActive && evaluation.CooldownRemainingSeconds > 0)
                {
                    stabilityText += $"  |  Cooldown {FormatDuration(evaluation.CooldownRemainingSeconds)}";
                }
            }
            else if (sector.IsHome)
            {
                actionLabel = "Home Sector";
                statusText = "This is a protected home sector.";
            }

            var detailState = new SectorDetailState
            {
                Sector = sector,
                OwnerText = ownerText,
                BonusText = bonusText,
                StabilityText = stabilityText,
                PreviewText = previewText,
                PositionText = positionText,
                StatusText = statusText,
                HintText = hintText,
                ActionLabel = actionLabel,
                StrategyLabel = $"Strategy: {_selectedStrategy}",
                CanAttack = canAttack,
                CanCycleStrategy = true
            };

            if (loop != null)
            {
                _cachedDetailSectorId = sector.SectorId;
                _cachedDetailStrategy = _selectedStrategy;
                _cachedDetailStamp = ComputeDetailStateStamp(loop, sector, _selectedStrategy);
                _cachedDetailState = detailState;
            }

            return detailState;
        }

        private SectorDetailState BuildDuelDetailState(DisplaySector sector, HexHackDuelService duel)
        {
            var ownerText = BuildDuelOwnerText(sector);
            var bonusText = sector.IsCoreSector ? "Core: +30% push from this node." : "Hack node.";
            var stabilityText = $"Control: {FormatDuelControlLong(sector.ControlValue)}";
            var positionText = $"Node {sector.Label}  |  Coord {sector.Coord.Q},{sector.Coord.R}";
            var hintText = duel.IsMatchActive
                ? BuildDuelActiveHintText(duel)
                : $"Press PLAY to launch a {duel.MatchDurationSeconds:0.#} second test duel.";
            var statusText = duel.ResultSummary;
            var previewText = BuildDuelPreviewText(sector, duel);
            var actionLabel = duel.IsMatchActive ? "LINK" : "PLAY";
            var canAttack = true;
            var strategyLabel = $"Difficulty: {duel.Difficulty}";
            var strategyEnabled = !duel.IsMatchActive || duel.IsMatchFinished;

            if (duel.IsMatchFinished)
            {
                actionLabel = "PLAY AGAIN";
                canAttack = true;
            }

            return new SectorDetailState
            {
                Sector = sector,
                OwnerText = ownerText,
                BonusText = bonusText,
                StabilityText = stabilityText,
                PreviewText = previewText,
                PositionText = positionText,
                StatusText = statusText,
                HintText = hintText,
                ActionLabel = actionLabel,
                StrategyLabel = strategyLabel,
                CanAttack = canAttack,
                CanCycleStrategy = strategyEnabled
            };
        }

        private void OnCycleStrategy()
        {
            var duel = _context?.HexHackDuel;
            if (duel != null)
            {
                duel.CycleDifficulty();
                GetOrCreateToast().Show($"Difficulty set to {duel.Difficulty}.");

                RefreshFromSource(forceRebuild: false);
                if (_selected != null)
                {
                    UpdateDetailPanel(_selected);
                }

                return;
            }

            _selectedStrategy = _selectedStrategy switch
            {
                AttackStrategy.Aggressive => AttackStrategy.Stable,
                AttackStrategy.Stable => AttackStrategy.Risky,
                _ => AttackStrategy.Aggressive
            };

            if (_selected != null)
            {
                UpdateDetailPanel(_selected);
            }
        }

        private void OnAttackSelectedSector()
        {
            var duel = _context?.HexHackDuel;
            if (duel != null)
            {
                HandleDuelAction();
                return;
            }

            if (_selected == null) return;

            var loop = _context?.Loop;
            if (loop?.PvpCombat == null) return;

            var now = loop.NowUnixSeconds;
            var evaluation = loop.PvpCombat.EvaluateAttack(_selected.SectorId, now);
            if (!evaluation.CanAttack)
            {
                GetOrCreateToast().Show(GetBlockReasonText(evaluation));
                UpdateDetailPanel(_selected);
                return;
            }

            var result = loop.PvpCombat.AttackSector(_selected.SectorId, _selectedStrategy, now);
            GetOrCreateToast().Show(result.Win
                ? $"Victory on sector {_selected.SectorId}. +{result.Battle.LeaguePointsDelta} SP"
                : $"Defeat on sector {_selected.SectorId}. {result.Battle.LeaguePointsDelta} SP");

            RefreshFromSource(forceRebuild: false);
        }

        private void HandleDuelAction()
        {
            var duel = _context?.HexHackDuel;
            if (duel == null) return;

            if (!duel.IsMatchActive)
            {
                duel.StartMatch();
                GetOrCreateToast().Show("Hex Hack Duel started.");
                if (_detailPanel != null)
                {
                    _detailPanel.Hide();
                }
                if (_selected != null)
                {
                    _selected.SetSelected(false);
                    _selected = null;
                }
                RefreshFromSource(forceRebuild: false);
                return;
            }
        }

        private void ShowPowerInfoTooltip()
        {
            _powerInfoVisible = true;
            GetOrCreateHud().SetPowerInfoVisible(true);
        }

        private void HidePowerInfoTooltip()
        {
            _powerInfoVisible = false;
            GetOrCreateHud().SetPowerInfoVisible(_powerInfoVisible);
        }

        private static string GetBlockReasonText(PvpAttackEvaluation evaluation)
        {
            return evaluation.BlockReason switch
            {
                PvpAttackBlockReason.MapUninitialized => "Map is still initializing.",
                PvpAttackBlockReason.SeasonResetPending => "Season reset pending. Wait for map refresh.",
                PvpAttackBlockReason.SectorMissing => "Sector data missing.",
                PvpAttackBlockReason.HomeSector => "Home sectors are protected and cannot be captured.",
                PvpAttackBlockReason.AlreadyOwned => "You already control this sector.",
                PvpAttackBlockReason.MissingMapDefinitions => "Map definitions are incomplete.",
                PvpAttackBlockReason.NoAdjacentOwnedSector => "You can only attack adjacent frontier hexes.",
                PvpAttackBlockReason.CooldownActive => $"Sector cooldown active for {FormatDuration(evaluation.CooldownRemainingSeconds)}.",
                PvpAttackBlockReason.NoAttackCharges => "No attack tokens remaining.",
                _ => "Attack unavailable."
            };
        }

        private static bool ShouldShowBlockedPreview(PvpAttackBlockReason blockReason)
        {
            return blockReason == PvpAttackBlockReason.NoAdjacentOwnedSector ||
                   blockReason == PvpAttackBlockReason.NoAttackCharges;
        }

        private static string FormatDuration(long seconds)
        {
            if (seconds <= 0) return "0s";

            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1d) return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
            if (ts.TotalMinutes >= 1d) return $"{(int)ts.TotalMinutes}m {ts.Seconds:D2}s";
            return $"{ts.Seconds}s";
        }

        private static string FormatClockDuration(long seconds)
        {
            if (seconds <= 0) return "0:00:00";

            var ts = TimeSpan.FromSeconds(seconds);
            return $"{Math.Max(0, (int)ts.TotalHours)}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        private static string FormatPreviewChance(AttackPreview preview)
        {
            if (preview == null) return "-";

            var min = Mathf.Clamp01(preview.WinChanceMin);
            var max = Mathf.Clamp01(preview.WinChanceMax);
            if (Mathf.Abs(min - max) <= 0.0001f)
            {
                return max.ToString("P0");
            }

            return $"{min:P0} - {max:P0}";
        }

        private static string BuildOwnerText(DisplaySector sector)
        {
            if (sector.OwnerPlayerId > 0)
            {
                var playerColor = HexGridPalette.GetFrontlineColor(sector.OwnerPlayerId, Color.white);
                return $"Owner: <color=#{ColorUtility.ToHtmlStringRGB(playerColor)}>Player {sector.OwnerPlayerId}</color>";
            }

            var neutralColor = HexGridPalette.GetColorForOwner(0, isHome: false, ownedHexAlpha: 1f);
            return $"Owner: <color=#{ColorUtility.ToHtmlStringRGB(neutralColor)}>Neutral</color>";
        }

        private static string BuildDuelOwnerText(DisplaySector sector)
        {
            if (sector.OwnerPlayerId > 0)
            {
                var playerColor = HexGridPalette.GetFrontlineColor(sector.OwnerPlayerId, Color.white);
                var ownerName = sector.OwnerPlayerId == 1 ? "You" : "AI";
                return $"Owner: <color=#{ColorUtility.ToHtmlStringRGB(playerColor)}>{ownerName}</color>";
            }

            var neutralColor = HexGridPalette.GetColorForOwner(0, isHome: false, ownedHexAlpha: 1f);
            return $"Owner: <color=#{ColorUtility.ToHtmlStringRGB(neutralColor)}>Neutral</color>";
        }

        private static string BuildDuelActiveHintText(HexHackDuelService duel)
        {
            return duel.AttackMode == HexHackDuelAttackMode.ManualSwipeSources
                ? $"Swipe from a player node above {duel.AttackControlThreshold:0} control into an adjacent target. Tap a linked source to cancel. Max {duel.MaxConcurrentTargets} targets."
                : "Tap a neutral, enemy, or damaged allied node to route pressure from adjacent player nodes.";
        }

        private string BuildDuelPreviewText(DisplaySector sector, HexHackDuelService duel)
        {
            if (!duel.IsMatchActive)
            {
                return "PLAY to initialize the board.";
            }

            if (duel.AttackMode == HexHackDuelAttackMode.ManualSwipeSources)
            {
                if (HexHackDuelService.IsPlayerOwner(sector.OwnerPlayerId) && duel.HasPlayerLinkFromSource(sector.SectorId))
                {
                    return "Tap to cancel this source link.";
                }

                if (HexHackDuelService.IsPlayerOwner(sector.OwnerPlayerId) && sector.ControlValue > duel.AttackControlThreshold)
                {
                    return "Swipe from this node into an adjacent target.";
                }

                return $"Manual links active {CountActiveLinks(duel.PlayerLinks)}. Max targets {duel.MaxConcurrentTargets}.";
            }

            return HexHackDuelService.IsPlayerOwner(sector.OwnerPlayerId) && sector.ControlValue > duel.AttackControlThreshold
                ? "Player source node."
                : $"Auto-link from adjacent player nodes. Active links {CountActiveLinks(duel.PlayerLinks)}";
        }

        private static string FormatDuelControlShort(float control)
        {
            if (Mathf.Abs(control) < 0.5f) return "0";
            return control > 0f
                ? $"P{Mathf.Abs(Mathf.RoundToInt(control))}"
                : $"E{Mathf.Abs(Mathf.RoundToInt(control))}";
        }

        private static string BuildDuelHexInfoText(DuelNodeState node)
        {
            if (node.IsCore)
            {
                return string.Empty;
            }

            if (node.OwnerPlayerId <= 0 && Mathf.Abs(node.Control) < 0.5f)
            {
                return string.Empty;
            }

            var amount = Mathf.Abs(Mathf.RoundToInt(node.Control));
            var colorOwnerId = node.Control >= 0f ? 1 : 2;
            var color = HexGridPalette.GetFrontlineColor(colorOwnerId, Color.white);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{amount}</color>";
        }

        private static string FormatDuelControlLong(float control)
        {
            if (Mathf.Abs(control) < 0.001f) return "Neutral";
            return control > 0f ? $"+{control:0.#} player" : $"{control:0.#} enemy";
        }

        private static string BuildPowerTooltipBody(SnapshotService.PowerBreakdown breakdown)
        {
            return
                $"+ {breakdown.PrestigePower:0.##} (Prestige)\n" +
                $"+ {breakdown.PermanentUpgradePower:0.##} (Permanent Upgrades)\n" +
                $"+ {breakdown.SectorPower:0.##} (Sectors)\n" +
                $"+ {breakdown.ProductionBonusPower:0.##} (Production Bonus)\n\n" +
                "Subscription boosts production, but does not directly raise PvP Power.";
        }

        private void HideEditorObject(GameObject go)
        {
            if (!Application.isPlaying)
            {
                go.hideFlags = HideFlags.DontSaveInEditor;
            }
        }

        private void DestroyObject(GameObject go)
        {
            if (go == null) return;

            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        public static bool TryInferRadiusFromCellCount(int cellCount, out int radius)
        {
            radius = 0;
            if (cellCount <= 0) return false;

            for (var r = 0; r <= 64; r++)
            {
                var expected = 1 + (3 * r * (r + 1));
                if (expected == cellCount)
                {
                    radius = r;
                    return true;
                }

                if (expected > cellCount)
                {
                    return false;
                }
            }

            return false;
        }

        private static string BuildLiveScoreTable(Bootstrap.GameLoop loop)
        {
            var entries = BuildLiveScoreEntries(loop);
            if (entries.Count == 0)
            {
                return "<mspace=0.58em>PLAYER       POWER SCORE\nNo active factions</mspace>";
            }

            var lines = new string[entries.Count + 1];
            lines[0] = FormatLiveScoreHeaderRow();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                lines[i + 1] = FormatLiveScoreRow(
                    entry.PlayerId,
                    entry.Name,
                    TrimCell(FormatPowerCell(entry.PvpPower), 6),
                    entry.Score.ToString());
            }

            return $"<mspace=0.58em>{string.Join("\n", lines)}</mspace>";
        }

        private static string BuildDuelScoreTable(HexHackDuelService duel)
        {
            var lines = new[]
            {
                FormatLiveScoreHeaderRow(),
                FormatLiveScoreRow(1, "You", TrimCell(FormatPowerCell(duel.PlayerPower), 6), duel.PlayerHexCount.ToString()),
                FormatLiveScoreRow(2, "AI", TrimCell(FormatPowerCell(duel.EnemyPower), 6), duel.EnemyHexCount.ToString()),
                $"{TrimCell("CORE", ScoreboardPlayerColumnWidth).PadRight(ScoreboardPlayerColumnWidth)} {TrimCell(GetCoreOwnerText(duel), ScoreboardPowerColumnWidth + ScoreboardScoreColumnWidth + 1)}"
            };

            return $"<mspace=0.58em>{string.Join("\n", lines)}</mspace>";
        }

        private static string GetCoreOwnerText(HexHackDuelService duel)
        {
            for (var i = 0; i < duel.Nodes.Count; i++)
            {
                var node = duel.Nodes[i];
                if (!node.IsCore) continue;
                return node.OwnerPlayerId switch
                {
                    1 => "You",
                    2 => "AI",
                    _ => "Neutral"
                };
            }

            return "-";
        }

        private static int CountActiveLinks(IReadOnlyList<DuelLinkState> links)
        {
            if (links == null) return 0;

            var count = 0;
            for (var i = 0; i < links.Count; i++)
            {
                if (links[i].IsActive)
                {
                    count++;
                }
            }

            return count;
        }

        private string GetCachedLiveScoreTable(Bootstrap.GameLoop loop)
        {
            var stamp = ComputeLiveScoreStamp(loop);
            if (_cachedLiveScoreStamp == stamp && !string.IsNullOrEmpty(_cachedLiveScoreTable))
            {
                return _cachedLiveScoreTable;
            }

            _cachedLiveScoreStamp = stamp;
            _cachedLiveScoreTable = BuildLiveScoreTable(loop);
            return _cachedLiveScoreTable;
        }

        private static long ComputeLiveScoreStamp(Bootstrap.GameLoop loop)
        {
            if (loop == null) return 0;

            unchecked
            {
                long stamp = 17;
                stamp = (stamp * 31) + loop.NowUnixSeconds;
                stamp = (stamp * 31) + ComputeLocalPowerStateStamp(loop.State);

                var mapState = loop.State?.MapState;
                stamp = (stamp * 31) + (mapState?.MapSeasonId ?? 0);
                stamp = (stamp * 31) + (mapState?.MatchStartUnixSeconds ?? 0);

                var sectors = mapState?.Sectors;
                if (sectors != null)
                {
                    for (var i = 0; i < sectors.Length; i++)
                    {
                        var sector = sectors[i];
                        if (sector == null)
                        {
                            stamp = (stamp * 31) - 1;
                            continue;
                        }

                        stamp = (stamp * 31) + sector.SectorId;
                        stamp = (stamp * 31) + sector.OwnerPlayerId;
                    }
                }

                return stamp;
            }
        }

        private static long ComputeDetailStateStamp(Bootstrap.GameLoop loop, DisplaySector sector, AttackStrategy strategy)
        {
            if (loop == null) return 0;

            unchecked
            {
                long stamp = 17;
                stamp = (stamp * 31) + sector.SectorId;
                stamp = (stamp * 31) + sector.OwnerPlayerId;
                stamp = (stamp * 31) + (long)Mathf.RoundToInt(sector.Stability * 100f);
                stamp = (stamp * 31) + (int)strategy;
                stamp = (stamp * 31) + loop.NowUnixSeconds;
                stamp = (stamp * 31) + ComputeLocalPowerStateStamp(loop.State);
                stamp = (stamp * 31) + (loop.State?.PvpAttacksRemaining ?? 0);
                stamp = (stamp * 31) + (loop.State?.NextPvpAttackRegenAtUnixSeconds ?? 0);

                var runtimeSector = loop.Map?.GetSector(sector.SectorId);
                if (runtimeSector != null)
                {
                    stamp = (stamp * 31) + runtimeSector.OwnerPlayerId;
                    stamp = (stamp * 31) + runtimeSector.LastCombatUnixSeconds;
                    stamp = (stamp * 31) + runtimeSector.CapturedUnixSeconds;
                }

                return stamp;
            }
        }

        private static long ComputeLocalPowerStateStamp(MapState state)
        {
            if (state == null) return 0;

            unchecked
            {
                long stamp = 17;
                stamp = (stamp * 31) + state.MapSeasonId;
                return stamp;
            }
        }

        private static long ComputeLocalPowerStateStamp(GameState state)
        {
            if (state == null) return 0;

            unchecked
            {
                long stamp = 17;
                stamp = (stamp * 31) + state.PrestigeCount;
                stamp = (stamp * 31) + state.PermanentUpgradeLevel;
                stamp = (stamp * 31) + ComputeLocalPowerStateStamp(state.MapState);

                var levels = state.GeneratorLevels;
                if (levels != null)
                {
                    for (var i = 0; i < levels.Length; i++)
                    {
                        stamp = (stamp * 31) + levels[i];
                    }
                }

                var sectors = state.MapState?.Sectors;
                if (sectors != null)
                {
                    for (var i = 0; i < sectors.Length; i++)
                    {
                        var sector = sectors[i];
                        if (sector == null)
                        {
                            stamp = (stamp * 31) - 1;
                            continue;
                        }

                        stamp = (stamp * 31) + sector.SectorId;
                        stamp = (stamp * 31) + sector.OwnerPlayerId;
                    }
                }

                return stamp;
            }
        }

        private static List<LiveScoreEntry> BuildLiveScoreEntries(Bootstrap.GameLoop loop)
        {
            if (loop == null) return new List<LiveScoreEntry>();
            return BuildLiveScoreEntries(loop.MapConfig, loop.State?.MapState, loop.FactionSnapshots);
        }

        private static List<LiveScoreEntry> BuildLiveScoreEntries(MapConfig mapConfig, MapState mapState, FactionSnapshotService factionSnapshots)
        {
            var entries = new List<LiveScoreEntry>();
            if (mapConfig == null || mapState == null || factionSnapshots == null) return entries;

            var playerIds = CollectTrackedPlayerIds(mapConfig, mapState);
            for (var i = 0; i < playerIds.Count; i++)
            {
                var playerId = playerIds[i];
                if (playerId <= 0) continue;

                var snapshot = factionSnapshots.BuildCurrentSnapshot(playerId);
                entries.Add(new LiveScoreEntry(
                    playerId,
                    GetPlayerDisplayName(mapConfig, playerId),
                    snapshot?.PvpPower ?? 0,
                    CountOwnedSectors(mapState, playerId)));
            }

            entries.Sort((a, b) =>
            {
                var scoreCompare = b.Score.CompareTo(a.Score);
                if (scoreCompare != 0) return scoreCompare;

                var powerCompare = b.PvpPower.CompareTo(a.PvpPower);
                if (powerCompare != 0) return powerCompare;

                return a.PlayerId.CompareTo(b.PlayerId);
            });

            return entries;
        }

        private static List<int> CollectTrackedPlayerIds(MapConfig mapConfig, MapState mapState)
        {
            var playerIds = new List<int>();
            if (mapConfig == null) return playerIds;

            var homeSectorIds = mapConfig.GetEffectiveHomeSectorIds();
            for (var i = 0; i < homeSectorIds.Length; i++)
            {
                var playerId = mapConfig.GetHomeOwnerPlayerId(homeSectorIds[i]);
                if (playerId > 0 && !playerIds.Contains(playerId))
                {
                    playerIds.Add(playerId);
                }
            }

            var sectors = mapState?.Sectors ?? Array.Empty<SectorState>();
            for (var i = 0; i < sectors.Length; i++)
            {
                var sector = sectors[i];
                if (sector == null) continue;
                if (sector.OwnerPlayerId <= 0) continue;
                if (!playerIds.Contains(sector.OwnerPlayerId))
                {
                    playerIds.Add(sector.OwnerPlayerId);
                }
            }

            return playerIds;
        }

        private static int CountOwnedSectors(MapState mapState, int playerId)
        {
            if (mapState?.Sectors == null || playerId <= 0) return 0;

            var owned = 0;
            for (var i = 0; i < mapState.Sectors.Length; i++)
            {
                var sector = mapState.Sectors[i];
                if (sector == null) continue;
                if (sector.OwnerPlayerId == playerId) owned++;
            }

            return owned;
        }

        private static string GetPlayerDisplayName(MapConfig mapConfig, int playerId)
        {
            if (mapConfig != null && playerId == mapConfig.LocalPlayerId) return "You";
            return $"Player {playerId}";
        }

        private static string FormatLiveScoreRow(string player, string power, string score)
        {
            return $"{player} {TrimCell(power, ScoreboardPowerColumnWidth).PadLeft(ScoreboardPowerColumnWidth)} {TrimCell(score, ScoreboardScoreColumnWidth).PadLeft(ScoreboardScoreColumnWidth)}";
        }

        private static string FormatLiveScoreRow(int playerId, string playerName, string power, string score)
        {
            var row = $"{TrimCell(playerName, ScoreboardPlayerColumnWidth).PadRight(ScoreboardPlayerColumnWidth)} {TrimCell(power, ScoreboardPowerColumnWidth).PadLeft(ScoreboardPowerColumnWidth)} {TrimCell(score, ScoreboardScoreColumnWidth).PadLeft(ScoreboardScoreColumnWidth)}";
            var color = HexGridPalette.GetFrontlineColor(playerId, Color.white);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{row}</color>";
        }

        private static string FormatLiveScoreHeaderRow()
        {
            return $"{TrimCell("PLAYER", ScoreboardPlayerColumnWidth).PadRight(ScoreboardPlayerColumnWidth)} {TrimCell("POWER", ScoreboardPowerColumnWidth).PadLeft(ScoreboardPowerColumnWidth)} {TrimCell("SCORE", ScoreboardScoreColumnWidth).PadLeft(ScoreboardScoreColumnWidth)}";
        }

        private static string FormatPowerCell(double power)
        {
            if (double.IsNaN(power) || double.IsInfinity(power)) return "0";
            return power >= 100 ? power.ToString("0") : power.ToString("0.0");
        }

        private static string TrimCell(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value) || maxChars <= 0) return string.Empty;
            return value.Length <= maxChars ? value : value.Substring(0, maxChars);
        }

        private readonly struct GridSnapshot
        {
            public readonly int Radius;
            public readonly int Count;
            public readonly IReadOnlyList<DisplaySector> Sectors;

            public GridSnapshot(int radius, int count, IReadOnlyList<DisplaySector> sectors)
            {
                Radius = radius;
                Count = count;
                Sectors = sectors;
            }

            public int ComputeLayoutHash()
            {
                unchecked
                {
                    var hash = 17;
                    hash = (hash * 31) + Radius;
                    hash = (hash * 31) + Count;
                    for (var i = 0; i < Sectors.Count; i++)
                    {
                        hash = (hash * 31) + Sectors[i].SectorId;
                    }

                    return hash;
                }
            }
        }

        internal struct DisplaySector
        {
            public HexCoord Coord;
            public int SectorIndex;
            public int SectorId;
            public string Label;
            public bool IsHome;
            public bool IsCoreSector;
            public int OwnerPlayerId;
            public float Stability;
            public float ControlValue;
            public float ProductionBonusPercent;
            public bool IsAttackFrontier;
            public bool IsDisconnectedOwned;
            public bool IsDuelSector;
            public string InfoText;
            public string BadgeText;
        }

        internal struct SectorDetailState
        {
            public DisplaySector Sector;
            public string OwnerText;
            public string BonusText;
            public string StabilityText;
            public string PreviewText;
            public string PositionText;
            public string StatusText;
            public string HintText;
            public string ActionLabel;
            public string StrategyLabel;
            public bool CanAttack;
            public bool CanCycleStrategy;
        }

        private readonly struct LiveScoreEntry
        {
            public readonly int PlayerId;
            public readonly string Name;
            public readonly double PvpPower;
            public readonly int Score;

            public LiveScoreEntry(int playerId, string name, double pvpPower, int score)
            {
                PlayerId = playerId;
                Name = name ?? string.Empty;
                PvpPower = pvpPower;
                Score = score;
            }
        }
    }

    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public int Q { get; }
        public int R { get; }

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public bool Equals(HexCoord other)
        {
            return Q == other.Q && R == other.R;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Q * 397) ^ R;
            }
        }

        public override string ToString()
        {
            return $"({Q},{R})";
        }
    }

    public static class HexGridMath
    {
        private static readonly float Sqrt3 = Mathf.Sqrt(3f);
        public static readonly HexCoord[] NeighborDirections =
        {
            new(1, 0),
            new(1, -1),
            new(0, -1),
            new(-1, 0),
            new(-1, 1),
            new(0, 1)
        };

        public static List<HexCoord> EnumerateAxial(int radius)
        {
            radius = Mathf.Max(0, radius);
            var coords = new List<HexCoord>(1 + (3 * radius * (radius + 1)));

            for (var q = -radius; q <= radius; q++)
            {
                var rMin = Mathf.Max(-radius, -q - radius);
                var rMax = Mathf.Min(radius, -q + radius);

                for (var r = rMin; r <= rMax; r++)
                {
                    coords.Add(new HexCoord(q, r));
                }
            }

            coords.Sort((a, b) =>
            {
                var byR = b.R.CompareTo(a.R);
                return byR != 0 ? byR : a.Q.CompareTo(b.Q);
            });

            return coords;
        }

        public static List<HexCoord> GetCornerCoords(int radius)
        {
            radius = Mathf.Max(0, radius);
            return new List<HexCoord>
            {
                new(radius, 0),
                new(0, radius),
                new(-radius, radius),
                new(-radius, 0),
                new(0, -radius),
                new(radius, -radius)
            };
        }

        public static Vector2 AxialToLocalPosition(HexCoord coord, float hexRadius)
        {
            var x = hexRadius * Sqrt3 * (coord.Q + (coord.R * 0.5f));
            var y = hexRadius * 1.5f * coord.R;
            return new Vector2(x, y);
        }

        public static Vector2 GetCellSize(float hexRadius)
        {
            return new Vector2(Sqrt3 * hexRadius, 2f * hexRadius);
        }

        public static Vector2[] GetHexCorners(Vector2 center, float hexRadius)
        {
            var corners = new Vector2[6];
            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * ((60f * i) - 30f);
                corners[i] = new Vector2(
                    center.x + (hexRadius * Mathf.Cos(angle)),
                    center.y + (hexRadius * Mathf.Sin(angle)));
            }

            return corners;
        }

        public static (Vector2 start, Vector2 end) GetSharedEdge(Vector2 a, Vector2 b, float hexRadius)
        {
            var mid = (a + b) * 0.5f;
            var dir = (b - a).normalized;
            var perp = new Vector2(-dir.y, dir.x);
            var halfLength = Mathf.Max(0.5f, hexRadius * 0.5f);
            return (mid - (perp * halfLength), mid + (perp * halfLength));
        }
    }

    internal static class HexGridPalette
    {
        private static readonly Color NeutralColor = new(0.33f, 0.37f, 0.44f, 1f);
        public static readonly Color[] OwnerColors =
        {
            new(0.2039f, 1f, 0.1176f, 1f),
            new(0.95f, 0.66f, 0.23f, 1f),
            new(0.40f, 0.72f, 0.34f, 1f),
            new(0.18f, 0.62f, 0.87f, 1f),
            new(0.49f, 0.43f, 0.88f, 1f),
            new(0.86f, 0.31f, 0.63f, 1f),
        };

        public static Color GetColorForOwner(int ownerPlayerId, bool isHome, float ownedHexAlpha)
        {
            if (ownerPlayerId <= 0)
            {
                return isHome ? Color.Lerp(NeutralColor, new Color(0.94f, 0.82f, 0.38f, 1f), 0.35f) : NeutralColor;
            }

            var color = OwnerColors[(ownerPlayerId - 1) % OwnerColors.Length];
            if (!isHome)
            {
                color.a = Mathf.Clamp01(ownedHexAlpha);
            }

            return color;
        }

        public static Color GetFrontlineColor(int ownerPlayerId, Color fallback)
        {
            if (ownerPlayerId <= 0) return fallback;

            var color = OwnerColors[(ownerPlayerId - 1) % OwnerColors.Length];
            color.a = 1f;
            return color;
        }

        public static Color GetNetworkColor(int ownerPlayerId, float alpha)
        {
            if (ownerPlayerId <= 0)
            {
                return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }

            var color = OwnerColors[(ownerPlayerId - 1) % OwnerColors.Length];
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        public static Color GetGlowColor(int ownerPlayerId, float alpha)
        {
            var color = GetFrontlineColor(ownerPlayerId, Color.white);
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }

    internal sealed class HexCellView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private HexCellGraphic _graphic;
        private Button _button;
        private TMP_Text _label;
        private TMP_Text _info;
        private TMP_Text _homeBadge;
        private TMP_Text _marker;
        private Action<HexCellView, PointerEventData> _onBeginDrag;
        private Action<HexCellView, PointerEventData> _onEndDrag;
        private Color _baseColor;
        private bool _selected;

        public int SectorId { get; private set; }
        public HexGridRenderer.DisplaySector CurrentSector { get; private set; }

        public void Initialize(
            HexCellGraphic graphic,
            Button button,
            TMP_Text label,
            TMP_Text info,
            TMP_Text homeBadge,
            TMP_Text marker,
            Action<HexCellView, PointerEventData> onBeginDrag,
            Action<HexCellView, PointerEventData> onEndDrag)
        {
            _graphic = graphic;
            _button = button;
            _label = label;
            _info = info;
            _homeBadge = homeBadge;
            _marker = marker;
            _onBeginDrag = onBeginDrag;
            _onEndDrag = onEndDrag;
        }

        public void Apply(HexGridRenderer.DisplaySector sector, bool showCoordinates, bool showHomeBadge, float ownedHexAlpha, bool showConnectivityMarkers)
        {
            CurrentSector = sector;
            SectorId = sector.SectorId;
            _baseColor = HexGridPalette.GetColorForOwner(sector.OwnerPlayerId, sector.IsHome, ownedHexAlpha);
            var showsSpecialBadge = sector.IsHome || sector.IsCoreSector;

            if (_graphic != null)
            {
                _graphic.color = _selected ? Color.Lerp(_baseColor, Color.white, 0.18f) : _baseColor;
            }

            if (_label != null)
            {
                if (sector.IsDuelSector && sector.IsCoreSector)
                {
                    _label.text = string.Empty;
                    _label.gameObject.SetActive(false);
                }
                else if (showsSpecialBadge)
                {
                    _label.text = showCoordinates
                        ? $"{sector.Label}\n{sector.Coord.Q},{sector.Coord.R}"
                        : sector.Label;
                    _label.gameObject.SetActive(true);
                }
                else
                {
                    _label.text = string.Empty;
                    _label.gameObject.SetActive(false);
                }
            }

            if (_info != null)
            {
                var infoText = sector.InfoText ?? string.Empty;
                _info.text = infoText;
                _info.gameObject.SetActive(!string.IsNullOrEmpty(infoText));
            }

            if (_homeBadge != null)
            {
                if (_homeBadge.transform is RectTransform badgeRect)
                {
                    if (sector.IsDuelSector && sector.IsCoreSector)
                    {
                        badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
                        badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
                        badgeRect.pivot = new Vector2(0.5f, 0.5f);
                        badgeRect.anchoredPosition = Vector2.zero;
                    }
                    else
                    {
                        badgeRect.anchorMin = new Vector2(0.5f, 1f);
                        badgeRect.anchorMax = new Vector2(0.5f, 1f);
                        badgeRect.pivot = new Vector2(0.5f, 1f);
                        badgeRect.anchoredPosition = new Vector2(0f, -8f);
                    }
                }

                _homeBadge.text = string.IsNullOrWhiteSpace(sector.BadgeText)
                    ? "HOME"
                    : sector.BadgeText;
                _homeBadge.gameObject.SetActive(showHomeBadge && showsSpecialBadge);
            }

            if (_marker != null)
            {
                if (!showConnectivityMarkers || showsSpecialBadge)
                {
                    _marker.text = string.Empty;
                    _marker.gameObject.SetActive(false);
                }
                else if (sector.IsDisconnectedOwned)
                {
                    _marker.text = "ISLAND";
                    _marker.color = new Color(1f, 0.86f, 0.47f, 0.96f);
                    _marker.gameObject.SetActive(true);
                }
                else
                {
                    _marker.text = string.Empty;
                    _marker.gameObject.SetActive(false);
                }
            }

        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            transform.localScale = selected ? Vector3.one * 1.08f : Vector3.one;

            if (_graphic != null)
            {
                _graphic.color = selected ? Color.Lerp(_baseColor, Color.white, 0.18f) : _baseColor;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _onBeginDrag?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _onEndDrag?.Invoke(this, eventData);
        }
    }

    internal sealed class HexCellGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            var radius = Mathf.Min(rect.width / Mathf.Sqrt(3f), rect.height / 2f);

            var centerVertex = UIVertex.simpleVert;
            centerVertex.color = color;
            centerVertex.position = center;
            vh.AddVert(centerVertex);

            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * ((60f * i) - 30f);
                var vertex = UIVertex.simpleVert;
                vertex.color = color;
                vertex.position = new Vector2(
                    center.x + (radius * Mathf.Cos(angle)),
                    center.y + (radius * Mathf.Sin(angle)));
                vh.AddVert(vertex);
            }

            for (var i = 1; i <= 6; i++)
            {
                var next = i == 6 ? 1 : i + 1;
                vh.AddTriangle(0, i, next);
            }
        }
    }

    internal sealed class HexFrontierGraphic : MaskableGraphic
    {
        internal readonly struct LineSegment
        {
            public readonly Vector2 Start;
            public readonly Vector2 End;
            public readonly Color Color;

            public LineSegment(Vector2 start, Vector2 end, Color color)
            {
                Start = start;
                End = end;
                Color = color;
            }
        }

        private readonly List<LineSegment> _segments = new();
        private float _thickness = 4f;

        public void SetSegments(IReadOnlyList<LineSegment> segments, float thickness)
        {
            _segments.Clear();
            if (segments != null)
            {
                for (var i = 0; i < segments.Count; i++)
                {
                    _segments.Add(segments[i]);
                }
            }

            _thickness = Mathf.Max(1f, thickness);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (_segments.Count == 0) return;

            for (var i = 0; i < _segments.Count; i++)
            {
                AddLineQuad(vh, _segments[i], _segments[i].Color, _thickness);
            }
        }

        private static void AddLineQuad(VertexHelper vh, LineSegment segment, Color color, float thickness)
        {
            var delta = segment.End - segment.Start;
            if (delta.sqrMagnitude <= 0.0001f) return;

            var normal = new Vector2(-delta.y, delta.x).normalized * (thickness * 0.5f);
            var v0 = segment.Start - normal;
            var v1 = segment.Start + normal;
            var v2 = segment.End + normal;
            var v3 = segment.End - normal;

            var index = vh.currentVertCount;
            vh.AddVert(v0, color, Vector2.zero);
            vh.AddVert(v1, color, Vector2.up);
            vh.AddVert(v2, color, Vector2.one);
            vh.AddVert(v3, color, Vector2.right);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }

    internal sealed class PvpToastView : MonoBehaviour
    {
        private TMP_Text _text;
        private float _hideAt;

        public void Initialize(TMP_Text text)
        {
            _text = text;
            gameObject.SetActive(false);
        }

        public void Show(string message, float seconds = 2.25f)
        {
            if (_text != null) _text.text = message;
            gameObject.SetActive(true);
            _hideAt = Time.unscaledTime + Mathf.Max(0.5f, seconds);
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;
            if (Time.unscaledTime < _hideAt) return;
            gameObject.SetActive(false);
        }
    }
}
