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
        private readonly Vector3[] _corners = new Vector3[4];
        private SerializedProperty _querySize;
        private SerializedProperty _queryOffset;
        private Transform _previewOrigin;
        private bool _showScenePreview = true;
        private bool _previewFacingLeft;

        private void OnEnable()
        {
            _querySize = serializedObject.FindProperty("_querySize");
            _queryOffset = serializedObject.FindProperty("_queryOffset");
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
            _previewFacingLeft = EditorGUILayout.Toggle("朝向左侧", _previewFacingLeft);
            EditorGUILayout.HelpBox(
                "蓝色实心区域就是 Physics2D.OverlapBox 的实际查询范围。"
                + "预览基准为空时，Prefab Mode 使用预制体根节点，普通场景使用世界原点。"
                + "Offset 以朝右为基准，勾选朝向左侧会镜像 X 偏移。"
                + "可在 Scene 中拖动中心与缩放手柄直接修改 Offset 和 Size。",
                MessageType.Info);

            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_showScenePreview || target == null)
                return;

            serializedObject.Update();
            Vector2 size = _querySize.vector2Value;
            if (size.x <= 0f || size.y <= 0f)
                return;

            Vector3 origin = ResolvePreviewOrigin();
            Vector2 offset = _queryOffset.vector2Value;
            float facingSign = _previewFacingLeft ? -1f : 1f;
            Vector3 center = origin + new Vector3(
                offset.x * facingSign,
                offset.y,
                0f);
            DrawSolidRange(center, size);
            DrawEditHandles(origin, center, size, facingSign);
        }

        private Vector3 ResolvePreviewOrigin()
        {
            if (_previewOrigin != null)
                return _previewOrigin.position;

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot != null)
                return prefabStage.prefabContentsRoot.transform.position;

            return Vector3.zero;
        }

        private void DrawSolidRange(Vector3 center, Vector2 size)
        {
            Vector2 halfSize = size * 0.5f;
            _corners[0] = center + new Vector3(-halfSize.x, -halfSize.y, 0f);
            _corners[1] = center + new Vector3(-halfSize.x, halfSize.y, 0f);
            _corners[2] = center + new Vector3(halfSize.x, halfSize.y, 0f);
            _corners[3] = center + new Vector3(halfSize.x, -halfSize.y, 0f);

            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            Handles.DrawSolidRectangleWithOutline(
                _corners,
                PreviewFillColor,
                PreviewOutlineColor);
            Handles.zTest = previousZTest;
        }

        private void DrawEditHandles(
            Vector3 origin,
            Vector3 center,
            Vector2 size,
            float facingSign)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 editedCenter = Handles.PositionHandle(center, Quaternion.identity);
            Vector3 editedSize = Handles.ScaleHandle(
                new Vector3(size.x, size.y, 1f),
                editedCenter,
                Quaternion.identity,
                HandleUtility.GetHandleSize(editedCenter));

            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(target, "修改近战攻击查询范围");
            _queryOffset.vector2Value = new Vector2(
                (editedCenter.x - origin.x) * facingSign,
                editedCenter.y - origin.y);
            _querySize.vector2Value = new Vector2(
                Mathf.Max(0.01f, Mathf.Abs(editedSize.x)),
                Mathf.Max(0.01f, Mathf.Abs(editedSize.y)));
            serializedObject.ApplyModifiedProperties();
            SceneView.RepaintAll();
        }
    }
}
