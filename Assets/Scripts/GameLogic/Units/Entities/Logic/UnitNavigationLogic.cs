using System.Collections.Generic;
using BorFramework;
using GameLogic.Navigation;
using UnityEngine;

namespace GameLogic.Units.Common
{
    internal sealed class UnitNavigationLogic : Logic
    {
        private const float RetryIntervalSeconds = 1f;
        private const float TargetMoveRepathDistance = 0.4f;
        private const float StuckDurationSeconds = 0.8f;
        private const float PositionChangeEpsilon = 0.005f;
        private const float MinimumWaypointDistance = 0.08f;
        private const float ApproachArrivalDistance = 0.12f;

        private readonly UnitEntity _owner;
        private readonly UnitCommandComp _command;
        private readonly IUnitQuery _unitQuery;
        private readonly INavigationSystem _navigation;
        private readonly List<Vector3> _path = new();
        private UnitEntity _target;
        private float _attackRange;
        private int _waypointIndex;
        private float _retryTimeRemaining;
        private float _stuckTime;
        private Vector2 _pathTargetCenter;
        private Vector2 _lastPosition;
        private Vector2 _approachPosition;
        private Vector2 _moveDestination;
        private bool _canAttackFromPosition;
        private bool _pausedForAttack;
        private bool _hasMoveDestination;

        public override ELogicPhase Phase => ELogicPhase.Navigation;

        public bool HasMoveDestination => _hasMoveDestination;

        public UnitNavigationLogic(
            UnitEntity owner,
            UnitCommandComp command,
            IUnitQuery unitQuery,
            INavigationSystem navigation)
        {
            _owner = owner;
            _command = command;
            _unitQuery = unitQuery;
            _navigation = navigation;
        }

        public void SetTarget(UnitEntity target, float attackRange)
        {
            if (_hasMoveDestination)
                return;

            _pausedForAttack = false;
            if (_target == target && Mathf.Approximately(_attackRange, attackRange))
                return;

            _unitQuery?.ReleaseApproachPosition(_owner);
            _target = target;
            _attackRange = attackRange;
            _path.Clear();
            _retryTimeRemaining = 0f;
            _stuckTime = 0f;
            _canAttackFromPosition = false;
        }

        public void ClearTarget()
        {
            if (_hasMoveDestination)
                return;

            _unitQuery?.ReleaseApproachPosition(_owner);
            _target = null;
            _path.Clear();
            _waypointIndex = 0;
            _canAttackFromPosition = false;
            _pausedForAttack = false;
            _command.Clear();
        }

        public bool TrySetMoveDestination(Vector2 destination)
        {
            if (_unitQuery == null || _navigation?.Map == null
                || !_unitQuery.TryGetUnitAttackFootprint(_owner, out Vector2 center, out float radius))
                return false;

            var path = new List<Vector3>();
            if (!_navigation.TryFindPathToPoint(center, destination, radius, path))
                return false;

            _unitQuery.ReleaseApproachPosition(_owner);
            _target = null;
            _pausedForAttack = false;
            _canAttackFromPosition = false;
            _hasMoveDestination = true;
            _moveDestination = destination;
            _path.Clear();
            _path.AddRange(path);
            _waypointIndex = 0;
            _retryTimeRemaining = 0f;
            _stuckTime = 0f;
            _lastPosition = center;
            _command.Clear();
            return true;
        }

        public void PauseForAttack()
        {
            _pausedForAttack = true;
            _path.Clear();
            _command.Clear();
        }

        public bool IsReadyToAttack(UnitEntity target)
        {
            return target != null && _target == target && _canAttackFromPosition
                   && _unitQuery.TryGetUnitAttackFootprint(_owner, out Vector2 center, out _)
                   && (center - _approachPosition).sqrMagnitude
                   <= ApproachArrivalDistance * ApproachArrivalDistance;
        }

