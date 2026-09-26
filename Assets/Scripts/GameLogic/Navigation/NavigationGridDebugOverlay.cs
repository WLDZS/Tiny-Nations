#if DEBUG
using UnityEngine;

namespace GameLogic.Navigation
{
    internal sealed class NavigationGridDebugOverlay
    {
        private const float BorderWidth = 1f;
        private static readonly Color WalkableColor = new(0.1f, 0.9f, 0.3f, 0.2f);
        private static readonly Color CollisionColor = new(1f, 0.2f, 0.15f, 0.35f);
        private static readonly Color NoGroundColor = new(0.35f, 0.4f, 0.45f, 0.2f);
        private static readonly Color BorderColor = new(0f, 0f, 0f, 0.65f);

        private readonly NavigationMap _map;

        public NavigationGridDebugOverlay(NavigationMap map)
        {
            _map = map;
        }

        public void Draw()
        {
            if (_map == null || Event.current.type != EventType.Repaint)
                return;

            Camera camera = Camera.main;
            if (camera == null)
                return;

            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.depth = 60;

            foreach (Vector3Int position in _map.CellBounds.allPositionsWithin)
            {
                if (_map.TryGetCell(position, out NavigationCell cell))
                    DrawCell(camera, position, cell);
            }

            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        private void DrawCell(Camera camera, Vector3Int position, NavigationCell cell)
        {
            _map.GetCellWorldCorners(position, out Vector3 first, out Vector3 opposite);
            Vector3 firstScreen = camera.WorldToScreenPoint(first);
            Vector3 oppositeScreen = camera.WorldToScreenPoint(opposite);
            if (firstScreen.z <= 0f || oppositeScreen.z <= 0f)
                return;

            float left = Mathf.Min(firstScreen.x, oppositeScreen.x);
            float right = Mathf.Max(firstScreen.x, oppositeScreen.x);
            float top = Screen.height - Mathf.Max(firstScreen.y, oppositeScreen.y);
            float bottom = Screen.height - Mathf.Min(firstScreen.y, oppositeScreen.y);
            if (right < 0f || left > Screen.width || bottom < 0f || top > Screen.height)
                return;

            var rect = new Rect(left, top, right - left, bottom - top);
            GUI.color = cell.IsWalkable
                ? WalkableColor
                : cell.HasCollision
                    ? CollisionColor
                    : NoGroundColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = BorderColor;
            GUI.DrawTexture(new Rect(left, top, rect.width, BorderWidth), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(left, bottom - BorderWidth, rect.width, BorderWidth), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(left, top, BorderWidth, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(right - BorderWidth, top, BorderWidth, rect.height), Texture2D.whiteTexture);
        }
    }
}
#endif
