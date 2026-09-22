using BorFramework;
using GameLogic.Navigation;
using UnityEngine;

namespace GameLogic.Units.Common
{
    internal sealed class UnitNavigationComp : Comp
    {
        public bool HasDestination { get; private set; }

        public Vector2 Destination { get; private set; }

        public int RequestVersion { get; private set; }

        public EUnitNavigationState State { get; private set; }

        public EPathQueryStatus? LastPathQueryStatus { get; private set; }

        public EUnitNavigationBlockReason BlockReason { get; private set; }

        public void BeginDestination(Vector2 destination)
        {
            Destination = destination;
            HasDestination = true;
            RequestVersion++;
            ResetExecutionState();
        }

        public void UpdateDestination(Vector2 destination)
        {
            if (!HasDestination)
            {
                BeginDestination(destination);
                return;
            }

            Destination = destination;
        }

        public void ClearDestination()
        {
            if (!HasDestination)
                return;

            HasDestination = false;
            Destination = default;
            RequestVersion++;
            ResetExecutionState();
        }

        public void SetState(EUnitNavigationState state)
        {
            State = state;
        }

        public void SetPathQueryStatus(EPathQueryStatus status)
        {
            LastPathQueryStatus = status;
        }

        public void SetBlocked(EUnitNavigationBlockReason reason)
        {
            State = EUnitNavigationState.Blocked;
            BlockReason = reason;
        }

        public void ClearBlockReason()
        {
            BlockReason = EUnitNavigationBlockReason.None;
        }

        public void ResetExecutionState()
        {
            State = EUnitNavigationState.Idle;
            LastPathQueryStatus = null;
            BlockReason = EUnitNavigationBlockReason.None;
        }
    }
}
