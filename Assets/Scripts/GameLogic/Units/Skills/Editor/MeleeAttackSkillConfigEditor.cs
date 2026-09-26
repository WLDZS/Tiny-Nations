using GameLogic.Units.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameLogic.Units.EditorTools
{
    [CustomEditor(typeof(MeleeAttackSkillConfig))]
    internal sealed class MeleeAttackSkillConfigEditor : UnityEditor.Editor
    {
        private static readonly Color PreviewFillColor = new(0.05f, 0.75f, 1f, 0.28f);
        private static readonly Color PreviewOutlineColor = new(0.1f, 0.85f, 1f, 0.95f);
        private Transform _previewOrigin;
        private bool _showScenePreview = true;

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("攻击范围预览", EditorStyles.boldLabel);
            _showScenePreview = EditorGUILayout.Toggle("在 Scene 中显示", _showScenePreview);
            _previewOrigin = (Transform)EditorGUILayout.ObjectField(
                "预览基准",
                _previewOrigin,
                typeof(Transform),
                true);
            EditorGUILayout.HelpBox(
                "蓝色圆形显示攻击者身体半径加技能攻击范围。目标身体半径也会计入实际距离。"
                + "预览基准为空时，Prefab Mode 使用当前 Prefab，普通场景使用世界原点。",
                MessageType.Info);

            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_showScenePreview || target is not MeleeAttackSkillConfig config)
                return;

            Transform origin = ResolvePreviewOrigin();
            Vector3 center = origin != null ? origin.position : Vector3.zero;
            float bodyRadius = 0f;
            Collider2D bodyCollider = origin != null
                ? origin.GetComponentInParent<Collider2D>()
                : null;
            if (bodyCollider != null && bodyCollider.enabled)
            {
                Bounds bounds = bodyCollider.bounds;
                center = bounds.center;
                bodyRadius = bodyCollider is CircleCollider2D
                    ? Mathf.Max(bounds.extents.x, bounds.extents.y)
                    : bounds.extents.magnitude;
            }

            float radius = bodyRadius + config.AttackRange;
            if (radius <= 0f)
                return;

            CompareFunction previousZTest = Handles.zTest;
            Color previousColor = Handles.color;
            Handles.zTest = CompareFunction.Always;
            Handles.color = PreviewFillColor;
            Handles.DrawSolidDisc(center, Vector3.forward, radius);
            Handles.color = PreviewOutlineColor;
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            Handles.color = previousColor;
            Handles.zTest = previousZTest;
        }

        private Transform ResolvePreviewOrigin()
        {
            if (_previewOrigin != null)
                return _previewOrigin;

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null || prefabStage.prefabContentsRoot == null)
                return null;

            Transform root = prefabStage.prefabContentsRoot.transform;
            Transform worldPositionTransform = root.Find("WordPos");
            return worldPositionTransform != null ? worldPositionTransform : root;
        }
    }
}
