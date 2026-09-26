using GameLogic.Units.Common;
using UnityEngine;

namespace GameLogic.Units.Skills
{
    internal readonly struct SkillRuntimeContext
    {
        private readonly UnitViewComp _ownerView;

        public UnitEntity Owner { get; }

        public Transform OwnerWorldPositionTransform { get; }

        public float HorizontalFacingSign => _ownerView?.HorizontalFacingSign ?? 1f;

        public IUnitQuery UnitQuery { get; }

        public IUnitRelationResolver RelationResolver { get; }

        public SkillRuntimeContext(
            UnitEntity owner,
            Transform ownerWorldPositionTransform,
            UnitViewComp ownerView,
            IUnitQuery unitQuery,
            IUnitRelationResolver relationResolver)
        {
            Owner = owner;
            OwnerWorldPositionTransform = ownerWorldPositionTransform;
            _ownerView = ownerView;
            UnitQuery = unitQuery;
            RelationResolver = relationResolver;
        }
    }
}
