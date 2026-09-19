using System.Collections.Generic;
using GameLogic.Units.Skills;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameLogic.Units.EditorTools
{
    [InitializeOnLoad]
    internal static class MeleeAttackDebugRangeDrawer
    {
        private static readonly Color ActiveFillColor = new(1f, 0.1f, 0.1f, 0.32f);
        private static readonly Color ActiveOutlineColor = new(1f, 0.15f, 0.15f, 0.95f);
        private static readonly Color InactiveFillColor = new(1f, 0.75f, 0.05f, 0.2f);
        private static readonly Color InactiveOutlineColor = new(1f, 0.75f, 0.05f, 0.9f);
        private static readonly List<int> StaleRangeIds = new();
        private static readonly Vector3[] Corners = new Vector3[4];

        static MeleeAttackDebugRangeDrawer()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnEditorUpdate()
        {
            if (MeleeAttackDebugRangeRegistry.HasActiveRanges)
                SceneView.RepaintAll();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode
                || state == PlayModeStateChange.EnteredEditMode)
            {
                MeleeAttackDebugRangeRegistry.Clear();
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            StaleRangeIds.Clear();
            int currentFrame = Time.frameCount;

            foreach (KeyValuePair<int, MeleeAttackDebugRange> entry
                     in MeleeAttackDebugRangeRegistry.ActiveRanges)
            {
                MeleeAttackDebugRange range = entry.Value;
                if (range.LastUpdatedFrame < currentFrame - 1)
                {
                    StaleRangeIds.Add(entry.Key);
                    continue;
                }

                DrawRange(range);
            }

            for (int i = 0; i < StaleRangeIds.Count; i++)
                MeleeAttackDebugRangeRegistry.Remove(StaleRangeIds[i]);
        }

        private static void DrawRange(MeleeAttackDebugRange range)
        {
            Vector2 halfSize = range.Size * 0.5f;
            Corners[0] = range.Center + new Vector2(-halfSize.x, -halfSize.y);
            Corners[1] = range.Center + new Vector2(-halfSize.x, halfSize.y);
            Corners[2] = range.Center + halfSize;
            Corners[3] = range.Center + new Vector2(halfSize.x, -halfSize.y);

            Color fillColor = range.IsHitWindowActive
                ? ActiveFillColor
                : InactiveFillColor;
            Color outlineColor = range.IsHitWindowActive
                ? ActiveOutlineColor
                : InactiveOutlineColor;
            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            Handles.DrawSolidRectangleWithOutline(Corners, fillColor, outlineColor);
            Handles.zTest = previousZTest;
        }
    }
}
