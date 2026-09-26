using System.Collections.Generic;
using BorFramework;
using GameLogic.Navigation;
using GameLogic.Units.Skills;
using UnityEngine;

namespace GameLogic.Units.Common
{
    internal sealed class UnitNavigationLogic : Logic
    {
        private const int MaximumRecoveryAttempts = 1;
        private const float WaypointReachedDistance = 0.05f;
        private const float ArrivalDistance = 0.05f;
        private const float DestinationRepathCooldownSeconds = 0.25f;
        private const float DestinationChangeDistance = 0.5f;
        private const float BlockedDestinationChangeDistance = 0.1f;
        private const float StuckRepathDelaySeconds = 0.4f;
        private const float MinimumProgressDistance = 0.03f;

        private readonly UnitViewComp _view;
        private readonly UnitCommandComp _command;
        private readonly UnitNavigationComp _navigation;
        private readonly UnitSkillComp _skills;
        private readonly UnitLifeComp _life;
        private readonly INavigationSystem _navigationSystem;
        private readonly Vector2 _navigationAnchorOffset;
        private readonly float _clearanceRadius;
        private readonly List<Vector2> _waypoints = new();
        private int _waypointIndex;
        private int _observedRequestVersion = -1;
        private int _recoveryAttempts;
        private float _destinationRepathCooldownRemainingSeconds;
        private float _stuckTimeSeconds;
        private float _progressSampleDistanceToWaypoint;
        private Vector2 _plannedDestination;
        private Vector2 _pathDestination;
        private Vector2 _waypointApproachStart;
        private EUnitNavigationState _stateBeforePause;
        private bool _isApproachDestination;
        private bool _issuedMoveIntentLastTick;

        public override ELogicPhase Phase => ELogicPhase.Navigation;

        public UnitNavigationLogic(
            UnitViewComp view,
            UnitCommandComp command,
            UnitNavigationComp navigation,
            UnitSkillComp skills,
            UnitLifeComp life,
            INavigationSystem navigationSystem,
            Vector2 navigationAnchorOffset,
            float clearanceRadius)
        {
            _view = view;
            _command = command;
            _navigation = navigation;
            _skills = skills;
            _life = life;
            _navigationSystem = navigationSystem;
            _navigationAnchorOffset = navigationAnchorOffset;
            _clearanceRadius = clearanceRadius;
        }

        protected override void OnTick(float dt)
        {
            // 每帧默认停止，只有取得可跟随路径点后才输出方向。
            _command.Clear();
            if (_life.IsDead || _view.WorldPositionTransform == null || !_navigation.HasDestination)
            {
                EnterIdle();
                return;
            }

            Vector2 currentPosition = _view.WorldPositionTransform.position;
            if (_observedRequestVersion != _navigation.RequestVersion)
                ResetForNewRequest();

            if (_skills.ActiveSkill != null && _skills.ActiveSkill.BlocksMovement)
            {
                EnterPaused();
                return;
            }

            ResumeFromPause();
            _destinationRepathCooldownRemainingSeconds -= Mathf.Max(0f, dt);

            if (NeedsPath())
            {
                // 同请求从 Stop 恢复时也会进入 Idle，不能因此补回恢复次数。
                if (_navigation.State != EUnitNavigationState.Idle)
                    _recoveryAttempts = 0;
                BuildPath(currentPosition);
            }

            if (_navigation.State == EUnitNavigationState.Following)
                FollowPath(currentPosition, dt);
        }

        protected override void OnStop()
        {
            EnterIdle();
            _command.Clear();
        }

        private void ResetForNewRequest()
        {
            _observedRequestVersion = _navigation.RequestVersion;
            _recoveryAttempts = 0;
            EnterIdle();
        }

