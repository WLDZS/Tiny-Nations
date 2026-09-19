#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using BorFramework;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameLogic.Units
{
    internal sealed class UnitSpawnPanel
    {
        private const string DefinitionSuffix = "Definition";
        private const string ExpandedWindowTitle = "单位生成调试";
        private const int ExpandedWindowId = 31001;
        private const int CollapsedWindowId = 31002;
        private const float ExpandedWindowMaxWidth = 420f;
        private const float ExpandedWindowMaxHeight = 560f;
        private const float CollapsedWindowMaxWidth = 190f;
        private const float CollapsedWindowMaxHeight = 36f;
        private const float WindowMargin = 8f;
        private const float TitleBarHeight = 22f;
        private const float ToggleButtonWidth = 48f;
        private const float ToggleButtonMargin = 4f;
        private const float SpawnSpacing = 1.5f;
        private const int SpawnColumns = 4;

        private readonly List<string> _definitionAddresses = new();
        private readonly IResourceModule _resourceModule;
        private readonly IUnitSystem _unitSystem;
        private Rect _windowRect = new(8f, 8f, 170f, 36f);
        private Rect _collapsedWindowRect = new(8f, 8f, 190f, 36f);
        private Vector2 _scrollPosition;
        private GUIStyle _collapsedLabelStyle;
        private GUIStyle _statusStyle;
        private bool _started;
        private bool _disposed;
        private bool _isExpanded = true;
        private bool _isRefreshing;
        private bool _isSpawning;
        private bool _usePlayerInput;
        private string _teamIdText = "2";
        private int _spawnCount;
        private string _statusMessage = string.Empty;

        public UnitSpawnPanel(IResourceModule resourceModule, IUnitSystem unitSystem)
        {
            _resourceModule = resourceModule;
            _unitSystem = unitSystem;
        }

        private GUIStyle CollapsedLabelStyle
        {
            get
            {
                if (_collapsedLabelStyle != null)
                    return _collapsedLabelStyle;

                _collapsedLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold,
                    fontSize = 13
                };
                return _collapsedLabelStyle;
            }
        }

        private GUIStyle StatusStyle
        {
            get
            {
                if (_statusStyle != null)
                    return _statusStyle;

                _statusStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true
                };
                return _statusStyle;
            }
        }

        public void Start()
        {
            if (_started || _disposed || _resourceModule == null || _unitSystem == null)
                return;

            _started = true;
            RefreshUnitListAsync().Forget();
        }

        public void Draw()
        {
            if (!_started || _disposed)
                return;

            if (!_isExpanded)
            {
                DrawCollapsedWindow();
                return;
            }

            Vector2 windowSize = GetExpandedWindowSize();
            _windowRect.width = windowSize.x;
            _windowRect.height = windowSize.y;
            ClampWindowRect();

            _windowRect = GUI.Window(
                ExpandedWindowId,
                _windowRect,
                DrawWindow,
                ExpandedWindowTitle);

            ClampWindowRect();
        }

        public void Dispose()
        {
            _disposed = true;
            _started = false;
        }

        private void DrawWindow(int windowId)
        {
            Rect toggleButtonRect = new(
                _windowRect.width - ToggleButtonWidth - ToggleButtonMargin,
                ToggleButtonMargin,
                ToggleButtonWidth,
                TitleBarHeight - ToggleButtonMargin * 2f);

            if (GUI.Button(toggleButtonRect, "收起"))
            {
                _collapsedWindowRect.position = _windowRect.position;
                _isExpanded = false;
                return;
            }

            GUILayout.Space(2f);
            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                GUILayout.ExpandHeight(true));

            DrawToolbar();
            _usePlayerInput = GUILayout.Toggle(_usePlayerInput, "生成单位使用玩家输入");
            GUILayout.BeginHorizontal();
            GUILayout.Label("TeamId", GUILayout.Width(60f));
            _teamIdText = GUILayout.TextField(_teamIdText);
            GUILayout.EndHorizontal();
            GUILayout.Label($"下一个生成位置：{GetNextSpawnPosition()}");
            GUILayout.Space(4f);

            if (!_isRefreshing && _definitionAddresses.Count == 0)
                GUILayout.Label("没有找到带 UnitDefinition 标签的单位资源。");

            bool previousEnabled = GUI.enabled;
            for (int i = 0; i < _definitionAddresses.Count; i++)
            {
                string definitionAddress = _definitionAddresses[i];
                GUI.enabled = previousEnabled && !_isSpawning;

                if (GUILayout.Button($"生成 {GetDisplayName(definitionAddress)}"))
                    SpawnAsync(definitionAddress).Forget();
            }

            GUI.enabled = previousEnabled;
            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                GUILayout.Space(6f);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(_statusMessage, StatusStyle);
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(
                0f,
                0f,
                _windowRect.width - ToggleButtonWidth - ToggleButtonMargin * 2f,
                TitleBarHeight));
        }

        private void DrawCollapsedWindow()
        {
            Vector2 windowSize = GetCollapsedWindowSize();
            _collapsedWindowRect.width = windowSize.x;
            _collapsedWindowRect.height = windowSize.y;
            ClampCollapsedWindowRect();

            _collapsedWindowRect = GUI.Window(
                CollapsedWindowId,
                _collapsedWindowRect,
                DrawCollapsedWindowContents,
                GUIContent.none,
                GUIStyle.none);

            ClampCollapsedWindowRect();
        }

        private void DrawCollapsedWindowContents(int windowId)
        {
            GUI.Box(
                new Rect(0f, 0f, _collapsedWindowRect.width, _collapsedWindowRect.height),
                GUIContent.none);

            float expandButtonWidth = Mathf.Min(
                ToggleButtonWidth,
                Mathf.Max(1f, _collapsedWindowRect.width * 0.35f));
            Rect expandButtonRect = new(
                _collapsedWindowRect.width - expandButtonWidth - ToggleButtonMargin,
                ToggleButtonMargin,
                expandButtonWidth,
                _collapsedWindowRect.height - ToggleButtonMargin * 2f);
            Rect dragRect = new(
                ToggleButtonMargin,
                ToggleButtonMargin,
                Mathf.Max(1f, expandButtonRect.x - ToggleButtonMargin * 2f),
                _collapsedWindowRect.height - ToggleButtonMargin * 2f);

            GUI.Label(dragRect, GetCollapsedButtonText(), CollapsedLabelStyle);
            if (GUI.Button(expandButtonRect, "展开"))
            {
                _windowRect.position = _collapsedWindowRect.position;
                _isExpanded = true;
                return;
            }

            GUI.DragWindow(dragRect);
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"已收集单位：{_definitionAddresses.Count}");
            GUILayout.FlexibleSpace();

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !_isRefreshing;
            if (GUILayout.Button(
                    _isRefreshing ? "读取中..." : "刷新",
                    GUILayout.Width(72f)))
            {
                RefreshUnitListAsync().Forget();
            }

            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();
        }

        private string GetCollapsedButtonText()
        {
            if (_isRefreshing)
                return "单位生成（读取中）";

            if (_isSpawning)
                return "单位生成（生成中）";

            return $"单位生成（{_definitionAddresses.Count}）";
        }

        private Vector2 GetExpandedWindowSize()
        {
            float availableWidth = Mathf.Max(1f, Screen.width - WindowMargin * 2f);
            float availableHeight = Mathf.Max(1f, Screen.height - WindowMargin * 2f);

            return new Vector2(
                Mathf.Min(ExpandedWindowMaxWidth, availableWidth),
                Mathf.Min(ExpandedWindowMaxHeight, availableHeight));
        }

        private Vector2 GetCollapsedWindowSize()
        {
            float availableWidth = Mathf.Max(1f, Screen.width - WindowMargin * 2f);
            float availableHeight = Mathf.Max(1f, Screen.height - WindowMargin * 2f);

            return new Vector2(
                Mathf.Min(CollapsedWindowMaxWidth, availableWidth),
                Mathf.Min(CollapsedWindowMaxHeight, availableHeight));
        }

        private void ClampWindowRect()
        {
            float maxX = Mathf.Max(WindowMargin, Screen.width - _windowRect.width - WindowMargin);
            float maxY = Mathf.Max(WindowMargin, Screen.height - _windowRect.height - WindowMargin);
            _windowRect.x = Mathf.Clamp(_windowRect.x, WindowMargin, maxX);
            _windowRect.y = Mathf.Clamp(_windowRect.y, WindowMargin, maxY);
        }

        private void ClampCollapsedWindowRect()
        {
            float maxX = Mathf.Max(
                WindowMargin,
                Screen.width - _collapsedWindowRect.width - WindowMargin);
            float maxY = Mathf.Max(
                WindowMargin,
                Screen.height - _collapsedWindowRect.height - WindowMargin);
            _collapsedWindowRect.x = Mathf.Clamp(
                _collapsedWindowRect.x,
                WindowMargin,
                maxX);
            _collapsedWindowRect.y = Mathf.Clamp(
                _collapsedWindowRect.y,
                WindowMargin,
                maxY);
        }

        private async UniTask RefreshUnitListAsync()
        {
            if (_disposed || _isRefreshing)
                return;

            _isRefreshing = true;
            _statusMessage = "正在读取 YooAsset 单位清单...";
            IReadOnlyList<string> addresses =
                await _resourceModule.GetAssetAddressesAsync(UnitDefinition.ResourceTag);

            if (_disposed)
                return;

            _definitionAddresses.Clear();
            for (int i = 0; i < addresses.Count; i++)
                _definitionAddresses.Add(addresses[i]);

            _isRefreshing = false;
            _statusMessage = _definitionAddresses.Count > 0
                ? $"已加载 {_definitionAddresses.Count} 个单位。"
                : _resourceModule.State == EResourceState.Ready
                    ? "单位清单为空，请检查 YooAsset Collector 标签。"
                    : $"YooAsset 尚未可用：{_resourceModule.State}";
        }

        private async UniTask SpawnAsync(string definitionAddress)
        {
            if (_disposed || _isSpawning)
                return;

            if (!int.TryParse(_teamIdText, out int teamId))
            {
                _statusMessage = "生成失败：TeamId 必须是整数。";
                return;
            }

            _isSpawning = true;
            _statusMessage = $"正在生成 {GetDisplayName(definitionAddress)}...";
            Vector3 spawnPosition = GetNextSpawnPosition();
            var spawnRequest = new UnitSpawnRequest(
                definitionAddress,
                spawnPosition,
                Quaternion.identity,
                teamId,
                _usePlayerInput);
            UnitEntity unit = await _unitSystem.SpawnAsync(spawnRequest);

            if (_disposed)
            {
                if (unit != null)
                    _unitSystem.Despawn(unit);

                return;
            }

            _isSpawning = false;
            if (unit == null)
            {
                _statusMessage = $"生成失败：{definitionAddress}";
                return;
            }

            _spawnCount++;
            _statusMessage = BuildSpawnStatus(
                unit,
                GetDisplayName(definitionAddress));
        }

        private static string BuildSpawnStatus(UnitEntity unit, string displayName)
        {
            string status = $"已生成 {displayName}。";

            if (unit.TryGetTeamId(out int teamId))
                status += $"\nTeamId：{teamId}";

            if (unit.TryGetAttributeCurrentValue(
                    EUnitAttributeType.Health,
                    out float health)
                && unit.TryGetAttributeCurrentValue(
                    EUnitAttributeType.MaxHealth,
                    out float maxHealth))
            {
                status += $"\nHealth：{health:0.##} / {maxHealth:0.##}";
            }

            if (unit.TryGetAttributeBaseValue(
                    EUnitAttributeType.MoveSpeed,
                    out float baseMoveSpeed)
                && unit.TryGetAttributeCurrentValue(
                    EUnitAttributeType.MoveSpeed,
                    out float currentMoveSpeed))
            {
                status +=
                    $"\nMoveSpeed Base / Current：{baseMoveSpeed:0.##} / {currentMoveSpeed:0.##}";
            }

            if (unit.TryGetAttributeCurrentValue(
                    EUnitAttributeType.Mana,
                    out float mana)
                && unit.TryGetAttributeCurrentValue(
                    EUnitAttributeType.MaxMana,
                    out float maxMana))
            {
                status += $"\nMana：{mana:0.##} / {maxMana:0.##}";
            }

            return status;
        }

        private Vector3 GetNextSpawnPosition()
        {
            int column = _spawnCount % SpawnColumns;
            int row = _spawnCount / SpawnColumns;
            return new Vector3(2f + column * SpawnSpacing, -row * SpawnSpacing, 0f);
        }

        private static string GetDisplayName(string definitionAddress)
        {
            if (definitionAddress.EndsWith(
                    DefinitionSuffix,
                    System.StringComparison.Ordinal))
            {
                return definitionAddress.Substring(
                    0,
                    definitionAddress.Length - DefinitionSuffix.Length);
            }

            return definitionAddress;
        }
    }
}
#endif
