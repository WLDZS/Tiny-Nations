using System;
using BorFramework;
using Cysharp.Threading.Tasks;
using GameLogic.Units;
using GameLogic.Units.Common;
using UnityEngine;

namespace GameLogic.Navigation
{
    /// <summary>Runs twenty real units toward one or two stationary targets.</summary>
    internal sealed class NavigationMultiUnitTestRunner : IDisposable
    {
        private const string WarriorDefinitionAddress = "WarriorBlueDefinition";
        private const int MoverCount = 20;
        private const float MinimumTravelDistance = 0.75f;
        private const float NearbyDistance = 1.25f;
        private const float MinimumPairSeparation = 0.55f;
        private const float MinimumObservationSeconds = 4f;
        private const float TimeoutSeconds = 15f;

        private MoverProbe[] _movers = Array.Empty<MoverProbe>();
        private TargetProbe[] _targets = Array.Empty<TargetProbe>();
        private IUnitSystem _unitSystem;
        private int _version;
        private int _spawnedMoverCount;
        private bool _disposed;
        private bool _running;
        private bool _spawningTargets;
        private float _elapsedSeconds;
        private string _status = "选择 20 单位共同目标或不同目标测试。";

        public int UnitCount => _movers.Length;

        public int TargetCount => _targets.Length;

        public float ElapsedSeconds => _elapsedSeconds;

        public string Status => _status;

        public void Start(bool useDifferentTargets)
        {
            if (_disposed)
                return;

            Reset();
            IGameSystemModule module = GameHub.Ins.GetModule<IGameSystemModule>();
            _unitSystem = module?.GetSystem<IUnitSystem>();
            if (_unitSystem == null)
            {
                _status = "单位系统尚未启动。请从主菜单进入导航测试场景。";
                return;
            }

            _targets = useDifferentTargets
                ? new[]
                {
                    new TargetProbe(new Vector3(-2.5f, 5.5f, 0f)),
                    new TargetProbe(new Vector3(3.5f, -5.5f, 0f))
                }
                : new[] { new TargetProbe(new Vector3(-2.5f, 5.5f, 0f)) };

            _movers = new MoverProbe[MoverCount];
            _spawningTargets = true;
            for (int index = 0; index < MoverCount; index++)
            {
                int column = index % 5;
                if (useDifferentTargets && index >= 10)
                {
                    int row = (index - 10) / 5;
                    _movers[index] = new MoverProbe(
                        new Vector3(-3.5f + column, -6.5f + row * 2f, 0f), 1);
                }
                else
                {
                    int row = index / 5;
                    float y = useDifferentTargets ? 4.5f + row * 2f : 4.5f + row;
                    _movers[index] = new MoverProbe(new Vector3(-9.5f + column, y, 0f), 0);
                }
            }

            _status = $"正在生成 0/{MoverCount} 名移动单位；全部生成后再放入目标。";
            SpawnUnitsAsync(_version, _unitSystem).Forget();
        }

