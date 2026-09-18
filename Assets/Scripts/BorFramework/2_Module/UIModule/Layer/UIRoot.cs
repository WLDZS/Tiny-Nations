using System.Collections.Generic;
using UnityEngine;

namespace BorFramework
{
    public class UIRoot : MonoBehaviour
    {
        private readonly Dictionary<EUILayer, Transform> _layers = new();
        private readonly Dictionary<EUILayer, Transform[]> _subLayers = new();

        public void BuildLayers()
        {
            if (_layers.Count > 0)
                return;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            CreateLayer(EUILayer.World);
            CreateLayer(EUILayer.Screen);
            CreateLayer(EUILayer.Window);
            CreateLayer(EUILayer.Popup);
            CreateLayer(EUILayer.GlobalOverlay);
        }

        public Transform GetLayer(EUILayer layer)
        {
            _layers.TryGetValue(layer, out var layerRoot);
            return layerRoot;
        }

        public Transform GetLayer(EUILayer layer, EUISubLayer subLayer)
        {
            if (!_subLayers.TryGetValue(layer, out var subLayers))
                return null;

            var index = (int)subLayer;
            if (index < 0 || index >= subLayers.Length)
                return null;

            return subLayers[index];
        }

        private void CreateLayer(EUILayer layer)
        {
            var layerRoot = CreateChild(layer.ToString(), transform);
            var subLayers = new Transform[3];

            subLayers[(int)EUISubLayer.Layer1] = CreateChild(nameof(EUISubLayer.Layer1), layerRoot);
            subLayers[(int)EUISubLayer.Layer2] = CreateChild(nameof(EUISubLayer.Layer2), layerRoot);
            subLayers[(int)EUISubLayer.Layer3] = CreateChild(nameof(EUISubLayer.Layer3), layerRoot);

            _layers.Add(layer, layerRoot);
            _subLayers.Add(layer, subLayers);
        }

        private static Transform CreateChild(string childName, Transform parent)
        {
            var child = new GameObject(childName, typeof(RectTransform));
            var rectTransform = child.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return rectTransform;
        }
    }
}