        private bool NeedsPath()
        {
            switch (_navigation.State)
            {
                case EUnitNavigationState.Idle:
                    return true;
                case EUnitNavigationState.Following:
                    return _destinationRepathCooldownRemainingSeconds <= 0f
                           && HasDestinationChangedEnough();
                case EUnitNavigationState.Arrived:
                case EUnitNavigationState.Blocked:
                    return HasDestinationChangedEnough(
                        _navigation.State == EUnitNavigationState.Blocked || _isApproachDestination
                            ? BlockedDestinationChangeDistance
                            : DestinationChangeDistance);
                default:
                    return false;
            }
        }

        private void BuildPath(Vector2 currentPosition)
        {
            _plannedDestination = _navigation.Destination;
            _isApproachDestination = false;
            if (HasArrivedAtDestination(currentPosition))
            {
                EnterArrived();
                return;
            }

            EPathQueryStatus pathStatus = _navigationSystem != null
                ? _navigationSystem.FindApproachPath(
                    currentPosition,
                    _plannedDestination,
                    _navigationAnchorOffset,
                    _clearanceRadius,
                    _waypoints,
                    out _pathDestination)
                : EPathQueryStatus.MapUnavailable;
            _navigation.SetPathQueryStatus(pathStatus);
            if (pathStatus != EPathQueryStatus.Success
                && pathStatus != EPathQueryStatus.Approach)
            {
                EnterBlocked(EUnitNavigationBlockReason.PathQueryFailed);
                return;
            }

            _isApproachDestination = pathStatus == EPathQueryStatus.Approach;

            if (_waypoints.Count == 0)
            {
                EnterBlocked(EUnitNavigationBlockReason.PathEndedBeforeDestination);
                return;
            }

            _waypointIndex = 0;
            _waypointApproachStart = currentPosition;
            _destinationRepathCooldownRemainingSeconds = DestinationRepathCooldownSeconds;
            _navigation.SetExecutionState(EUnitNavigationState.Following);
            ResetProgressTracking();
        }

        private void FollowPath(Vector2 currentPosition, float dt)
        {
            SkipReachedWaypoints(currentPosition);
            var recoveryReason = EUnitNavigationBlockReason.None;
            if (_waypointIndex >= _waypoints.Count)
            {
                if (HasArrivedAtPosition(currentPosition, _pathDestination))
                {
                    EnterArrived();
                    return;
                }

                recoveryReason = EUnitNavigationBlockReason.PathEndedBeforeDestination;
            }
            else if (IsStuck(currentPosition, dt))
            {
                recoveryReason = EUnitNavigationBlockReason.NoProgress;
            }

            if (recoveryReason != EUnitNavigationBlockReason.None)
            {
                RecoverPath(currentPosition, recoveryReason);
                if (_navigation.State != EUnitNavigationState.Following)
                    return;

                SkipReachedWaypoints(currentPosition);
                if (_waypointIndex >= _waypoints.Count)
                {
                    EnterBlocked(EUnitNavigationBlockReason.PathEndedBeforeDestination);
                    return;
                }

                // 建好路径只重建进度样本，实际前进后才恢复重试次数。
                ResetProgressSample(currentPosition);
            }

            Vector2 direction = _waypoints[_waypointIndex] - currentPosition;
            _command.SetMoveDirection(direction.normalized);
            _issuedMoveIntentLastTick = direction.sqrMagnitude > Mathf.Epsilon;
        }

        private void RecoverPath(
            Vector2 currentPosition,
            EUnitNavigationBlockReason exhaustedReason)
        {
            if (_recoveryAttempts >= MaximumRecoveryAttempts)
            {
                EnterBlocked(exhaustedReason);
                return;
            }

            _recoveryAttempts++;
            BuildPath(currentPosition);
        }

        private void SkipReachedWaypoints(Vector2 currentPosition)
        {
            float reachedDistanceSquared = WaypointReachedDistance * WaypointReachedDistance;
            while (_waypointIndex < _waypoints.Count)
            {
                Vector2 waypoint = _waypoints[_waypointIndex];
                Vector2 remainingOffset = waypoint - currentPosition;
                Vector2 approachOffset = waypoint - _waypointApproachStart;
                bool reachedWaypoint = remainingOffset.sqrMagnitude
                                       <= reachedDistanceSquared;
                bool passedWaypoint = approachOffset.sqrMagnitude > Mathf.Epsilon
                                      && Vector2.Dot(remainingOffset, approachOffset) <= 0f;
                if (!reachedWaypoint && !passedWaypoint)
                    return;

                _waypointApproachStart = waypoint;
                _waypointIndex++;
                ResetProgressTracking();
            }
        }

