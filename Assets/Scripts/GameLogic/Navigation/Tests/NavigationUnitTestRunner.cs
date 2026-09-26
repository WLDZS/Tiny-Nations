using System;
using BorFramework;
using Cysharp.Threading.Tasks;
using GameLogic.Units;
using GameLogic.Units.Common;
using UnityEngine;

namespace GameLogic.Navigation
{
    /// <summary>Runs movement checks through the real UnitSystem, AI, navigation and Rigidbody2D chain.</summary>
    internal sealed class NavigationUnitTestRunner : IDisposable
    {
        private const string WarriorDefinitionAddress = "WarriorBlueDefinition";
        private const string SpiderDefinitionAddress = "SpiderDefinition";
        private const float AttackArrivalDistance = 1.6f;

        private static readonly NavigationUnitTestScenario[] Scenarios =
        {
            new("WarriorBlue · 开放区域", WarriorDefinitionAddress,
                new Vector3(-9.5f, 5.5f, 0f), new Vector3(-5.5f, 5.5f, 0f),
                1.5f, 7f),
            new("WarriorBlue · 绕开中央障碍", WarriorDefinitionAddress,
                new Vector3(-3.5f, 2.5f, 0f), new Vector3(3.5f, 2.5f, 0f),
                8f, 12f),
            new("WarriorBlue · 一格通道", WarriorDefinitionAddress,
                new Vector3(4.5f, 3.25f, 0f), new Vector3(9.5f, 3.25f, 0f),
                2f, 8f),
            new("WarriorBlue · 障碍旁有其他单位", WarriorDefinitionAddress,
                new Vector3(-3.5f, 0.5f, 0f), new Vector3(3.5f, 0.5f, 0f),
                3f, 9f, new Vector3(-2.25f, 2.5f, 0f)),
            new("Spider · 大半径绕障", SpiderDefinitionAddress,
                new Vector3(-3.5f, 2.5f, 0f), new Vector3(3.5f, 2.5f, 0f),
                8f, 14f),
            new("WarriorBlue · 散障碍区", WarriorDefinitionAddress,
                new Vector3(16.5f, 6.5f, 0f), new Vector3(23.5f, 9.5f, 0f),
                7f, 20f),
            new("WarriorBlue · 曲折通道", WarriorDefinitionAddress,
                new Vector3(18.5f, 0.5f, 0f), new Vector3(25.5f, 0.5f, 0f),
                9f, 25f),
            new("WarriorBlue · 死胡同出口", WarriorDefinitionAddress,
                new Vector3(21.5f, -9.5f, 0f), new Vector3(28.5f, -9.5f, 0f),
                6.5f, 20f)
        };

        private IUnitSystem _unitSystem;
        private UnitEntity _mover;
        private UnitEntity _target;
        private UnitEntity _bystander;
        private int _version;
        private int _scenarioIndex = -1;
        private bool _disposed;
        private bool _running;
        private bool _seenFollowing;
        private bool _seenSuccessfulQuery;
        private bool _hasMoverSample;
        private float _elapsedSeconds;
        private float _distanceTravelled;
        private Vector2 _moverPosition;
        private Vector2 _previousMoverPosition;
        private Vector2 _targetPosition;
        private Vector2? _bystanderPosition;
        private EPathQueryStatus? _lastPathQueryStatus;
        private string _status = "选择一个单位用例；目标单位保持静止，AI 单位会自行寻路。";

        public int ScenarioCount => Scenarios.Length;

        public string Status => _status;

        public bool HasPair => _mover != null && _target != null;

        public float ElapsedSeconds => _elapsedSeconds;

        public float DistanceTravelled => _distanceTravelled;

        public Vector2 MoverPosition => _moverPosition;

        public Vector2 TargetPosition => _targetPosition;

        public Vector2? BystanderPosition => _bystanderPosition;

        public string NavigationState => _mover?.Navigation?.State.ToString() ?? "未生成";

        public string PathQueryStatus => _lastPathQueryStatus?.ToString() ?? "未查询";

        public string BlockReason => _mover?.Navigation?.BlockReason.ToString() ?? "无";

        public string GetScenarioName(int index)
        {
            return index >= 0 && index < Scenarios.Length ? Scenarios[index].Name : string.Empty;
        }

        public Vector2 GetScenarioFocus(int index)
        {
            if (index < 0 || index >= Scenarios.Length)
                return Vector2.zero;

            NavigationUnitTestScenario scenario = Scenarios[index];
            Vector3 midpoint = (scenario.MoverStart + scenario.TargetPosition) * 0.5f;
            return new Vector2(midpoint.x, midpoint.y);
        }

        public void StartScenario(int index)
        {
            if (_disposed || index < 0 || index >= Scenarios.Length)
                return;

            Reset();
            IGameSystemModule module = GameHub.Ins.GetModule<IGameSystemModule>();
            _unitSystem = module?.GetSystem<IUnitSystem>();
            if (_unitSystem == null)
            {
                _status = "单位系统尚未启动。请从主菜单进入导航测试场景。";
                return;
            }

            _scenarioIndex = index;
            _status = $"正在生成 {Scenarios[index].Name} 的单位...";
            SpawnScenarioAsync(_version, _unitSystem).Forget();
        }

