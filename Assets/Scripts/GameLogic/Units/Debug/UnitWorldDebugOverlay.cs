#if DEBUG
using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Units
{
    internal sealed class UnitWorldDebugOverlay
    {
        private const int DotTextureSize = 9;
        private static readonly Color UnitPointColor = new(0.1f, 0.9f, 1f, 1f);

        private readonly UnitSystem _unitSystem;
        private readonly List<Vector3> _unitPositions = new();
        private Texture2D _dotTexture;
        private GUIStyle _coordinateStyle;

        public UnitWorldDebugOverlay(UnitSystem unitSystem)
        {
            _unitSystem = unitSystem;
        }

        public void Draw(bool showUnitPositions)
        {
            if (!showUnitPositions || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
                return;

            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.depth = 50;

            DrawUnitPositions(camera);

            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        public void Dispose()
        {
            if (_dotTexture != null)
                Object.Destroy(_dotTexture);

            _dotTexture = null;
            _unitPositions.Clear();
        }

        private void DrawUnitPositions(Camera camera)
        {
            _unitSystem.CopyDebugUnitPositions(_unitPositions);
            if (_unitPositions.Count == 0)
                return;

            if (_dotTexture == null)
                _dotTexture = CreateDotTexture();

            if (_coordinateStyle == null)
            {
                _coordinateStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10
                };
                _coordinateStyle.normal.textColor = UnitPointColor;
            }

            for (int index = 0; index < _unitPositions.Count; index++)
            {
                Vector3 worldPosition = _unitPositions[index];
                Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
                if (screenPosition.z <= 0f
                    || screenPosition.x < 0f
                    || screenPosition.x > Screen.width
                    || screenPosition.y < 0f
                    || screenPosition.y > Screen.height)
                {
                    continue;
                }

                float x = screenPosition.x;
                float y = Screen.height - screenPosition.y;
                GUI.color = UnitPointColor;
                GUI.DrawTexture(
                    new Rect(x - DotTextureSize * 0.5f, y - DotTextureSize * 0.5f,
                        DotTextureSize, DotTextureSize),
                    _dotTexture);
                GUI.color = Color.white;
                GUI.Label(
                    new Rect(x + 6f, y - 12f, 110f, 20f),
                    $"({worldPosition.x:0.##}, {worldPosition.y:0.##})",
                    _coordinateStyle);
            }
        }

        private static Texture2D CreateDotTexture()
        {
            var texture = new Texture2D(
                DotTextureSize,
                DotTextureSize,
                TextureFormat.RGBA32,
                false);
            texture.filterMode = FilterMode.Point;
            float center = (DotTextureSize - 1) * 0.5f;
            float radiusSquared = center * center;
            for (int y = 0; y < DotTextureSize; y++)
            {
                for (int x = 0; x < DotTextureSize; x++)
                {
                    float horizontalDistance = x - center;
                    float verticalDistance = y - center;
                    float distanceSquared = horizontalDistance * horizontalDistance
                                            + verticalDistance * verticalDistance;
                    texture.SetPixel(
                        x,
                        y,
                        distanceSquared <= radiusSquared ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return texture;
        }
    }
}
#endif