        protected override void OnTick(float dt)
        {
            if (_hasMoveDestination)
            {
                FollowMoveDestination(dt);
                return;
            }

            if (_target == null)
                return;

            if (_target.Life.IsDead
                || _unitQuery == null || _navigation?.Map == null
                || !_unitQuery.TryGetUnitAttackFootprint(
                    _owner, out Vector2 sourceCenter, out float sourceRadius)
                || !_unitQuery.TryGetUnitAttackFootprint(
                    _target, out _, out _))
            {
                ClearTarget();
                return;
            }

            if (_pausedForAttack)
            {
                _command.Clear();
                return;
            }

            if (!_unitQuery.TryGetApproachPosition(
                    _owner, _target, _attackRange,
                    out Vector2 approachPosition, out bool canAttack))
            {
                PauseAfterFailedPath(sourceCenter);
                return;
            }

            _approachPosition = approachPosition;
            _canAttackFromPosition = canAttack;
            if ((sourceCenter - approachPosition).sqrMagnitude
                <= ApproachArrivalDistance * ApproachArrivalDistance
                && (!canAttack
                    || _unitQuery.IsTargetInAttackRange(_owner, _target, _attackRange)))
            {
                _path.Clear();
                _command.Clear();
                _stuckTime = 0f;
                return;
            }

            float safeDt = Mathf.Max(0f, dt);
            if (_retryTimeRemaining > 0f)
            {
                _retryTimeRemaining -= safeDt;
                if (_retryTimeRemaining > 0f
                    && (approachPosition - _pathTargetCenter).sqrMagnitude
                    < TargetMoveRepathDistance * TargetMoveRepathDistance)
                {
                    _command.Clear();
                    return;
                }
            }

            if (_path.Count == 0
                || ((Vector2)_path[_path.Count - 1] - approachPosition).sqrMagnitude
                > TargetMoveRepathDistance * TargetMoveRepathDistance)
            {
                if (!_navigation.TryFindPathToPoint(
                        sourceCenter, approachPosition, sourceRadius, _path))
                {
                    PauseAfterFailedPath(approachPosition);
                    return;
                }

                _waypointIndex = 0;
                _pathTargetCenter = approachPosition;
            }

            FollowPath(sourceCenter, safeDt, approachPosition);
        }

        private void FollowMoveDestination(float dt)
        {
            if (!_unitQuery.TryGetUnitAttackFootprint(_owner, out Vector2 center, out float radius))
            {
                ClearMoveDestination();
                return;
            }

            if (_path.Count == 0)
            {
                if (!_navigation.TryFindPathToPoint(center, _moveDestination, radius, _path))
                {
                    ClearMoveDestination();
                    return;
                }

                _waypointIndex = 0;
            }

            FollowPath(center, Mathf.Max(0f, dt), _moveDestination);
        }

        private void FollowPath(Vector2 sourceCenter, float safeDt, Vector2 destination)
        {
            float waypointDistance = MinimumWaypointDistance;
            float movementStep = 0f;
            if (_owner.Attributes.TryGetCurrentValue(
                    EUnitAttributeType.MoveSpeed, out float moveSpeed))
            {
                movementStep = moveSpeed * Mathf.Max(safeDt, Time.fixedDeltaTime);
                waypointDistance = Mathf.Max(
                    waypointDistance,
                    movementStep);
            }

            while (_waypointIndex < _path.Count)
            {
                float threshold = _waypointIndex == _path.Count - 1
                    ? Mathf.Min(waypointDistance, 0.04f)
                    : waypointDistance;
                if (((Vector2)_path[_waypointIndex] - sourceCenter).sqrMagnitude
                    > threshold * threshold)
                {
                    break;
                }

                _waypointIndex++;
            }

            if (_waypointIndex >= _path.Count)
            {
                _path.Clear();
                _command.Clear();
                if (_hasMoveDestination)
                    _hasMoveDestination = false;
                return;
            }

            Vector2 direction = (Vector2)_path[_waypointIndex] - sourceCenter;
            _command.SetMoveDirection(movementStep > 0f && direction.magnitude < movementStep
                ? direction / movementStep
                : direction.normalized);
            if ((sourceCenter - _lastPosition).sqrMagnitude
                <= PositionChangeEpsilon * PositionChangeEpsilon)
            {
                _stuckTime += safeDt;
                if (_stuckTime >= StuckDurationSeconds)
                {
                    if (_hasMoveDestination)
                        ClearMoveDestination();
                    else
                        PauseAfterFailedPath(destination);
                    return;
                }
            }
            else
            {
                _stuckTime = 0f;
            }

            _lastPosition = sourceCenter;
        }

        protected override void OnStop()
        {
            ClearMoveDestination();
            ClearTarget();
        }

        private void ClearMoveDestination()
        {
            _hasMoveDestination = false;
            _path.Clear();
            _waypointIndex = 0;
            _stuckTime = 0f;
            _command.Clear();
        }

        private void PauseAfterFailedPath(Vector2 targetCenter)
        {
            _path.Clear();
            _pathTargetCenter = targetCenter;
            _retryTimeRemaining = RetryIntervalSeconds;
            _stuckTime = 0f;
            _command.Clear();
        }
    }
}
