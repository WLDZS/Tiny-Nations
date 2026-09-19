using GameLogic.Units.Common;
using UnityEngine;

namespace GameLogic.Units.Skills
{
    internal readonly struct SkillRuntimeContext
    {
        private readonly UnitViewComp _ownerView;

        public UnitEntity Owner { get; }

        public Transform OwnerTransform { get; }

        public float HorizontalFacingSign => _ownerView?.HorizontalFacingSign ?? 1f;

        public IUnitQuery UnitQuery { get; }

        public IUnitRelationResolver RelationResolver { get; }

        public SkillRuntimeContext(
            UnitEntity owner,
            Transform ownerTransform,
            UnitViewComp ownerView,
            IUnitQuery unitQuery,
            IUnitRelationResolver relationResolver)
        {
            Owner = owner;
            OwnerTransform = ownerTransform;
            _ownerView = ownerView;
            UnitQuery = unitQuery;
            RelationResolver = relationResolver;
        }
    }
}
