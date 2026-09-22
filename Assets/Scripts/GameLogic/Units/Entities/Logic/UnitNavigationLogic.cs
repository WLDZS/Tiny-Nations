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
        private Vector2 _waypointApproachStart;
        private EUnitNavigationState _stateBeforePause;
        private bool _hasProgressSample;
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
            if (_life.IsDead || _view.Transform == null || !_navigation.HasDestination)
            {
                EnterIdle();
                return;
            }

            Vector2 currentPosition = _view.Transform.position;
            if (_observedRequestVersion != _navigation.RequestVersion)
                ResetForNewRequest();

            if (_skills.ActiveSkill != null && _skills.ActiveSkill.BlocksMovement)
            {
                EnterPaused();
                return;
            }

            ResumeFromPause();
            _destinationRepathCooldownRemainingSeconds -= Mathf.Max(0f, dt);

            if (_navigation.State == EUnitNavigationState.Idle)
            {
                if (!TryBuildPath(currentPosition))
                    return;
            }
            else if (_navigation.State == EUnitNavigationState.Arrived
                     || _navigation.State == EUnitNavigationState.Blocked)
            {
                if (!HasDestinationChangedEnough())
                {
                    _command.Clear();
                    return;
                }

                _recoveryAttempts = 0;
                if (!TryBuildPath(currentPosition))
                    return;
            }
            else if (_navigation.State == EUnitNavigationState.Following
                     && _destinationRepathCooldownRemainingSeconds <= 0f
                     && HasDestinationChangedEnough())
            {
                _recoveryAttempts = 0;
                if (!TryBuildPath(currentPosition))
                    return;
            }

            if (_navigation.State != EUnitNavigationState.Following)
            {
                _command.Clear();
                return;
            }

            SkipReachedWaypoints(currentPosition);
            if (_waypointIndex >= _waypoints.Count)
            {
                if (HasArrivedAtDestination(currentPosition))
                {
                    EnterArrived();
                    return;
                }

                if (!TryRecover(
                        currentPosition,
                        EUnitNavigationBlockReason.PathEndedBeforeDestination))
                {
                    return;
                }

                SkipReachedWaypoints(currentPosition);
            }

            if (_waypointIndex >= _waypoints.Count)
            {
                EnterBlocked(EUnitNavigationBlockReason.PathEndedBeforeDestination);
                return;
            }

            if (IsStuck(currentPosition, dt))
            {
                if (!TryRecover(currentPosition, EUnitNavigationBlockReason.NoProgress))
                    return;

                SkipReachedWaypoints(currentPosition);
                if (_waypointIndex >= _waypoints.Count)
                {
                    EnterBlocked(EUnitNavigationBlockReason.PathEndedBeforeDestination);
                    return;
                }
            }

            Vector2 direction = _waypoints[_waypointIndex] - currentPosition;
            _command.SetMoveDirection(direction.normalized);
            _issuedMoveIntentLastTick = direction.sqrMagnitude > Mathf.Epsilon;
        }

        public override void OnStop()
        {
            EnterIdle();
        }

        public override void Dispose()
        {
            EnterIdle();
        }

        private void ResetForNewRequest()
        {
            _observedRequestVersion = _navigation.RequestVersion;
            _recoveryAttempts = 0;
            ClearPath();
            _navigation.ResetExecutionState();
        }

        private bool TryBuildPath(Vector2 currentPosition)
        {
            _plannedDestination = _navigation.Destination;
            if (HasArrivedAtDestination(currentPosition))
            {
                EnterArrived();
                return false;
            }

            EPathQueryStatus pathStatus = _navigationSystem != null
                ? _navigationSystem.FindPath(
                    currentPosition,
                    _plannedDestination,
                    _navigationAnchorOffset,
                    _clearanceRadius,
                    _waypoints)
                : EPathQueryStatus.MapUnavailable;
            _navigation.SetPathQueryStatus(pathStatus);
            if (pathStatus != EPathQueryStatus.Success)
            {
                EnterBlocked(EUnitNavigationBlockReason.PathQueryFailed);
                return false;
            }

            if (_waypoints.Count == 0)
            {
                EnterBlocked(EUnitNavigationBlockReason.PathEndedBeforeDestination);
                return false;
            }

            _waypointIndex = 0;
            _waypointApproachStart = currentPosition;
            _destinationRepathCooldownRemainingSeconds = DestinationRepathCooldownSeconds;
            _navigation.ClearBlockReason();
            _navigation.SetState(EUnitNavigationState.Following);
            ResetProgressTracking();
            return true;
        }

        private bool TryRecover(
            Vector2 currentPosition,
            EUnitNavigationBlockReason exhaustedReason)
        {
            if (_recoveryAttempts >= MaximumRecoveryAttempts)
            {
                EnterBlocked(exhaustedReason);
                return false;
            }

            _recoveryAttempts++;
            return TryBuildPath(currentPosition);
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
            if (!_issuedMoveIntentLastTick)
            {
                ResetProgressSample(currentPosition);
                return false;
            }

            float distanceToWaypoint = Vector2.Distance(
                currentPosition,
                _waypoints[_waypointIndex]);
            if (!_hasProgressSample)
            {
                ResetProgressSample(currentPosition);
                return false;
            }

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
            _progressSampleDistanceToWaypoint = _waypointIndex < _waypoints.Count
                ? Vector2.Distance(currentPosition, _waypoints[_waypointIndex])
                : 0f;
            _stuckTimeSeconds = 0f;
            _hasProgressSample = true;
        }

        private void ResetProgressTracking()
        {
            _stuckTimeSeconds = 0f;
            _progressSampleDistanceToWaypoint = 0f;
            _hasProgressSample = false;
            _issuedMoveIntentLastTick = false;
        }

        private bool HasDestinationChangedEnough()
        {
            float destinationChangeDistanceSquared =
                DestinationChangeDistance * DestinationChangeDistance;
            return (_navigation.Destination - _plannedDestination).sqrMagnitude
                   >= destinationChangeDistanceSquared;
        }

        private bool HasArrivedAtDestination(Vector2 currentPosition)
        {
            float arrivalDistanceSquared = ArrivalDistance * ArrivalDistance;
            return (currentPosition - _navigation.Destination).sqrMagnitude
                   <= arrivalDistanceSquared;
        }

        private void EnterPaused()
        {
            if (_navigation.State != EUnitNavigationState.Paused)
            {
                _stateBeforePause = _navigation.State;
                _navigation.SetState(EUnitNavigationState.Paused);
            }

            _command.Clear();
            ResetProgressTracking();
        }

        private void ResumeFromPause()
        {
            if (_navigation.State != EUnitNavigationState.Paused)
                return;

            _navigation.SetState(_stateBeforePause);
            ResetProgressTracking();
        }

        private void EnterArrived()
        {
            _plannedDestination = _navigation.Destination;
            ClearPath();
            _navigation.ClearBlockReason();
            _navigation.SetState(EUnitNavigationState.Arrived);
            _command.Clear();
        }

        private void EnterBlocked(EUnitNavigationBlockReason reason)
        {
            ClearPath();
            _navigation.SetBlocked(reason);
            _command.Clear();
        }

        private void EnterIdle()
        {
            ClearPath();
            _navigation.ResetExecutionState();
            _command.Clear();
        }

        private void ClearPath()
        {
            _waypoints.Clear();
            _waypointIndex = 0;
            _destinationRepathCooldownRemainingSeconds = 0f;
            _waypointApproachStart = default;
            ResetProgressTracking();
        }
    }
}
