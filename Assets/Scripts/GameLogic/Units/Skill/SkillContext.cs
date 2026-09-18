using BorFramework;
using UnityEngine;

namespace GameLogic.Units.Skills
{
    internal readonly struct SkillContext
    {
        public Entity Owner { get; }

        public Vector3 Origin { get; }

        public Transform Target { get; }

        public bool HasTarget => Target != null;

        public SkillContext(Entity owner, Vector3 origin, Transform target)
        {
            Owner = owner;
            Origin = origin;
            Target = target;
        }
    }
}