        public void Tick(float dt)
        {
            if (!_running || _unitSystem == null)
                return;

            if (!_spawningTargets)
                _elapsedSeconds += Mathf.Max(0f, dt);

            for (int index = 0; index < _targets.Length; index++)
            {
                TargetProbe target = _targets[index];
                if (target.Unit == null)
                    continue;

                if (!_unitSystem.TryGetUnitWorldPosition(target.Unit, out Vector3 position))
                {
                    Fail($"目标 {index + 1} 已消失，无法继续测试。");
                    return;
                }

                target.WorldPosition = position;
            }

            int passedCount = 0;
            int blockedCount = 0;
            int nearbyCount = 0;
            for (int index = 0; index < _movers.Length; index++)
            {
                MoverProbe mover = _movers[index];
                if (_targets[mover.TargetIndex].Unit == null)
                    continue;

                if (!_unitSystem.TryGetUnitWorldPosition(mover.Unit, out Vector3 position))
                {
                    Fail($"移动单位 {index + 1} 已消失，无法继续测试。");
                    return;
                }

                mover.WorldPosition = position;
                mover.DistanceTravelled += Vector2.Distance(mover.PreviousPosition, mover.WorldPosition);
                mover.PreviousPosition = mover.WorldPosition;

                UnitNavigationComp navigation = mover.Unit.Navigation;
                if (navigation == null)
                {
                    Fail($"移动单位 {index + 1} 没有导航组件。");
                    return;
                }

                if (navigation.State == EUnitNavigationState.Following)
                    mover.SeenFollowing = true;

                if (navigation.LastPathQueryStatus.HasValue)
                    mover.LastPathQueryStatus = navigation.LastPathQueryStatus;

                if (mover.LastPathQueryStatus == EPathQueryStatus.Success)
                    mover.SeenSuccessfulQuery = true;

                if (navigation.State == EUnitNavigationState.Blocked)
                    blockedCount++;

                float targetDistance = Vector2.Distance(
                    mover.WorldPosition, _targets[mover.TargetIndex].WorldPosition);
                if (targetDistance <= NearbyDistance)
                    nearbyCount++;

                if (mover.SeenFollowing
                    && mover.SeenSuccessfulQuery
                    && mover.DistanceTravelled >= MinimumTravelDistance)
                {
                    mover.Passed = true;
                }

                if (mover.Passed)
                    passedCount++;
            }

            if (_spawningTargets)
                return;

            int overlapCount = CountOverlaps();
            if (passedCount == MoverCount
                && nearbyCount >= 2
                && overlapCount == 0
                && blockedCount == 0
                && _elapsedSeconds >= MinimumObservationSeconds)
            {
                _running = false;
                _status = $"通过：20/20 名单位已移动，目标附近 {nearbyCount} 名，重叠 0 对，用时 {_elapsedSeconds:0.##} 秒。";
            }
            else if (_elapsedSeconds >= TimeoutSeconds)
            {
                Fail($"超时：{passedCount}/20 已移动，目标附近 {nearbyCount} 名，重叠 {overlapCount} 对，导航阻塞 {blockedCount} 名。");
            }
            else
            {
                _status = $"运行中：{passedCount}/20 已移动，目标附近 {nearbyCount} 名，重叠 {overlapCount} 对，导航阻塞 {blockedCount} 名。";
            }
        }