        private bool IsStuck(Vector2 currentPosition, float dt)
        {
            if (_navigation.IsAvoidanceYielding)
            {
                ResetProgressSample(currentPosition);
                return false;
            }

            if (!_issuedMoveIntentLastTick)
            {
                ResetProgressSample(currentPosition);
                return false;
            }

            float distanceToWaypoint = Vector2.Distance(
                currentPosition,
                _waypoints[_waypointIndex]);
            if (_progressSampleDistanceToWaypoint - distanceToWaypoint
                >= MinimumProgressDistance)
            {
                _recoveryAttempts = 0;
                ResetProgressSample(currentPosition);
                return false;
            }

            _stuckTimeSeconds += Mathf.Max(0f, dt);
            if (_stuckTimeSeconds < StuckRepathDelaySeconds)
                return false;

            ResetProgressTracking();
            return true;
        }

        private void ResetProgressSample(Vector2 currentPosition)
        {
            _progressSampleDistanceToWaypoint = Vector2.Distance(
                currentPosition,
                _waypoints[_waypointIndex]);
            _stuckTimeSeconds = 0f;
        }

        private void ResetProgressTracking()
        {
            _stuckTimeSeconds = 0f;
            _progressSampleDistanceToWaypoint = 0f;
            _issuedMoveIntentLastTick = false;
        }

        private bool HasDestinationChangedEnough()
        {
            return HasDestinationChangedEnough(DestinationChangeDistance);
        }

        private bool HasDestinationChangedEnough(float changeDistance)
        {
            float destinationChangeDistanceSquared =
                changeDistance * changeDistance;
            return (_navigation.Destination - _plannedDestination).sqrMagnitude
                   >= destinationChangeDistanceSquared;
        }

        private bool HasArrivedAtDestination(Vector2 currentPosition)
        {
            return HasArrivedAtPosition(currentPosition, _navigation.Destination);
        }

        private static bool HasArrivedAtPosition(Vector2 currentPosition, Vector2 destination)
        {
            float arrivalDistanceSquared = ArrivalDistance * ArrivalDistance;
            return (currentPosition - destination).sqrMagnitude
                   <= arrivalDistanceSquared;
        }

        private void EnterPaused()
        {
            if (_navigation.State != EUnitNavigationState.Paused)
            {
                _stateBeforePause = _navigation.State;
                _navigation.SetExecutionState(EUnitNavigationState.Paused, _navigation.BlockReason);
            }

            ResetProgressTracking();
        }

        private void ResumeFromPause()
        {
            if (_navigation.State != EUnitNavigationState.Paused)
                return;

            _navigation.SetExecutionState(_stateBeforePause, _navigation.BlockReason);
            ResetProgressTracking();
        }

        private void EnterArrived()
        {
            _plannedDestination = _navigation.Destination;
            ClearPath();
            _navigation.SetExecutionState(EUnitNavigationState.Arrived);
        }

        private void EnterBlocked(EUnitNavigationBlockReason reason)
        {
            ClearPath();
            _navigation.SetExecutionState(EUnitNavigationState.Blocked, reason);
        }

        private void EnterIdle()
        {
            ClearPath();
            _isApproachDestination = false;
            _navigation.SetExecutionState(EUnitNavigationState.Idle);
        }

        private void ClearPath()
        {
            _waypoints.Clear();
            _waypointIndex = 0;
            _destinationRepathCooldownRemainingSeconds = 0f;
            _pathDestination = default;
            _waypointApproachStart = default;
            ResetProgressTracking();
        }
    }
}
