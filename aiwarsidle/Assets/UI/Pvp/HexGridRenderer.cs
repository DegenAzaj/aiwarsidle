using System;
using System.Collections.Generic;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.PvP.Config;
using AIWarsIdle.PvP.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AIWarsIdle.UI.Pvp
{
    [ExecuteAlways]
    public sealed class HexGridRenderer : MonoBehaviour
    {
        private const string GeneratedRootName = "__hex_grid_generated";
        private const string GeneratedPanelName = "__hex_grid_panel";
        private const string GeneratedHudName = "__hex_grid_hud";
        private const string GeneratedToastName = "__hex_grid_toast";
        private const string GeneratedDismissLayerName = "__hex_grid_dismiss";
        private const string GeneratedFrontierName = "__hex_grid_frontier";
        private const string GeneratedFrontierGlowName = "__hex_grid_frontier_glow";
        private const string GeneratedNetworkName = "__hex_grid_network";

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

        [Header("Detail Panel")]
        [SerializeField, Min(0f)] private float _detailPanelHorizontalOffset = 164f;
        [SerializeField, Min(0f)] private float _detailPanelVerticalOffset = 104f;
        [SerializeField, Min(0f)] private float _detailPanelEdgePadding = 12f;

        private readonly List<HexCellView> _cells = new();
        private readonly Dictionary<int, HexCellView> _cellsBySectorId = new();
        private RectTransform _generatedRoot;
        private SectorDetailPanelView _detailPanel;
        private PvpHudView _hud;
        private PvpToastView _toast;
        private readonly List<TMP_Text> _externalAttacksTexts = new();
        private readonly List<TMP_Text> _externalRegenTexts = new();
        private HexCellView _selected;
        private float _refreshCarry;
        private int _lastLayoutHash;
        private Vector2 _lastViewportSize;
        private AttackStrategy _selectedStrategy = AttackStrategy.Stable;
        private bool _powerInfoVisible;

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
            _hud = null;
            _toast = null;
            _externalAttacksTexts.Clear();
            _externalRegenTexts.Clear();
            _selected = null;
            _lastLayoutHash = 0;
            _lastViewportSize = Vector2.zero;
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
            _detailPanelEdgePadding = Mathf.Max(0f, _detailPanelEdgePadding);

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

            ApplyConnectivityMarkers(config, sectors);

            return new GridSnapshot(resolvedRadius, coords.Count, sectors);
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

            FitGridToViewport(root);

            UpdateHud(snapshot);
            if (_detailPanel != null)
            {
                _detailPanel.Hide();
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
            UpdateHud(snapshot);
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

        private SectorDetailPanelView GetOrCreateDetailPanel()
        {
            if (_detailPanel != null) return _detailPanel;

            var existing = transform.Find(GeneratedPanelName);
            if (existing != null)
            {
                _detailPanel = existing.GetComponent<SectorDetailPanelView>();
                if (_detailPanel != null) return _detailPanel;
            }

            var panelGo = new GameObject(
                GeneratedPanelName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(SectorDetailPanelView));
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

            _detailPanel = panelGo.GetComponent<SectorDetailPanelView>();
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
            return _detailPanel;
        }

        private PvpHudView GetOrCreateHud()
        {
            if (_hud != null) return _hud;

            var existing = transform.Find(GeneratedHudName);
            if (existing != null)
            {
                _hud = existing.GetComponent<PvpHudView>();
                if (_hud != null) return _hud;
            }

            var hudGo = new GameObject(
                GeneratedHudName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(PvpHudView));
            hudGo.transform.SetParent(transform, worldPositionStays: false);
            HideEditorObject(hudGo);

            var rect = hudGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(320f, 190f);
            rect.anchoredPosition = new Vector2(12f, -12f);

            var bg = hudGo.GetComponent<Image>();
            bg.color = new Color32(18, 24, 34, 224);

            _hud = hudGo.GetComponent<PvpHudView>();
            _hud.Initialize(
                CreateHudText(rect, "attacks", new Vector2(16f, -18f), 22f),
                CreateHudText(rect, "league", new Vector2(16f, -52f), 18f),
                CreateHudText(rect, "map_reset", new Vector2(16f, -82f), 18f),
                CreateHudText(rect, "power", new Vector2(16f, -112f), 18f),
                CreateHudText(rect, "hint", new Vector2(16f, -142f), 14f),
                CreatePanelSecondaryButton(rect, "power_info_button", new Vector2(240f, 138f), "PvpPower ?"),
                CreateHudTooltip(rect),
                TogglePowerInfoTooltip);
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
            return _toast;
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
            if (panel != null)
            {
                DestroyObject(panel.gameObject);
            }

            var hud = transform.Find(GeneratedHudName);
            if (hud != null)
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
                CreateMarkerLabel(rect));
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

            if (!_showOwnershipNetwork)
            {
                graphic.SetSegments(Array.Empty<HexFrontierGraphic.LineSegment>(), Mathf.Max(1f, _frontlineThickness * 0.8f));
                graphic.gameObject.SetActive(false);
                return;
            }

            var segments = BuildOwnershipNetworkSegments(snapshot);
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
                _detailPanel.Hide();
            }
        }

        private void UpdateDetailPanel(HexCellView cell)
        {
            var panel = GetOrCreateDetailPanel();
            panel.ShowSector(BuildDetailState(cell.CurrentSector));
            PositionDetailPanel(panel.GetComponent<RectTransform>(), cell);
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

            var candidates = new[]
            {
                localCenter + new Vector2(rightBias ? _detailPanelHorizontalOffset : -_detailPanelHorizontalOffset, _detailPanelVerticalOffset),
                localCenter + new Vector2(rightBias ? -_detailPanelHorizontalOffset : _detailPanelHorizontalOffset, _detailPanelVerticalOffset),
                localCenter + new Vector2(rightBias ? _detailPanelHorizontalOffset : -_detailPanelHorizontalOffset, _detailPanelVerticalOffset * 0.35f),
                localCenter + new Vector2(rightBias ? -_detailPanelHorizontalOffset : _detailPanelHorizontalOffset, _detailPanelVerticalOffset * 0.35f),
                localCenter + new Vector2(rightBias ? _detailPanelHorizontalOffset : -_detailPanelHorizontalOffset, -_detailPanelVerticalOffset * 0.55f),
                localCenter + new Vector2(rightBias ? -_detailPanelHorizontalOffset : _detailPanelHorizontalOffset, -_detailPanelVerticalOffset * 0.55f),
                localCenter + new Vector2(0f, _detailPanelVerticalOffset),
                localCenter + new Vector2(0f, -_detailPanelVerticalOffset * 0.7f),
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

            if (loop == null)
            {
                hud.ShowPreview(snapshot.Count, _radius, _powerInfoVisible);
                return;
            }

            var attacks = loop.PvpAttacks;
            var league = loop.League;
            var snapshotService = loop.Snapshot;

            var now = loop.NowUnixSeconds;
            var remaining = attacks != null ? Mathf.Clamp(loop.State.PvpAttacksRemaining, 0, attacks.MaxAttacks) : 0;
            var max = attacks != null ? attacks.MaxAttacks : 0;
            var nextRegenAt = attacks != null ? loop.State.NextPvpAttackRegenAtUnixSeconds : 0;
            var attackCountText = $"{remaining}/{Mathf.Max(max, remaining)}";
            var regenTimerText = max > 0 && remaining < max && nextRegenAt > now
                ? FormatClockDuration(nextRegenAt - now)
                : string.Empty;

            var leagueText = league != null
                ? $"League {loop.State.League}  |  Season points {loop.State.SeasonPoints}"
                : "League: unavailable";

            var mapResetText = loop.Map != null
                ? $"Map resets in {FormatDuration(Math.Max(0, loop.Map.GetCurrentSeasonEndsAtUnixSeconds(now) - now))}"
                : "Map reset: unavailable";

            var power = snapshotService != null ? snapshotService.BuildSnapshot().PvpPower : 0;
            var powerText = $"PvpPower {power:0.##}";

            var hint = league != null
                ? $"League season ends in {FormatDuration(Math.Max(0, league.GetCurrentSeasonEndsAtUnixSeconds(now) - now))}"
                : "Tap PvpPower for breakdown";

            UpdateExternalAttackHud(attackCountText, regenTimerText);
            hud.ShowState(_externalAttacksTexts.Count > 0 ? string.Empty : $"Attacks: {attackCountText}",
                leagueText,
                mapResetText,
                powerText,
                hint,
                _powerInfoVisible);
        }

        private void UpdateExternalAttackHud(string countText, string regenText)
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
        }

        private void EnsureExternalHudRefs()
        {
            if (_externalAttacksTexts.Count > 0 && _externalRegenTexts.Count > 0) return;
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
            var ownerText = sector.OwnerPlayerId > 0 ? $"Owner: Player {sector.OwnerPlayerId}" : "Owner: Neutral";
            var bonusText = $"Bonus: +{sector.ProductionBonusPercent:0.#}% production";
            var stabilityText = $"Stability: {sector.Stability:0.#}%";
            var positionText = $"Coord: {sector.Coord.Q}, {sector.Coord.R}  |  Id: {sector.SectorId}";
            var previewText = $"Preview: select runtime PvP services";
            var statusText = "Sector ready.";
            var hintText = "Click Strategy to cycle Aggressive / Stable / Risky.";
            var actionLabel = $"Attack ({_selectedStrategy})";
            var canAttack = false;

            if (loop?.PvpCombat != null)
            {
                var now = loop.NowUnixSeconds;
                var evaluation = loop.PvpCombat.EvaluateAttack(sector.SectorId, now);

                if (evaluation.CanAttack)
                {
                    var preview = loop.PvpCombat.GetAttackPreview(sector.SectorId, _selectedStrategy, now);
                    previewText = $"Preview: {FormatPreviewChance(preview)} win  ({_selectedStrategy})";
                    statusText = "Attack available.";
                    canAttack = true;
                }
                else
                {
                    previewText = $"Preview: blocked ({_selectedStrategy})";
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
                StrategyLabel = $"Strategy: {_selectedStrategy}",
                CanAttack = canAttack
            };
        }

        private void OnCycleStrategy()
        {
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

        private void TogglePowerInfoTooltip()
        {
            _powerInfoVisible = !_powerInfoVisible;
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
            public int OwnerPlayerId;
            public float Stability;
            public float ProductionBonusPercent;
            public bool IsAttackFrontier;
            public bool IsDisconnectedOwned;
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
            new(0.91f, 0.35f, 0.29f, 1f),
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

    internal sealed class HexCellView : MonoBehaviour
    {
        private HexCellGraphic _graphic;
        private Button _button;
        private TMP_Text _label;
        private TMP_Text _info;
        private TMP_Text _homeBadge;
        private TMP_Text _marker;
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
            TMP_Text marker)
        {
            _graphic = graphic;
            _button = button;
            _label = label;
            _info = info;
            _homeBadge = homeBadge;
            _marker = marker;
        }

        public void Apply(HexGridRenderer.DisplaySector sector, bool showCoordinates, bool showHomeBadge, float ownedHexAlpha, bool showConnectivityMarkers)
        {
            CurrentSector = sector;
            SectorId = sector.SectorId;
            _baseColor = HexGridPalette.GetColorForOwner(sector.OwnerPlayerId, sector.IsHome, ownedHexAlpha);

            if (_graphic != null)
            {
                _graphic.color = _selected ? Color.Lerp(_baseColor, Color.white, 0.18f) : _baseColor;
            }

            if (_label != null)
            {
                if (sector.IsHome)
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
                var shouldShowStability = sector.IsHome || sector.Stability < 100f;
                _info.text = shouldShowStability
                    ? $"{Mathf.RoundToInt(sector.Stability)}%"
                    : string.Empty;
                _info.gameObject.SetActive(shouldShowStability);
            }

            if (_homeBadge != null)
            {
                _homeBadge.gameObject.SetActive(showHomeBadge && sector.IsHome);
            }

            if (_marker != null)
            {
                if (!showConnectivityMarkers || sector.IsHome)
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

    internal sealed class SectorDetailPanelView : MonoBehaviour
    {
        private TMP_Text _title;
        private TMP_Text _owner;
        private TMP_Text _bonus;
        private TMP_Text _stability;
        private TMP_Text _preview;
        private TMP_Text _position;
        private TMP_Text _status;
        private Button _strategyButton;
        private TMP_Text _strategyButtonLabel;
        private Button _actionButton;
        private TMP_Text _hint;
        private TMP_Text _buttonLabel;
        private Action _onCycleStrategy;
        private Action _onAttack;

        public void Initialize(
            TMP_Text title,
            TMP_Text owner,
            TMP_Text bonus,
            TMP_Text stability,
            TMP_Text preview,
            TMP_Text position,
            TMP_Text status,
            Button strategyButton,
            Button actionButton,
            TMP_Text hint,
            Action onCycleStrategy,
            Action onAttack)
        {
            _title = title;
            _owner = owner;
            _bonus = bonus;
            _stability = stability;
            _preview = preview;
            _position = position;
            _status = status;
            _strategyButton = strategyButton;
            _actionButton = actionButton;
            _hint = hint;
            _onCycleStrategy = onCycleStrategy;
            _onAttack = onAttack;
            _strategyButtonLabel = strategyButton != null
                ? strategyButton.GetComponentInChildren<TMP_Text>(includeInactive: true)
                : null;
            _buttonLabel = actionButton != null
                ? actionButton.GetComponentInChildren<TMP_Text>(includeInactive: true)
                : null;

            if (_strategyButton != null)
            {
                _strategyButton.onClick.RemoveAllListeners();
                _strategyButton.onClick.AddListener(() => _onCycleStrategy?.Invoke());
            }

            if (_actionButton != null)
            {
                _actionButton.onClick.RemoveAllListeners();
                _actionButton.onClick.AddListener(() => _onAttack?.Invoke());
            }
        }

        public void ShowEmpty()
        {
            gameObject.SetActive(true);
            if (_title != null) _title.text = "Sector";
            if (_owner != null) _owner.text = "Owner: -";
            if (_bonus != null) _bonus.text = "Bonus: -";
            if (_stability != null) _stability.text = "Stability: -";
            if (_preview != null) _preview.text = "Preview: -";
            if (_position != null) _position.text = "Coord: -";
            if (_status != null) _status.text = "Select a sector on the map.";
            if (_hint != null) _hint.text = "Hexes are generated from radius and runtime map data.";
            if (_strategyButton != null) _strategyButton.interactable = false;
            if (_strategyButtonLabel != null) _strategyButtonLabel.text = "Strategy: Stable";
            if (_actionButton != null) _actionButton.interactable = false;
            if (_buttonLabel != null) _buttonLabel.text = "Attack";
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void ShowSector(HexGridRenderer.SectorDetailState detailState)
        {
            gameObject.SetActive(true);
            if (_title != null) _title.text = detailState.Sector.Label;
            if (_owner != null) _owner.text = detailState.OwnerText;
            if (_bonus != null) _bonus.text = detailState.BonusText;
            if (_stability != null) _stability.text = detailState.StabilityText;
            if (_preview != null) _preview.text = detailState.PreviewText;
            if (_position != null) _position.text = detailState.PositionText;
            if (_status != null) _status.text = detailState.StatusText;
            if (_hint != null) _hint.text = detailState.HintText;
            if (_strategyButton != null) _strategyButton.interactable = true;
            if (_strategyButtonLabel != null) _strategyButtonLabel.text = detailState.StrategyLabel;
            if (_actionButton != null) _actionButton.interactable = detailState.CanAttack;
            if (_buttonLabel != null) _buttonLabel.text = detailState.ActionLabel;
        }
    }

    internal sealed class PvpHudView : MonoBehaviour
    {
        private TMP_Text _attacks;
        private TMP_Text _league;
        private TMP_Text _mapReset;
        private TMP_Text _power;
        private TMP_Text _hint;
        private Button _powerInfoButton;
        private TMP_Text _powerTooltip;
        private Action _togglePowerInfo;

        public void Initialize(
            TMP_Text attacks,
            TMP_Text league,
            TMP_Text mapReset,
            TMP_Text power,
            TMP_Text hint,
            Button powerInfoButton,
            TMP_Text powerTooltip,
            Action togglePowerInfo)
        {
            _attacks = attacks;
            _league = league;
            _mapReset = mapReset;
            _power = power;
            _hint = hint;
            _powerInfoButton = powerInfoButton;
            _powerTooltip = powerTooltip;
            _togglePowerInfo = togglePowerInfo;

            if (_powerInfoButton != null)
            {
                _powerInfoButton.onClick.RemoveAllListeners();
                _powerInfoButton.onClick.AddListener(() => _togglePowerInfo?.Invoke());
            }
        }

        public void ShowPreview(int count, int radius, bool infoVisible)
        {
            ShowState(
                $"Preview map: {count} hexes",
                $"Radius: {radius}",
                "Runtime PvP HUD appears in play mode.",
                "PvpPower preview unavailable.",
                "Tap PvpPower for breakdown",
                infoVisible);
        }

        public void ShowState(string attacks, string league, string mapReset, string power, string hint, bool infoVisible)
        {
            if (_attacks != null) _attacks.text = attacks;
            if (_league != null) _league.text = league;
            if (_mapReset != null) _mapReset.text = mapReset;
            if (_power != null) _power.text = power;
            if (_hint != null) _hint.text = hint;
            SetPowerInfoVisible(infoVisible);
        }

        public void SetPowerInfoVisible(bool visible)
        {
            if (_powerTooltip != null) _powerTooltip.gameObject.SetActive(visible);
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
