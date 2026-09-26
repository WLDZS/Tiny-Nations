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

        // 执行结果由 NavigationLogic 更新；修改请求本身不代表已经执行。
        public EUnitNavigationState State { get; private set; }

        public EPathQueryStatus? LastPathQueryStatus { get; private set; }

        public EUnitNavigationBlockReason BlockReason { get; private set; }

        public bool IsAvoidanceYielding { get; private set; }

        public void SetAvoidanceYielding(bool isYielding)
        {
            IsAvoidanceYielding = isYielding;
        }

        public void BeginDestination(Vector2 destination)
        {
            Destination = destination;
            HasDestination = true;
            RequestVersion++;
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
            IsAvoidanceYielding = false;
            Destination = default;
            RequestVersion++;
        }

        /// <summary>由导航执行器一起更新状态和原因；进入 Idle 时清除上次查询结果。</summary>
        public void SetExecutionState(
            EUnitNavigationState state,
            EUnitNavigationBlockReason blockReason = EUnitNavigationBlockReason.None)
        {
            State = state;
            BlockReason = blockReason;
            if (state == EUnitNavigationState.Idle)
            {
                IsAvoidanceYielding = false;
                LastPathQueryStatus = null;
            }
        }

        public void SetPathQueryStatus(EPathQueryStatus status)
        {
            LastPathQueryStatus = status;
        }
    }
}
