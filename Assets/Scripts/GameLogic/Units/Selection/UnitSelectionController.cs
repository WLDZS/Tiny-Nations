using System.Collections.Generic;
using BorFramework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameLogic.Units
{
    [RequireComponent(typeof(Camera))]
    public sealed class UnitSelectionController : MonoBehaviour
    {
        private const float DragThresholdPixels = 2f;
        private const float SelectionBorderWidth = 1f;
        private static readonly Color SelectionFillColor = new(1f, 1f, 1f, 0.12f);
        private static readonly Color SelectionBorderColor = new(1f, 1f, 1f, 0.9f);

        private readonly HashSet<UnitEntity> _selectedUnits = new();
        private readonly List<UnitEntity> _candidateUnits = new();
        private readonly List<UnitEntity> _staleUnits = new();
        private MaterialPropertyBlock _propertyBlock;
        private Camera _camera;
        private UnitSystem _unitSystem;
        private int _selectionOutlineId;
        private Vector2 _dragStart;
        private Vector2 _dragEnd;
        private bool _isPointerDown;
        private bool _isDragging;
        private bool _wasLeftButtonPressed;
        private bool _wasRightButtonPressed;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _propertyBlock = new MaterialPropertyBlock();
            _selectionOutlineId = Shader.PropertyToID("_SelectionOutline");
        }

        private void Update()
        {
            if (_unitSystem == null)
            {
                IGameSystemModule systems = GameHub.Ins.GetModule<IGameSystemModule>();
                _unitSystem = systems?.GetSystem<IUnitSystem>() as UnitSystem;
            }

            PruneSelection();

            Mouse mouse = Mouse.current;
            if (mouse == null || _unitSystem == null)
            {
                _isPointerDown = false;
                _isDragging = false;
                _wasLeftButtonPressed = false;
                _wasRightButtonPressed = false;
                return;
            }

            Vector2 mousePosition = mouse.position.ReadValue();
            bool leftButtonPressed = mouse.leftButton.isPressed;
            bool rightButtonPressed = mouse.rightButton.isPressed;
            if (rightButtonPressed && !_wasRightButtonPressed
                && _selectedUnits.Count > 0
                && _camera.pixelRect.Contains(mousePosition))
            {
                IssueMoveCommand(mousePosition);
            }

            if (leftButtonPressed && !_wasLeftButtonPressed
                && _camera.pixelRect.Contains(mousePosition))
            {
                _dragStart = mousePosition;
                _dragEnd = mousePosition;
                _isPointerDown = true;
                _isDragging = false;
            }

            _wasLeftButtonPressed = leftButtonPressed;
            _wasRightButtonPressed = rightButtonPressed;

            if (!_isPointerDown)
                return;

            Rect viewRect = _camera.pixelRect;
            _dragEnd = new Vector2(
                Mathf.Clamp(mousePosition.x, viewRect.xMin, viewRect.xMax),
                Mathf.Clamp(mousePosition.y, viewRect.yMin, viewRect.yMax));

            if (!_isDragging && (_dragEnd - _dragStart).sqrMagnitude >= DragThresholdPixels * DragThresholdPixels)
                _isDragging = true;

            if (leftButtonPressed)
                return;

            ApplySelection();
            _isPointerDown = false;
            _isDragging = false;
        }

        private void OnGUI()
        {
            if (!_isDragging || Event.current.type != EventType.Repaint)
                return;

            Rect selection = GetSelectionRect();
            selection.y = Screen.height - selection.yMax;

            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.depth = 40;
            GUI.color = SelectionFillColor;
            GUI.DrawTexture(selection, Texture2D.whiteTexture);

            GUI.color = SelectionBorderColor;
            GUI.DrawTexture(new Rect(selection.xMin, selection.yMin, selection.width, SelectionBorderWidth),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(selection.xMin, selection.yMax - SelectionBorderWidth,
                selection.width, SelectionBorderWidth), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(selection.xMin, selection.yMin, SelectionBorderWidth,
                selection.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(selection.xMax - SelectionBorderWidth, selection.yMin,
                SelectionBorderWidth, selection.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        private void OnDisable()
        {
            ClearSelection();
            _isPointerDown = false;
            _isDragging = false;
            _wasLeftButtonPressed = false;
            _wasRightButtonPressed = false;
        }

        private void ApplySelection()
        {
            ClearSelection();
            _unitSystem.CopyActiveUnits(_candidateUnits);
            Rect selection = GetSelectionRect();

            for (int i = 0; i < _candidateUnits.Count; i++)
            {
                UnitEntity unit = _candidateUnits[i];
                if (!_unitSystem.TryGetSelectionRenderer(unit, out SpriteRenderer renderer))
                    continue;

                Bounds bounds = renderer.bounds;
                Vector3 screenMin = _camera.WorldToScreenPoint(bounds.min);
                Vector3 screenMax = _camera.WorldToScreenPoint(bounds.max);
                if (screenMin.z <= 0f || screenMax.z <= 0f)
                    continue;

                Rect unitRect = Rect.MinMaxRect(
                    Mathf.Min(screenMin.x, screenMax.x),
                    Mathf.Min(screenMin.y, screenMax.y),
                    Mathf.Max(screenMin.x, screenMax.x),
                    Mathf.Max(screenMin.y, screenMax.y));
                bool isInside = _isDragging
                    ? selection.Overlaps(unitRect, true)
                    : unitRect.Contains(_dragEnd);
                if (!isInside)
                    continue;

                _selectedUnits.Add(unit);
                SetOutline(renderer, true);
                if (!_isDragging)
                    break;
            }
        }

        private void PruneSelection()
        {
            if (_unitSystem == null || _selectedUnits.Count == 0)
                return;

            _staleUnits.Clear();
            foreach (UnitEntity unit in _selectedUnits)
            {
                if (!_unitSystem.IsActiveUnit(unit))
                    _staleUnits.Add(unit);
            }

            for (int i = 0; i < _staleUnits.Count; i++)
                _selectedUnits.Remove(_staleUnits[i]);
        }

        private void ClearSelection()
        {
            foreach (UnitEntity unit in _selectedUnits)
            {
                if (_unitSystem != null && _unitSystem.IsActiveUnit(unit))
                {
                    if (_unitSystem.TryGetSelectionRenderer(unit, out SpriteRenderer renderer))
                        SetOutline(renderer, false);
                }
            }

            _selectedUnits.Clear();
        }

        private void SetOutline(SpriteRenderer renderer, bool isSelected)
        {
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(_selectionOutlineId, isSelected ? 1f : 0f);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private void IssueMoveCommand(Vector2 mousePosition)
        {
            Ray ray = _camera.ScreenPointToRay(mousePosition);
            if (Mathf.Abs(ray.direction.z) < 0.0001f)
                return;

            float distance = -ray.origin.z / ray.direction.z;
            if (distance < 0f)
                return;

            Vector2 destination = ray.GetPoint(distance);
            float spacing = 0.52f;
            foreach (UnitEntity unit in _selectedUnits)
            {
                if (_unitSystem.TryGetUnitAttackFootprint(unit, out _, out float radius))
                    spacing = Mathf.Max(spacing, Mathf.Max(0.22f, radius) * 2f + 0.08f);
            }

            bool accepted = false;
            int failedCount = 0;
            var assignedPositions = new List<Vector2>();
            var assignedRadii = new List<float>();
            int candidateCount = Mathf.Max(24, _selectedUnits.Count * 4);
            foreach (UnitEntity unit in _selectedUnits)
            {
                if (!_unitSystem.TryGetUnitAttackFootprint(unit, out _, out float radius))
                {
                    failedCount++;
                    continue;
                }

                float crowdRadius = Mathf.Max(0.22f, radius);
                bool unitAccepted = false;
                for (int index = 0; index < candidateCount; index++)
                {
                    float angle = index * 2.3999632f;
                    float distanceFromClick = spacing * Mathf.Sqrt(index);
                    Vector2 candidate = destination + new Vector2(
                        Mathf.Cos(angle), Mathf.Sin(angle)) * distanceFromClick;
                    if (OverlapsAssignedPosition(
                            candidate, crowdRadius, assignedPositions, assignedRadii)
                        || !_unitSystem.TryIssueMoveCommand(unit, candidate))
                    {
                        continue;
                    }

                    assignedPositions.Add(candidate);
                    assignedRadii.Add(crowdRadius);
                    accepted = true;
                    unitAccepted = true;
                    break;
                }

                if (!unitAccepted)
                    failedCount++;
            }

            if (!accepted)
                Debug.LogWarning($"移动命令失败：目标 {destination} 不可达或不在可行走区域。", this);
            else if (failedCount > 0)
                Debug.LogWarning($"移动命令有 {failedCount} 个单位未找到可达落点。", this);
        }

        private static bool OverlapsAssignedPosition(
            Vector2 candidate,
            float radius,
            List<Vector2> positions,
            List<float> radii)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                float minimumDistance = radius + radii[i] + 0.08f;
                if ((candidate - positions[i]).sqrMagnitude
                    < minimumDistance * minimumDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private Rect GetSelectionRect()
        {
            return Rect.MinMaxRect(
                Mathf.Min(_dragStart.x, _dragEnd.x),
                Mathf.Min(_dragStart.y, _dragEnd.y),
                Mathf.Max(_dragStart.x, _dragEnd.x),
                Mathf.Max(_dragStart.y, _dragEnd.y));
        }
    }
}