        private int CountOverlaps()
        {
            int count = 0;
            float minimumDistanceSquared = MinimumPairSeparation * MinimumPairSeparation;
            for (int i = 0; i < _movers.Length; i++)
            {
                if (_movers[i].Unit == null)
                    continue;

                for (int j = i + 1; j < _movers.Length; j++)
                {
                    if (_movers[j].Unit != null
                        && (_movers[i].WorldPosition - _movers[j].WorldPosition).sqrMagnitude
                        < minimumDistanceSquared)
                    {
                        count++;
                    }
                }

                for (int j = 0; j < _targets.Length; j++)
                {
                    if (_targets[j].Unit != null
                        && (_movers[i].WorldPosition - _targets[j].WorldPosition).sqrMagnitude
                        < minimumDistanceSquared)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public string GetMoverStatus(int index)
        {
            if (index < 0 || index >= _movers.Length)
                return string.Empty;

            MoverProbe mover = _movers[index];
            if (mover.Unit == null)
                return $"{index + 1:00}：等待生成";

            if (_targets[mover.TargetIndex].Unit == null)
                return $"{index + 1:00} → 目标{mover.TargetIndex + 1}：等待目标";

            string result = mover.Passed ? "已移动" : _running ? "行进中" : "未完成移动";
            UnitNavigationComp navigation = mover.Unit.Navigation;
            string navigationState = navigation?.State.ToString() ?? "未装配";
            if (_unitSystem is UnitSystem unitSystem
                && unitSystem.TryGetApproachSlotIndex(mover.Unit, out int slotIndex))
            {
                navigationState += slotIndex < 2
                    ? " / 近处位置"
                    : navigation?.State == EUnitNavigationState.Arrived
                        ? " / 外围待位"
                        : " / 前往外围";
            }

            string blockReason = navigation?.State == EUnitNavigationState.Blocked
                ? $" / {navigation.BlockReason}"
                : string.Empty;
            return $"{index + 1:00} → 目标{mover.TargetIndex + 1}：{result}，走 {mover.DistanceTravelled:0.##}，"
                   + $"{navigationState} / {mover.LastPathQueryStatus?.ToString() ?? "未查询"}{blockReason}";
        }

        public bool TryGetMoverPosition(int index, out Vector2 position, out int targetIndex)
        {
            position = default;
            targetIndex = -1;
            if (index < 0 || index >= _movers.Length || _movers[index].Unit == null)
                return false;

            position = _movers[index].WorldPosition;
            targetIndex = _movers[index].TargetIndex;
            return true;
        }

        public bool TryGetTargetPosition(int index, out Vector2 position)
        {
            position = default;
            if (index < 0 || index >= _targets.Length || _targets[index].Unit == null)
                return false;

            position = _targets[index].WorldPosition;
            return true;
        }

        public void Reset()
        {
            _version++;
            _running = false;
            _spawningTargets = false;
            _elapsedSeconds = 0f;
            _spawnedMoverCount = 0;
            ReleaseUnits();
            _movers = Array.Empty<MoverProbe>();
            _targets = Array.Empty<TargetProbe>();
            _unitSystem = null;
            _status = "多单位测试已清理。";
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Reset();
            _disposed = true;
        }

        private async UniTask SpawnUnitsAsync(int version, IUnitSystem unitSystem)
        {
            for (int index = 0; index < _movers.Length; index++)
            {
                MoverProbe mover = _movers[index];
                UnitEntity unit = await unitSystem.SpawnAsync(
                    new UnitSpawnRequest(WarriorDefinitionAddress,
                        mover.SpawnPosition, Quaternion.identity, 1, false));
                if (!IsCurrent(version))
                {
                    ReleaseLateUnit(unitSystem, unit);
                    return;
                }

                if (unit == null)
                {
                    FailSpawn($"移动单位 {index + 1} 生成失败。");
                    return;
                }

                mover.Unit = unit;
                mover.WorldPosition = mover.SpawnPosition;
                mover.PreviousPosition = mover.WorldPosition;
                _spawnedMoverCount++;
                _status = $"正在生成 {_spawnedMoverCount}/{MoverCount} 名移动单位。";
            }

            for (int index = 0; index < _targets.Length; index++)
            {
                TargetProbe target = _targets[index];
                UnitEntity unit = await unitSystem.SpawnAsync(
                    new UnitSpawnRequest(WarriorDefinitionAddress,
                        target.SpawnPosition, Quaternion.identity, 2, true));
                if (!IsCurrent(version))
                {
                    ReleaseLateUnit(unitSystem, unit);
                    return;
                }

                if (unit == null)
                {
                    FailSpawn($"目标 {index + 1} 生成失败。");
                    return;
                }

                target.Unit = unit;
                target.WorldPosition = target.SpawnPosition;
                unitSystem.TryGetUnitTransform(unit, out Transform root);
                Transform hurtbox = root != null ? root.Find("Hurtbox") : null;
                Collider2D collider = hurtbox != null ? hurtbox.GetComponent<Collider2D>() : null;
                if (collider == null)
                {
                    FailSpawn($"目标 {index + 1} 缺少 Hurtbox，无法隔离战斗影响。");
                    return;
                }

                target.Hurtbox = collider;
                target.HurtboxWasEnabled = collider.enabled;
                // 保留 AI 目标，但避免 20 名近战单位在到达前击败目标。
                collider.enabled = false;
                _running = true;
            }

            _spawningTargets = false;
            _elapsedSeconds = 0f;
            _status = $"运行中：20 名单位前往 {_targets.Length} 个目标。";
        }

        private void FailSpawn(string message)
        {
            Reset();
            _status = $"失败：{message}";
        }

        private void Fail(string message)
        {
            _version++;
            _running = false;
            _spawningTargets = false;
            _status = $"失败：{message}";
        }

        private void ReleaseUnits()
        {
            if (_unitSystem == null)
                return;

            for (int index = 0; index < _targets.Length; index++)
            {
                TargetProbe target = _targets[index];
                if (target.Hurtbox != null)
                    target.Hurtbox.enabled = target.HurtboxWasEnabled;
            }

            for (int index = 0; index < _movers.Length; index++)
            {
                if (_movers[index].Unit != null)
                    _unitSystem.Despawn(_movers[index].Unit);
            }

            for (int index = 0; index < _targets.Length; index++)
            {
                if (_targets[index].Unit != null)
                    _unitSystem.Despawn(_targets[index].Unit);
            }
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

        private sealed class MoverProbe
        {
            public readonly Vector3 SpawnPosition;
            public readonly int TargetIndex;
            public UnitEntity Unit;
            public Vector2 WorldPosition;
            public Vector2 PreviousPosition;
            public float DistanceTravelled;
            public bool SeenFollowing;
            public bool SeenSuccessfulQuery;
            public bool Passed;
            public EPathQueryStatus? LastPathQueryStatus;

            public MoverProbe(Vector3 spawnPosition, int targetIndex)
            {
                SpawnPosition = spawnPosition;
                TargetIndex = targetIndex;
            }
        }

        private sealed class TargetProbe
        {
            public readonly Vector3 SpawnPosition;
            public UnitEntity Unit;
            public Vector2 WorldPosition;
            public Collider2D Hurtbox;
            public bool HurtboxWasEnabled;

            public TargetProbe(Vector3 spawnPosition)
            {
                SpawnPosition = spawnPosition;
            }
        }
    }
}
