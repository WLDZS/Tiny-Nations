using System.Collections.Generic;
using BorFramework;

namespace GameLogic.Units.Effects
{
    internal sealed class GameEffectComp : Comp
    {
        private readonly List<ActiveGameEffect> _activeEffects = new();

        public IReadOnlyList<ActiveGameEffect> ActiveEffects => _activeEffects;

        public void Add(ActiveGameEffect effect)
        {
            _activeEffects.Add(effect);
        }

        public void RemoveAt(int index)
        {
            _activeEffects.RemoveAt(index);
        }

        public void Clear()
        {
            _activeEffects.Clear();
        }
    }
}
