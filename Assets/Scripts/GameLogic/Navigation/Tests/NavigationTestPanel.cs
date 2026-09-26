using System.Collections.Generic;
using System.Diagnostics;
using BorFramework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace GameLogic.Navigation
{
    /// <summary>Runs repeatable path queries against the NavigationTest scene fixture.</summary>
    public sealed class NavigationTestPanel : MonoBehaviour
    {
        private const int WindowId = 32001;
        private const float WindowWidth = 350f;
        private const float WindowMargin = 8f;
        private const float SampleSpacing = 0.1f;
        private const float EndpointTolerance = 0.01f;

        private static readonly Color PathColor = new(1f, 0.85f, 0.15f, 1f);
        private static readonly Color StartColor = new(0.1f, 1f, 0.35f, 1f);
        private static readonly Color DestinationColor = new(1f, 0.25f, 0.2f, 1f);
        private static readonly Color ReachedColor = new(0.2f, 0.9f, 1f, 1f);

        private static readonly TestCase[] Cases =
        {
            new("01 直线通行", -10, 5, -6, 5, EPathQueryStatus.Success, 0.2f),
            new("02 同格精确终点", -10, 5, -10, 5, EPathQueryStatus.Success, 0.2f)
            {
                StartOffset = new Vector2(-0.2f, -0.2f),
                DestinationOffset = new Vector2(0.2f, 0.2f)
            },
            new("03 绕开矩形障碍", -8, 2, 3, 2, EPathQueryStatus.Success, 0.2f)
            {
                MinimumDetourRatio = 1.05f
            },
            new("04 窄通道可通行", 4, 3, 10, 3, EPathQueryStatus.Success, 0.2f),
            new("05 窄通道净空不足", 4, 3, 10, 3, EPathQueryStatus.InvalidStart, 0.55f),
            new("06 对角禁止穿角", -5, -3, -4, -2, EPathQueryStatus.Success, 0.2f)
            {
                MinimumDetourRatio = 1.2f
            },
            new("07 障碍起点", 0, 2, -8, 2, EPathQueryStatus.InvalidStart, 0.2f),
            new("08 障碍终点", -8, 2, 0, 2, EPathQueryStatus.InvalidDestination, 0.2f),
            new("09 封闭区域不可达", 0, -4, -9, -4, EPathQueryStatus.NoPath, 0.2f),
            new("10 靠近障碍目标", 0, -4, -7, -4, EPathQueryStatus.Approach, 0.2f)
            {
                UseApproach = true
            },
            new("11 地图外终点", -11, -7, 12, 7, EPathQueryStatus.InvalidDestination, 0.2f),
            new("12 导航锚点偏移", -10, 5, -6, 5, EPathQueryStatus.Success, 0.2f)
            {
                AnchorOffset = new Vector2(0.2f, 0.1f)
            },
            new("13 障碍边缘查询 A", -3, 0, 3, 0, EPathQueryStatus.Success, 0.2f),
            new("14 障碍边缘查询 B", 3, 0, -3, 0, EPathQueryStatus.Success, 0.2f),
            new("15 散障碍区绕行", 15, 6, 28, 10, EPathQueryStatus.Success, 0.2f)
            {
                MinimumDetourRatio = 1.05f
            },
            new("16 曲折通道折返", 16, 0, 27, 0, EPathQueryStatus.Success, 0.2f)
            {
                MinimumDetourRatio = 1.1f
            },
            new("17 死胡同寻找出口", 21, -9, 28, -9, EPathQueryStatus.Success, 0.2f)
            {
                MinimumDetourRatio = 1.15f
            },
            new("18 跨独立区域不可达", 10, 6, 15, 6, EPathQueryStatus.NoPath, 0.2f),
            new("19 散障碍旁净空不足", 17, 6, 28, 10, EPathQueryStatus.InvalidStart, 0.55f),
            new("20 靠近散障碍目标", 15, 7, 18, 7, EPathQueryStatus.Approach, 0.2f)
            {
                UseApproach = true
            }
        };

        [SerializeField] private Tilemap _groundTilemap;
        [SerializeField] private Tilemap _collisionTilemap;

        private readonly TestResult[] _results = new TestResult[Cases.Length];
        private readonly List<Vector2> _customWaypoints = new();
        private readonly NavigationUnitTestRunner _unitTests = new();
        private readonly NavigationMultiUnitTestRunner _multiUnitTests = new();
        private INavigationSystem _navigationSystem;
        private Collider2D _collisionCollider;
        private GUIStyle _resultStyle;
        private GUIStyle _markerStyle;
        private Texture2D _markerTexture;
        private Rect _windowRect;
        private Vector2 _scrollPosition;
        private Vector2 _customStart;
        private Vector2 _customDestination;
        private Vector2 _customReached;
        private EPathQueryStatus _customStatus;
        private float _customRadius = 0.2f;
        private int _selectedCase;
        private int _activeTab;
        private bool _hasCustomResult;
        private bool _showCustomResult;
        private bool _useApproach;
        private bool _pickStart;
        private bool _pickDestination;
        private bool _showMultiUnitTest;
        private string _message = "点击“运行全部”执行固定用例。";

        private void Awake()
        {
            if (_collisionTilemap != null)
                _collisionCollider = _collisionTilemap.GetComponent<Collider2D>();

            if (_groundTilemap != null)
            {
                _customStart = CellCenter(-10, 5);
                _customDestination = CellCenter(-6, 5);
            }
        }

        private void Update()
        {
            _unitTests.Tick(Time.deltaTime);
            _multiUnitTests.Tick(Time.deltaTime);
            if (_activeTab != 0)
                return;

            if (!_pickStart && !_pickDestination)
                return;

            Mouse mouse = Mouse.current;
            Camera camera = Camera.main;
            if (mouse == null || camera == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            Vector2 mousePosition = mouse.position.ReadValue();
            Vector2 guiPosition = new(mousePosition.x, Screen.height - mousePosition.y);
            if (_windowRect.Contains(guiPosition))
                return;

            float distance = -camera.transform.position.z;
            Vector3 worldPosition = camera.ScreenToWorldPoint(
                new Vector3(mousePosition.x, mousePosition.y, distance));
            if (_pickStart)
                _customStart = worldPosition;
            else
                _customDestination = worldPosition;

            _pickStart = false;
            _pickDestination = false;
            _hasCustomResult = false;
            _showCustomResult = true;
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Repaint)
                DrawWorldOverlay();

            float width = Mathf.Min(WindowWidth, Screen.width - WindowMargin * 2f);
            float height = Mathf.Min(650f, Screen.height - WindowMargin * 2f);
            if (_windowRect.width <= 0f)
                _windowRect.position = new Vector2(Screen.width - width - WindowMargin, WindowMargin);

            _windowRect.width = width;
            _windowRect.height = height;
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Screen.width - width);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Screen.height - height);
            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindow, "导航寻路测试");
        }

        private void OnDestroy()
        {
            _unitTests.Dispose();
            _multiUnitTests.Dispose();
            if (_markerTexture != null)
                Destroy(_markerTexture);
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("寻路查询"))
            {
                _activeTab = 0;
                _scrollPosition = Vector2.zero;
            }

            if (GUILayout.Button("实际单位"))
            {
                _activeTab = 1;
                _scrollPosition = Vector2.zero;
                _pickStart = false;
                _pickDestination = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("原地图"))
                FocusOn(Vector2.zero);
            if (GUILayout.Button("散障碍"))
                FocusOn(new Vector2(21.5f, 8f));
            if (GUILayout.Button("曲折通道"))
                FocusOn(new Vector2(21.5f, 0f));
            if (GUILayout.Button("死胡同"))
                FocusOn(new Vector2(21.5f, -8f));
            GUILayout.EndHorizontal();

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            if (_activeTab == 1)
            {
                DrawUnitTests();
                GUILayout.EndScrollView();
                GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 24f));
                return;
            }

            GUILayout.Label("固定地图：开放 / 绕障 / 窄口 / 散障碍 / 折返 / 死胡同");
            GUILayout.Label("绿色起点 · 红色目标 · 黄色路径 · 蓝色靠近终点");

            if (GUILayout.Button("运行全部固定用例"))
                RunAll();

            if (GUILayout.Button("连续回归 20 轮"))
                RunRepeated();

            int passed = 0;
            int executed = 0;
            for (int index = 0; index < _results.Length; index++)
            {
                if (_results[index] == null)
                    continue;

                executed++;
                if (_results[index].Passed)
                    passed++;
            }

            GUILayout.Label($"结果：{passed}/{executed} 通过");
            GUILayout.Label(_message, GetResultStyle());

            for (int index = 0; index < Cases.Length; index++)
            {
                TestResult result = _results[index];
                string prefix = result == null ? "[ ]" : result.Passed ? "[OK]" : "[X]";
                if (GUILayout.Button($"{prefix} {Cases[index].Name}"))
                {
                    _selectedCase = index;
                    _showCustomResult = false;
                    RunCase(index);
                    if (Cases[index].StartCell.x >= 14)
                        FocusOn(new Vector2(21.5f, Cases[index].StartCell.y));
                    else if (Cases[index].DestinationCell.x >= 14)
                        FocusOn(new Vector2(11.5f, Cases[index].StartCell.y));
                    else
                        FocusOn(Vector2.zero);
                }
            }

            TestResult selected = _results[_selectedCase];
            if (!_showCustomResult && selected != null)
            {
                GUILayout.Label(
                    $"预期 {selected.Expected} / 实际 {selected.Actual}  ·  "
                    + $"{selected.ElapsedMilliseconds:0.###} ms  ·  {selected.Waypoints.Count} 个路点");
                GUILayout.Label(selected.Detail, GetResultStyle());
            }

            GUILayout.Space(8f);
            GUILayout.Label("自由查询：点击按钮后在地图上点选坐标");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_pickStart ? "点击地图选起点…" : "设置起点"))
            {
                _pickStart = true;
                _pickDestination = false;
            }
            if (GUILayout.Button(_pickDestination ? "点击地图选终点…" : "设置终点"))
            {
                _pickDestination = true;
                _pickStart = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"起点 {_customStart:F2}  →  终点 {_customDestination:F2}");
            GUILayout.Label($"净空半径：{_customRadius:0.00} 世界单位");
            _customRadius = GUILayout.HorizontalSlider(_customRadius, 0f, 0.75f);
            _useApproach = GUILayout.Toggle(_useApproach, "目标不可达时尝试靠近");
            if (GUILayout.Button("查询当前坐标"))
                RunCustomQuery();

            if (_showCustomResult && _hasCustomResult)
                GUILayout.Label($"查询结果：{_customStatus}，{_customWaypoints.Count} 个路点");

            GUILayout.Label("切换“实际单位”可运行具体兵种的移动用例。");
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 24f));
        }

        private void DrawUnitTests()
        {
            GUILayout.Label("使用真实 UnitSystem、近战 AI、导航逻辑和 Rigidbody2D。");
            GUILayout.Label("目标单位保持静止；运行期间不要操作移动键。", GetResultStyle());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("20 单位 → 同一目标"))
                StartMultiUnitTest(false);
            if (GUILayout.Button("20 单位 → 不同目标"))
                StartMultiUnitTest(true);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            for (int index = 0; index < _unitTests.ScenarioCount; index++)
            {
                if (GUILayout.Button(_unitTests.GetScenarioName(index)))
                {
                    _multiUnitTests.Reset();
                    _showMultiUnitTest = false;
                    _unitTests.StartScenario(index);
                    FocusOn(_unitTests.GetScenarioFocus(index));
                }
            }

            if (GUILayout.Button("清理测试单位"))
            {
                _unitTests.Reset();
                _multiUnitTests.Reset();
                _showMultiUnitTest = false;
            }

            if (_showMultiUnitTest)
            {
                GUILayout.Label(_multiUnitTests.TargetCount > 1
                    ? "绿色单位 → T1；蓝色单位 → T2。"
                    : "绿色圆点为移动单位，红色 T1 为共同目标。");
                GUILayout.Label($"用时：{_multiUnitTests.ElapsedSeconds:0.##} 秒");
                GUILayout.Label(_multiUnitTests.Status, GetResultStyle());
                for (int index = 0; index < _multiUnitTests.UnitCount; index++)
                    GUILayout.Label(_multiUnitTests.GetMoverStatus(index), GetResultStyle());
                return;
            }

            GUILayout.Label(_unitTests.Status, GetResultStyle());
            if (!_unitTests.HasPair)
                return;

            GUILayout.Label($"用时：{_unitTests.ElapsedSeconds:0.##} 秒");
            GUILayout.Label($"累计行走：{_unitTests.DistanceTravelled:0.##} 世界单位");
            GUILayout.Label($"导航：{_unitTests.NavigationState} / {_unitTests.PathQueryStatus}");
            GUILayout.Label($"阻塞原因：{_unitTests.BlockReason}");
            GUILayout.Label($"移动单位 WordPos：{_unitTests.MoverPosition:F2}");
            GUILayout.Label($"目标单位 WordPos：{_unitTests.TargetPosition:F2}");
            if (_unitTests.BystanderPosition.HasValue)
                GUILayout.Label($"障碍旁单位 WordPos：{_unitTests.BystanderPosition.Value:F2}");
        }

        private void StartMultiUnitTest(bool useDifferentTargets)
        {
            _unitTests.Reset();
            _multiUnitTests.Start(useDifferentTargets);
            _showMultiUnitTest = true;
            _scrollPosition = Vector2.zero;
            if (useDifferentTargets)
                FocusOn(Vector2.zero);
            else
                FocusOn(new Vector2(-6f, 6f), 7f);
        }

        private void RunAll()
        {
            if (!TryGetNavigationSystem() || !ValidateFixture())
                return;

            int passed = 0;
            for (int index = 0; index < Cases.Length; index++)
            {
                RunCase(index);
                if (_results[index].Passed)
                    passed++;
            }

            _showCustomResult = false;
            _message = $"固定用例完成：{passed}/{Cases.Length} 通过。点击失败项查看原因。";
        }

        private void RunRepeated()
        {
            if (!TryGetNavigationSystem() || !ValidateFixture())
                return;

            const int roundCount = 20;
            int failed = 0;
            double longestMilliseconds = 0d;
            long startedAt = Stopwatch.GetTimestamp();
            for (int round = 0; round < roundCount; round++)
            {
                for (int index = 0; index < Cases.Length; index++)
                {
                    RunCase(index);
                    TestResult result = _results[index];
                    if (!result.Passed)
                        failed++;

                    longestMilliseconds = System.Math.Max(
                        longestMilliseconds, result.ElapsedMilliseconds);
                }
            }

            double totalMilliseconds = (Stopwatch.GetTimestamp() - startedAt) * 1000.0
                                       / Stopwatch.Frequency;
            _showCustomResult = false;
            _message = $"连续 {roundCount} 轮共 {roundCount * Cases.Length} 次查询，"
                       + $"失败 {failed} 次，总耗时 {totalMilliseconds:0.##} ms，"
                       + $"单次最长 {longestMilliseconds:0.###} ms。";
        }

        private void RunCase(int index)
        {
            if (!TryGetNavigationSystem() || !ValidateFixture())
                return;

            TestCase testCase = Cases[index];
            Vector2 start = CellCenter(testCase.StartCell.x, testCase.StartCell.y)
                            + testCase.StartOffset - testCase.AnchorOffset;
            Vector2 destination = CellCenter(testCase.DestinationCell.x, testCase.DestinationCell.y)
                                  + testCase.DestinationOffset - testCase.AnchorOffset;
            var waypoints = new List<Vector2>();
            Vector2 reportedReached = default;
            long startedAt = Stopwatch.GetTimestamp();
            EPathQueryStatus status = testCase.UseApproach
                ? _navigationSystem.FindApproachPath(
                    start, destination, testCase.AnchorOffset, testCase.Radius,
                    waypoints, out reportedReached)
                : _navigationSystem.FindPath(
                    start, destination, testCase.AnchorOffset, testCase.Radius, waypoints);
            double elapsedMilliseconds = (Stopwatch.GetTimestamp() - startedAt) * 1000.0
                                         / Stopwatch.Frequency;
            Vector2 reached = waypoints.Count > 0 ? waypoints[waypoints.Count - 1] : default;
            bool passed = CheckResult(
                testCase, start, destination, reached, reportedReached,
                status, waypoints, out string detail);
            _results[index] = new TestResult(
                testCase.Expected, status, start, destination, reached,
                waypoints, elapsedMilliseconds, detail, passed);
        }

        private void RunCustomQuery()
        {
            if (!TryGetNavigationSystem())
                return;

            _customStatus = _useApproach
                ? _navigationSystem.FindApproachPath(
                    _customStart, _customDestination, Vector2.zero,
                    _customRadius, _customWaypoints, out _customReached)
                : _navigationSystem.FindPath(
                    _customStart, _customDestination, Vector2.zero,
                    _customRadius, _customWaypoints);
            if (!_useApproach)
                _customReached = _customDestination;

            _hasCustomResult = true;
            _showCustomResult = true;
            _pickStart = false;
            _pickDestination = false;
        }

        private bool CheckResult(
            TestCase testCase,
            Vector2 start,
            Vector2 destination,
            Vector2 reached,
            Vector2 reportedReached,
            EPathQueryStatus status,
            List<Vector2> waypoints,
            out string detail)
        {
            if (status != testCase.Expected)
            {
                detail = $"状态不符：预期 {testCase.Expected}，实际 {status}。";
                return false;
            }

            if (status != EPathQueryStatus.Success && status != EPathQueryStatus.Approach)
            {
                detail = waypoints.Count == 0 ? "状态与空路径符合预期。" : "失败状态返回了非空路径。";
                return waypoints.Count == 0;
            }

            if (waypoints.Count == 0)
            {
                detail = "成功状态没有路点。";
                return false;
            }

            if (status == EPathQueryStatus.Success
                && Vector2.Distance(reached, destination) > EndpointTolerance)
            {
                detail = "成功路径没有到达精确目标坐标。";
                return false;
            }

            if (status == EPathQueryStatus.Approach
                && (Vector2.Distance(reached, destination) <= EndpointTolerance
                    || Vector2.Distance(reached, destination) > 3f))
            {
                detail = "靠近终点不在目标附近，或错误地等于障碍目标。";
                return false;
            }

            if (testCase.UseApproach
                && Vector2.Distance(reportedReached, reached) > EndpointTolerance)
            {
                detail = "靠近查询报告的终点与末路点不一致。";
                return false;
            }

            float pathLength = 0f;
            Vector2 from = start + testCase.AnchorOffset;
            for (int index = 0; index < waypoints.Count; index++)
            {
                Vector2 to = waypoints[index] + testCase.AnchorOffset;
                pathLength += Vector2.Distance(from, to);
                if (!IsSafeSegment(from, to, testCase.Radius))
                {
                    detail = $"第 {index + 1} 段路径穿过障碍或净空不足。";
                    return false;
                }

                from = to;
            }

            float directDistance = Vector2.Distance(start, destination);
            if (testCase.MinimumDetourRatio > 0f
                && pathLength < directDistance * testCase.MinimumDetourRatio)
            {
                detail = "路径没有按预期绕过障碍。";
                return false;
            }

            detail = $"状态、终点、路径净空均符合预期；长度 {pathLength:0.##}。";
            return true;
        }

        private bool IsSafeSegment(Vector2 from, Vector2 to, float radius)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / SampleSpacing));
            for (int index = 0; index <= sampleCount; index++)
            {
                Vector2 point = Vector2.Lerp(from, to, index / (float)sampleCount);
                Vector3Int cell = _groundTilemap.WorldToCell(point);
                if (!_groundTilemap.HasTile(cell) || _collisionTilemap.HasTile(cell))
                    return false;

                if (radius <= 0f || _collisionCollider == null)
                    continue;

                Vector2 closest = _collisionCollider.ClosestPoint(point);
                if (Vector2.Distance(closest, point) < radius + 0.015f)
                    return false;
            }

            return true;
        }

        private bool ValidateFixture()
        {
            if (_groundTilemap == null || _collisionTilemap == null || _collisionCollider == null)
            {
                _message = "测试场景缺少 Ground、Collision 或碰撞器引用。";
                return false;
            }

            int groundCount = CountTiles(_groundTilemap);
            int collisionCount = CountTiles(_collisionTilemap);
            if (groundCount == 720
                && collisionCount == 85
                && _collisionTilemap.HasTile(new Vector3Int(0, 2, 0))
                && _collisionTilemap.HasTile(new Vector3Int(-7, -4, 0))
                && !_collisionTilemap.HasTile(new Vector3Int(-9, -4, 0))
                && _groundTilemap.HasTile(new Vector3Int(-9, -4, 0))
                && _collisionTilemap.HasTile(new Vector3Int(18, 7, 0))
                && _collisionTilemap.HasTile(new Vector3Int(19, 0, 0))
                && _collisionTilemap.HasTile(new Vector3Int(25, -9, 0))
                && !_groundTilemap.HasTile(new Vector3Int(12, 6, 0)))
                return true;

            _message = $"固定地图已变化：Ground {groundCount}/720，Collision {collisionCount}/85，或关键格子不符。";
            return false;
        }

        private static void FocusOn(Vector2 position, float orthographicSize = 10f)
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            Vector3 current = camera.transform.position;
            camera.transform.position = new Vector3(position.x, position.y, current.z);
            camera.orthographicSize = orthographicSize;
        }

        private bool TryGetNavigationSystem()
        {
            IGameSystemModule module = GameHub.Ins.GetModule<IGameSystemModule>();
            _navigationSystem = module?.GetSystem<INavigationSystem>();
            if (_navigationSystem != null)
                return true;

            _message = "导航系统尚未启动。请从主菜单进入导航测试场景。";
            return false;
        }

        private Vector2 CellCenter(int x, int y)
        {
            return _groundTilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));
        }

        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.HasTile(cell))
                    count++;
            }

            return count;
        }

        private void DrawWorldOverlay()
        {
            Camera camera = Camera.main;
            if (camera == null || _groundTilemap == null)
                return;

            int oldDepth = GUI.depth;
            Color oldColor = GUI.color;
            GUI.depth = 49;

            if (_activeTab == 1)
            {
                if (_showMultiUnitTest)
                {
                    for (int index = 0; index < _multiUnitTests.UnitCount; index++)
                    {
                        if (_multiUnitTests.TryGetMoverPosition(
                                index, out Vector2 mover, out int targetIndex))
                        {
                            DrawMarker(camera, mover,
                                targetIndex == 0 ? StartColor : ReachedColor, string.Empty);
                        }
                    }

                    for (int index = 0; index < _multiUnitTests.TargetCount; index++)
                    {
                        if (_multiUnitTests.TryGetTargetPosition(index, out Vector2 target))
                            DrawMarker(camera, target, DestinationColor, $"T{index + 1}");
                    }
                }
                else if (_unitTests.HasPair)
                {
                    DrawMarker(camera, _unitTests.MoverPosition, StartColor, "M");
                    DrawMarker(camera, _unitTests.TargetPosition, DestinationColor, "T");
                    if (_unitTests.BystanderPosition.HasValue)
                        DrawMarker(camera, _unitTests.BystanderPosition.Value, ReachedColor, "B");
                }
            }
            else if (_showCustomResult)
            {
                DrawPath(camera, _customStart, _customDestination,
                    _customReached, _customWaypoints, _hasCustomResult);
            }
            else
            {
                TestResult result = _results[_selectedCase];
                if (result != null)
                    DrawPath(camera, result.Start, result.Destination,
                        result.Reached, result.Waypoints, true);
            }

            DrawMapLabel(camera, -8f, 6.5f, "开放区");
            DrawMapLabel(camera, 0f, 5.5f, "绕障区");
            DrawMapLabel(camera, 7f, 5.5f, "窄通道");
            DrawMapLabel(camera, -9f, -1f, "封闭区域");
            DrawMapLabel(camera, 21.5f, 10.5f, "散障碍区");
            DrawMapLabel(camera, 21.5f, 2.5f, "曲折通道");
            DrawMapLabel(camera, 21.5f, -5.5f, "死胡同");

            GUI.color = oldColor;
            GUI.depth = oldDepth;
        }

        private void DrawPath(
            Camera camera,
            Vector2 start,
            Vector2 destination,
            Vector2 reached,
            List<Vector2> waypoints,
            bool hasResult)
        {
            DrawMarker(camera, start, StartColor, "S");
            DrawMarker(camera, destination, DestinationColor, "T");
            if (!hasResult || waypoints.Count == 0)
                return;

            Vector2 from = start;
            for (int index = 0; index < waypoints.Count; index++)
            {
                DrawLine(WorldToGui(camera, from), WorldToGui(camera, waypoints[index]), 3f, PathColor);
                from = waypoints[index];
            }

            if (Vector2.Distance(reached, destination) > EndpointTolerance)
                DrawMarker(camera, reached, ReachedColor, "A");
        }

        private void DrawMarker(Camera camera, Vector2 world, Color color, string label)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
                return;

            if (_markerTexture == null)
                _markerTexture = CreateMarkerTexture();

            Vector2 point = new(screen.x, Screen.height - screen.y);
            GUI.color = color;
            GUI.DrawTexture(new Rect(point.x - 6f, point.y - 6f, 12f, 12f), _markerTexture);
            if (!string.IsNullOrEmpty(label))
                GUI.Label(new Rect(point.x + 7f, point.y - 12f, 24f, 20f), label, GetMarkerStyle());
        }

        private void DrawMapLabel(Camera camera, float x, float y, string label)
        {
            Vector2 point = WorldToGui(camera, new Vector2(x, y));
            GUI.color = Color.white;
            GUI.Label(new Rect(point.x - 40f, point.y - 10f, 100f, 20f), label, GetMarkerStyle());
        }

        private GUIStyle GetResultStyle()
        {
            if (_resultStyle == null)
                _resultStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };

            return _resultStyle;
        }

        private GUIStyle GetMarkerStyle()
        {
            if (_markerStyle == null)
            {
                _markerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 12
                };
                _markerStyle.normal.textColor = Color.white;
            }

            return _markerStyle;
        }

        private static Vector2 WorldToGui(Camera camera, Vector2 world)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        private static void DrawLine(Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
                return;

            Matrix4x4 oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, from);
            GUI.color = color;
            GUI.DrawTexture(new Rect(from.x, from.y - width * 0.5f, direction.magnitude, width),
                Texture2D.whiteTexture);
            GUI.matrix = oldMatrix;
        }

        private static Texture2D CreateMarkerTexture()
        {
            var texture = new Texture2D(12, 12, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    float dx = x - 5.5f;
                    float dy = y - 5.5f;
                    texture.SetPixel(x, y, dx * dx + dy * dy <= 30.25f ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return texture;
        }

        private sealed class TestCase
        {
            public string Name { get; }
            public Vector2Int StartCell { get; }
            public Vector2Int DestinationCell { get; }
            public EPathQueryStatus Expected { get; }
            public float Radius { get; }
            public Vector2 StartOffset { get; set; }
            public Vector2 DestinationOffset { get; set; }
            public Vector2 AnchorOffset { get; set; }
            public float MinimumDetourRatio { get; set; }
            public bool UseApproach { get; set; }

            public TestCase(
                string name,
                int startX,
                int startY,
                int destinationX,
                int destinationY,
                EPathQueryStatus expected,
                float radius)
            {
                Name = name;
                StartCell = new Vector2Int(startX, startY);
                DestinationCell = new Vector2Int(destinationX, destinationY);
                Expected = expected;
                Radius = radius;
            }
        }

        private sealed class TestResult
        {
            public EPathQueryStatus Expected { get; }
            public EPathQueryStatus Actual { get; }
            public Vector2 Start { get; }
            public Vector2 Destination { get; }
            public Vector2 Reached { get; }
            public List<Vector2> Waypoints { get; }
            public double ElapsedMilliseconds { get; }
            public string Detail { get; }
            public bool Passed { get; }

            public TestResult(
                EPathQueryStatus expected,
                EPathQueryStatus actual,
                Vector2 start,
                Vector2 destination,
                Vector2 reached,
                List<Vector2> waypoints,
                double elapsedMilliseconds,
                string detail,
                bool passed)
            {
                Expected = expected;
                Actual = actual;
                Start = start;
                Destination = destination;
                Reached = reached;
                Waypoints = waypoints;
                ElapsedMilliseconds = elapsedMilliseconds;
                Detail = detail;
                Passed = passed;
            }
        }
    }
}
