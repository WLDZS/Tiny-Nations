using System.Collections.Generic;
using GameLogic.Units.Common;
using UnityEngine;

namespace GameLogic.Units
{
    /// <summary>Chooses short-horizon AI velocities after all units have issued movement intent.</summary>
    internal sealed class UnitLocalAvoidance
    {
        private const float PersonalSpace = 0.15f;
        private const float PredictionSeconds = 0.7f;
        private const float MinimumSpeedSquared = 0.0001f;
        private const float ImmediateCollisionPenalty = 100f;
        private const float VelocityChangePerSecond = 8f;
        private const float SideSwitchPenalty = 1.5f;
        private static readonly float[] TurnAngles = { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f };

        private readonly List<Agent> _agents = new();
        private readonly Dictionary<UnitEntity, Vector2> _lastVelocities = new();

        public void Apply(Dictionary<UnitEntity, UnitRuntime> units, float dt)
        {
            _agents.Clear();
            foreach (KeyValuePair<UnitEntity, UnitRuntime> pair in units)
            {
                UnitRuntime runtime = pair.Value;
                if (pair.Key.Life.IsDead
                    || runtime.Instance == null
                    || !runtime.Instance.activeInHierarchy
                    || !(runtime.BodyCollider is CircleCollider2D bodyCollider)
                    || !bodyCollider.enabled
                    || bodyCollider.attachedRigidbody == null)
                {
                    continue;
                }

                Rigidbody2D rigidbody = bodyCollider.attachedRigidbody;
                Vector3 scale = bodyCollider.transform.lossyScale;
                float radius = bodyCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                Vector2 center = bodyCollider.transform.TransformPoint(bodyCollider.offset);
                Vector2 preferredVelocity = rigidbody.linearVelocity;
                bool canSteer = pair.Key.Navigation != null
                                && pair.Key.Navigation.State == EUnitNavigationState.Following
                                && preferredVelocity.sqrMagnitude > MinimumSpeedSquared;
                Vector2 observedVelocity = canSteer
                    && _lastVelocities.TryGetValue(pair.Key, out Vector2 lastVelocity)
                    ? lastVelocity
                    : preferredVelocity;
                _agents.Add(new Agent(pair.Key, rigidbody, center, radius + PersonalSpace,
                    preferredVelocity, observedVelocity, canSteer));
            }

            // Snapshot every input before writing velocities to avoid update-order bias.
            for (int i = 0; i < _agents.Count; i++)
            {
                Agent agent = _agents[i];
                if (!agent.CanSteer)
                {
                    agent.Unit.Navigation?.SetAvoidanceYielding(false);
                    _lastVelocities.Remove(agent.Unit);
                    continue;
                }

                Vector2 velocity = ChooseVelocity(i);
                if (_lastVelocities.TryGetValue(agent.Unit, out Vector2 previousVelocity)
                    && Vector2.Dot(previousVelocity, agent.PreferredVelocity)
                    > previousVelocity.magnitude * agent.PreferredVelocity.magnitude * 0.5f)
                {
                    Vector2 smoothedVelocity = Vector2.MoveTowards(
                        previousVelocity,
                        velocity,
                        agent.PreferredVelocity.magnitude * VelocityChangePerSecond * Mathf.Max(0f, dt));
                    if (Score(i, smoothedVelocity) <= Score(i, velocity) + 0.5f)
                        velocity = smoothedVelocity;
                }

                agent.Rigidbody.linearVelocity = velocity;
                agent.Unit.Navigation.SetAvoidanceYielding(
                    Vector2.Dot(velocity, agent.PreferredVelocity)
                    < agent.PreferredVelocity.sqrMagnitude * 0.5f);
                _lastVelocities[agent.Unit] = velocity;
            }
        }

        public void Remove(UnitEntity unit)
        {
            _lastVelocities.Remove(unit);
        }

        public void Clear()
        {
            _agents.Clear();
            _lastVelocities.Clear();
        }

