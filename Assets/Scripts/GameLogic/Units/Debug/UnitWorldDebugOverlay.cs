#if DEBUG
using System.Collections.Generic;
using GameLogic.Navigation;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameLogic.Units
{
    internal sealed class UnitWorldDebugOverlay
    {
        private const int DotTextureSize = 9;
        private const float GridLineWidth = 1f;

        private static readonly Color WalkableFillColor = new(0.1f, 0.9f, 0.3f, 0.08f);
        private static readonly Color WalkableLineColor = new(0.1f, 0.9f, 0.3f, 0.55f);
        private static readonly Color BlockedFillColor = new(1f, 0.2f, 0.2f, 0.2f);
        private static readonly Color BlockedLineColor = new(1f, 0.2f, 0.2f, 0.8f);
        private static readonly Color UnitPointColor = new(0.1f, 0.9f, 1f, 1f);

        private readonly NavigationSystem _navigationSystem;
        private readonly UnitSystem _unitSystem;
        private readonly List<Vector3> _unitPositions = new();
        private Texture2D _dotTexture;
        private GUIStyle _coordinateStyle;

        public UnitWorldDebugOverlay(NavigationSystem navigationSystem, UnitSystem unitSystem)
        {
            _navigationSystem = navigationSystem;
            _unitSystem = unitSystem;
        }

        public void Draw(bool showNavigationGrid, bool showUnitPositions)
        {
            if ((!showNavigationGrid && !showUnitPositions)
                || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
                return;

            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.depth = 50;

            if (showNavigationGrid)
                DrawNavigationGrid(camera);

            if (showUnitPositions)
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

        private void DrawNavigationGrid(Camera camera)
        {
            NavigationMap map = _navigationSystem.DebugMap;
            Tilemap groundTilemap = map?.GroundTilemap;
            Tilemap collisionTilemap = map?.CollisionTilemap;
            if (groundTilemap == null || collisionTilemap == null)
                return;

            BoundsInt groundBounds = groundTilemap.cellBounds;
            BoundsInt collisionBounds = collisionTilemap.cellBounds;
            float mapPlaneDistance = Vector3.Dot(
                groundTilemap.transform.position - camera.transform.position,
                camera.transform.forward);
            if (mapPlaneDistance <= 0f)
                return;

            Vector3 viewBottomLeft = camera.ViewportToWorldPoint(
                new Vector3(0f, 0f, mapPlaneDistance));
            Vector3 viewTopRight = camera.ViewportToWorldPoint(
                new Vector3(1f, 1f, mapPlaneDistance));
            Vector3Int bottomLeftCell = map.WorldToCell(viewBottomLeft);
            Vector3Int topRightCell = map.WorldToCell(viewTopRight);
            int minX = Mathf.Max(
                Mathf.Min(groundBounds.xMin, collisionBounds.xMin),
                Mathf.Min(bottomLeftCell.x, topRightCell.x) - 1);
            int maxX = Mathf.Min(
                Mathf.Max(groundBounds.xMax, collisionBounds.xMax) - 1,
                Mathf.Max(bottomLeftCell.x, topRightCell.x) + 1);
            int minY = Mathf.Max(
                Mathf.Min(groundBounds.yMin, collisionBounds.yMin),
                Mathf.Min(bottomLeftCell.y, topRightCell.y) - 1);
            int maxY = Mathf.Min(
                Mathf.Max(groundBounds.yMax, collisionBounds.yMax) - 1,
                Mathf.Max(bottomLeftCell.y, topRightCell.y) + 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3Int cell = new(x, y, 0);
                    bool hasGround = groundTilemap.HasTile(cell);
                    bool hasCollision = collisionTilemap.HasTile(cell);
                    if (!hasGround && !hasCollision)
                        continue;

                    Tilemap sourceTilemap = hasGround ? groundTilemap : collisionTilemap;
                    DrawCell(camera, sourceTilemap, cell, !hasCollision && map.IsBaseWalkable(cell));
                }
            }
        }

        private static void DrawCell(
            Camera camera,
            Tilemap tilemap,
            Vector3Int cell,
            bool isWalkable)
        {
            Vector3 lowerCorner = camera.WorldToScreenPoint(tilemap.CellToWorld(cell));
            Vector3 upperCorner = camera.WorldToScreenPoint(
                tilemap.CellToWorld(cell + new Vector3Int(1, 1, 0)));
            if (lowerCorner.z <= 0f || upperCorner.z <= 0f)
                return;

            float x = Mathf.Min(lowerCorner.x, upperCorner.x);
            float y = Screen.height - Mathf.Max(lowerCorner.y, upperCorner.y);
            float width = Mathf.Abs(upperCorner.x - lowerCorner.x);
            float height = Mathf.Abs(upperCorner.y - lowerCorner.y);
            if (x + width < 0f || x > Screen.width || y + height < 0f || y > Screen.height)
                return;

            GUI.color = isWalkable ? WalkableFillColor : BlockedFillColor;
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = isWalkable ? WalkableLineColor : BlockedLineColor;
            GUI.DrawTexture(new Rect(x, y, width, GridLineWidth), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y, GridLineWidth, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + height - GridLineWidth, width, GridLineWidth), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + width - GridLineWidth, y, GridLineWidth, height), Texture2D.whiteTexture);
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
