using UnityEngine;
using UnityEngine.InputSystem;

namespace GameLogic.CameraControl
{
    [RequireComponent(typeof(Camera))]
    public sealed class RtsCameraController : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        private float _edgeWidth = 24f;

        [SerializeField, Min(0f)]
        private float _moveSpeed = 12f;

        [SerializeField, Min(0.01f)]
        private float _moveSmoothTime = 0.18f;

        [SerializeField, Range(0.01f, 0.99f)]
        private float _zoomFactor = 0.85f;

        [SerializeField, Min(0.01f)]
        private float _zoomSmoothTime = 0.12f;

        [SerializeField, Min(0.01f)]
        private float _minOrthographicSize = 3f;

        [SerializeField, Min(0.01f)]
        private float _maxOrthographicSize = 15f;

        private Camera _camera;
        private Vector3 _targetPosition;
        private Vector3 _moveVelocity;
        private float _targetOrthographicSize;
        private float _zoomVelocity;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (!_camera.orthographic)
            {
                Debug.LogWarning("RTS摄像机控制需要正交摄像机。", this);
                enabled = false;
            }

            _targetPosition = transform.position;
            _targetOrthographicSize = _camera.orthographicSize;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (Application.isFocused && mouse != null)
                ReadInput(mouse);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                _targetPosition,
                ref _moveVelocity,
                _moveSmoothTime);

            _camera.orthographicSize = Mathf.SmoothDamp(
                _camera.orthographicSize,
                _targetOrthographicSize,
                ref _zoomVelocity,
                _zoomSmoothTime);
        }

        private void ReadInput(Mouse mouse)
        {
            Vector2 mousePosition = mouse.position.ReadValue();
            Rect viewRect = _camera.pixelRect;
            if (mousePosition.x < viewRect.xMin || mousePosition.x >= viewRect.xMax
                || mousePosition.y < viewRect.yMin || mousePosition.y >= viewRect.yMax)
                return;

            Vector2 moveDirection = Vector2.zero;
            if (mousePosition.x < viewRect.xMin + _edgeWidth)
                moveDirection.x = -1f;
            else if (mousePosition.x >= viewRect.xMax - _edgeWidth)
                moveDirection.x = 1f;

            if (mousePosition.y < viewRect.yMin + _edgeWidth)
                moveDirection.y = -1f;
            else if (mousePosition.y >= viewRect.yMax - _edgeWidth)
                moveDirection.y = 1f;

            if (moveDirection != Vector2.zero)
                _targetPosition += (Vector3)(moveDirection.normalized * (_moveSpeed * Time.deltaTime));

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f))
                return;

            float zoomFactor = scroll > 0f ? _zoomFactor : 1f / _zoomFactor;
            float minSize = Mathf.Min(_minOrthographicSize, _maxOrthographicSize);
            float maxSize = Mathf.Max(_minOrthographicSize, _maxOrthographicSize);
            _targetOrthographicSize = Mathf.Clamp(_targetOrthographicSize * zoomFactor, minSize, maxSize);
        }
    }
}