        private Vector2 ChooseVelocity(int agentIndex)
        {
            Agent agent = _agents[agentIndex];
            Vector2 preferred = agent.PreferredVelocity;
            float speed = preferred.magnitude;
            Vector2 forward = preferred / speed;
            Vector2 left = new Vector2(-forward.y, forward.x);
            Vector2 bestVelocity = Vector2.zero;
            float bestScore = Score(agentIndex, Vector2.zero) + 2f;

            for (int i = 0; i < TurnAngles.Length; i++)
            {
                float radians = TurnAngles[i] * Mathf.Deg2Rad;
                Vector2 direction = forward * Mathf.Cos(radians) + left * Mathf.Sin(radians);
                for (int speedIndex = 0; speedIndex < 2; speedIndex++)
                {
                    Vector2 candidate = direction * (speed * (speedIndex == 0 ? 1f : 0.6f));
                    float score = Score(agentIndex, candidate);
                    if (score >= bestScore)
                        continue;

                    bestScore = score;
                    bestVelocity = candidate;
                }
            }

            return bestVelocity;
        }

        private float Score(int agentIndex, Vector2 candidate)
        {
            Agent agent = _agents[agentIndex];
            float speedSquared = agent.PreferredVelocity.sqrMagnitude;
            float score = (candidate - agent.PreferredVelocity).sqrMagnitude / speedSquared;
            if (_lastVelocities.TryGetValue(agent.Unit, out Vector2 lastVelocity)
                && Vector2.Dot(lastVelocity, agent.PreferredVelocity)
                > lastVelocity.magnitude * agent.PreferredVelocity.magnitude * 0.5f)
            {
                score += 0.7f * (candidate - lastVelocity).sqrMagnitude / speedSquared;
                float previousSide = agent.PreferredVelocity.x * lastVelocity.y
                                     - agent.PreferredVelocity.y * lastVelocity.x;
                float candidateSide = agent.PreferredVelocity.x * candidate.y
                                      - agent.PreferredVelocity.y * candidate.x;
                if (Mathf.Abs(previousSide) > speedSquared * 0.15f
                    && previousSide * candidateSide < 0f)
                {
                    score += SideSwitchPenalty;
                }
            }

            for (int i = 0; i < _agents.Count; i++)
            {
                if (i == agentIndex)
                    continue;

                Agent other = _agents[i];
                Vector2 offset = agent.Center - other.Center;
                float requiredDistance = agent.Radius + other.Radius;
                Vector2 relativeVelocity = candidate - other.ObservedVelocity;
                float reachableDistance = requiredDistance
                                          + (candidate.magnitude + other.ObservedVelocity.magnitude)
                                          * PredictionSeconds;
                if (offset.sqrMagnitude > reachableDistance * reachableDistance)
                    continue;

                float relativeSpeedSquared = relativeVelocity.sqrMagnitude;
                float closestTime = relativeSpeedSquared > MinimumSpeedSquared
                    ? Mathf.Clamp(-Vector2.Dot(offset, relativeVelocity) / relativeSpeedSquared,
                        0f, PredictionSeconds)
                    : 0f;
                float closestDistance = (offset + relativeVelocity * closestTime).magnitude;
                if (closestDistance < requiredDistance)
                {
                    float intrusion = 1f - closestDistance / requiredDistance;
                    score += 25f * intrusion * intrusion;
                }

                Vector2 nextOffset = offset + relativeVelocity * Time.fixedDeltaTime;
                if (nextOffset.sqrMagnitude < requiredDistance * requiredDistance)
                    score += ImmediateCollisionPenalty;
            }

            return score;
        }

        private readonly struct Agent
        {
            public UnitEntity Unit { get; }
            public Rigidbody2D Rigidbody { get; }
            public Vector2 Center { get; }
            public float Radius { get; }
            public Vector2 PreferredVelocity { get; }
            public Vector2 ObservedVelocity { get; }
            public bool CanSteer { get; }

            public Agent(UnitEntity unit, Rigidbody2D rigidbody, Vector2 center,
                float radius, Vector2 preferredVelocity, Vector2 observedVelocity, bool canSteer)
            {
                Unit = unit;
                Rigidbody = rigidbody;
                Center = center;
                Radius = radius;
                PreferredVelocity = preferredVelocity;
                ObservedVelocity = observedVelocity;
                CanSteer = canSteer;
            }
        }
    }
}
