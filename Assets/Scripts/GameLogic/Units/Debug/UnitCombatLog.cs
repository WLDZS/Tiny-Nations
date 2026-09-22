#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using BorFramework;
using GameLogic.Units.Common;
using UnityEngine;

namespace GameLogic.Units
{
    internal sealed class UnitCombatLog : IDisposable
    {
        private readonly IEventModule _events;
        private readonly IUnitQuery _units;

        public UnitCombatLog(IEventModule events, IUnitQuery units)
        {
            _events = events;
            _units = units;
            _events.Subscribe<UnitDamageEvent>(OnDamaged);
            _events.Subscribe<UnitDeathEvent>(OnDied);
        }

        public void Dispose()
        {
            _events.Unsubscribe<UnitDamageEvent>(OnDamaged);
            _events.Unsubscribe<UnitDeathEvent>(OnDied);
        }

        private void OnDamaged(UnitDamageEvent damage)
        {
            string health = damage.Target != null
                            && damage.Target.Attributes.TryGetCurrentValue(EUnitAttributeType.Health, out float value)
                ? value.ToString("0.##")
                : "未知";
            string effect = damage.GameEffect != null ? damage.GameEffect.name : "未知效果";
            string message = $"[单位伤害] {GetName(damage.Source, "环境")} 对 {GetName(damage.Target, "未知单位")}"
                             + $" 造成 {damage.ActualDamage:0.##} 点伤害，剩余生命：{health}，效果：{effect}";
            Write(message, damage.Target);
        }

        private void OnDied(UnitDeathEvent death)
        {
            string effect = death.KillingEffect != null ? death.KillingEffect.name : "未知效果";
            string message = $"[单位死亡] {GetName(death.Target, "未知单位")}"
                             + $" 被 {GetName(death.Source, "环境")} 击杀，效果：{effect}";
            Write(message, death.Target);
        }

        private string GetName(UnitEntity unit, string fallback)
        {
            return _units.TryGetUnitTransform(unit, out Transform transform)
                ? $"{transform.name}[Team {unit.Team.TeamId}]"
                : fallback;
        }

        private void Write(string message, UnitEntity target)
        {
            _units.TryGetUnitTransform(target, out Transform transform);
            Debug.Log(message, transform);
        }
    }
}
#endif