        public void Tick(float dt)
        {
            if (!_running || _mover == null || _target == null || _unitSystem == null)
                return;

            _elapsedSeconds += Mathf.Max(0f, dt);
            if (!_unitSystem.TryGetUnitWorldPosition(_mover, out Vector3 moverPosition)
                || !_unitSystem.TryGetUnitWorldPosition(_target, out Vector3 targetPosition))
            {
                Finish("失败：单位已从场景中消失，未完成移动。");
                return;
            }

            _moverPosition = moverPosition;
            _targetPosition = targetPosition;
            if (_bystander != null)
            {
                if (!_unitSystem.TryGetUnitWorldPosition(_bystander, out Vector3 bystanderPosition))
                {
                    Finish("失败：障碍旁的单位已从场景中消失。");
                    return;
                }

                _bystanderPosition = bystanderPosition;
            }

            if (_hasMoverSample)
                _distanceTravelled += Vector2.Distance(_previousMoverPosition, _moverPosition);

            _previousMoverPosition = _moverPosition;
            _hasMoverSample = true;
            UnitNavigationComp navigation = _mover.Navigation;
            if (navigation == null)
            {
                Finish("失败：移动单位没有装配 UnitNavigationLogic。");
                return;
            }

            if (navigation.State == EUnitNavigationState.Following)
                _seenFollowing = true;

            if (navigation.LastPathQueryStatus.HasValue)
                _lastPathQueryStatus = navigation.LastPathQueryStatus;

            if (_lastPathQueryStatus == EPathQueryStatus.Success)
                _seenSuccessfulQuery = true;

            if (navigation.State == EUnitNavigationState.Blocked)
            {
                Finish($"失败：导航阻塞 {navigation.LastPathQueryStatus} / {navigation.BlockReason}。");
                return;
            }

            NavigationUnitTestScenario scenario = Scenarios[_scenarioIndex];
            float targetDistance = Vector2.Distance(_moverPosition, _targetPosition);
            if (_seenFollowing
                && _seenSuccessfulQuery
                && _distanceTravelled >= scenario.MinimumTravelDistance
                && targetDistance <= AttackArrivalDistance)
            {
                Finish(
                    $"通过：行走 {_distanceTravelled:0.##}，距目标 {targetDistance:0.##}，"
                    + $"用时 {_elapsedSeconds:0.##} 秒。");
                return;
            }

            if (_elapsedSeconds >= scenario.TimeoutSeconds)
            {
                Finish(
                    $"失败：超时，行走 {_distanceTravelled:0.##}，距目标 {targetDistance:0.##}，"
                    + $"导航 {navigation.State} / {navigation.LastPathQueryStatus}。");
            }
        }

        public void Reset()
        {
            _version++;
            _running = false;
            _seenFollowing = false;
            _seenSuccessfulQuery = false;
            _hasMoverSample = false;
            _elapsedSeconds = 0f;
            _distanceTravelled = 0f;
            _scenarioIndex = -1;
            _lastPathQueryStatus = null;
            _bystanderPosition = null;

            ReleaseUnits();
            _unitSystem = null;
            _status = "单位已清理。";
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Reset();
            _disposed = true;
        }

        private async UniTask SpawnScenarioAsync(int version, IUnitSystem unitSystem)
        {
            NavigationUnitTestScenario scenario = Scenarios[_scenarioIndex];
            UnitEntity target = await unitSystem.SpawnAsync(
                new UnitSpawnRequest(WarriorDefinitionAddress,
                    scenario.TargetPosition, Quaternion.identity, 2, true));
            if (!IsCurrent(version))
            {
                ReleaseLateUnit(unitSystem, target);
                return;
            }

            if (target == null)
            {
                FailSpawn("目标 WarriorBlue 生成失败。请检查 YooAsset 资源。");
                return;
            }

            _target = target;
            if (scenario.BystanderPosition.HasValue)
            {
                UnitEntity bystander = await unitSystem.SpawnAsync(
                    new UnitSpawnRequest(WarriorDefinitionAddress,
                        scenario.BystanderPosition.Value, Quaternion.identity, 1, true));
                if (!IsCurrent(version))
                {
                    ReleaseLateUnit(unitSystem, bystander);
                    return;
                }

                if (bystander == null)
                {
                    FailSpawn("障碍旁的单位生成失败。");
                    return;
                }

                _bystander = bystander;
                _bystanderPosition = scenario.BystanderPosition.Value;
            }

            UnitEntity mover = await unitSystem.SpawnAsync(
                new UnitSpawnRequest(scenario.MoverDefinitionAddress,
                    scenario.MoverStart, Quaternion.identity, 1, false));
            if (!IsCurrent(version))
            {
                ReleaseLateUnit(unitSystem, mover);
                return;
            }

            if (mover == null)
            {
                FailSpawn($"移动单位 {scenario.MoverDefinitionAddress} 生成失败。");
                return;
            }

            _mover = mover;
            _moverPosition = scenario.MoverStart;
            _previousMoverPosition = _moverPosition;
            _hasMoverSample = true;
            _targetPosition = scenario.TargetPosition;
            _running = true;
            _status = "运行中：观察单位路径、导航状态和到达目标的情况。";
        }

        private void FailSpawn(string message)
        {
            _running = false;
            ReleaseUnits();
            _status = message;
        }

        private void Finish(string message)
        {
            _running = false;
            _status = message;
        }

        private void ReleaseUnits()
        {
            if (_unitSystem != null)
            {
                if (_mover != null)
                    _unitSystem.Despawn(_mover);

                if (_bystander != null)
                    _unitSystem.Despawn(_bystander);

                if (_target != null)
                    _unitSystem.Despawn(_target);
            }

            _mover = null;
            _bystander = null;
            _target = null;
        }

        private bool IsCurrent(int version)
        {
            return !_disposed && version == _version;
        }

        private static void ReleaseLateUnit(IUnitSystem unitSystem, UnitEntity unit)
        {
            if (unit != null)
                unitSystem.Despawn(unit);
        }
    }
}
