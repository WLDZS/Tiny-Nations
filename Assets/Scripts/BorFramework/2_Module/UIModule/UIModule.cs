using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BorFramework
{
    public sealed class UIModule : IUIModule
    {
        private readonly Dictionary<Type, UIEntry> _elements = new();
        private readonly Stack<Type> _screenStack = new();
        private readonly Stack<Type> _windowStack = new();
        private readonly IResourceModule _resourceModule;

        public UIRoot UIRoot { get; private set; }

        public UIModule(IResourceModule resourceModule)
        {
            _resourceModule = resourceModule;
        }

        public void Init()
        {
            var go = new GameObject("UIRoot", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(go);

            UIRoot = go.AddComponent<UIRoot>();
            UIRoot.BuildLayers();
        }

        public void Start() { }

        public void Stop()
        {
            _windowStack.Clear();
            _screenStack.Clear();

            foreach (var entry in _elements.Values)
                CloseEntry(entry);
        }

        public void Dispose()
        {
            Stop();

            foreach (var entry in _elements.Values)
                DestroyEntry(entry);

            _elements.Clear();

            if (UIRoot != null)
                UnityEngine.Object.Destroy(UIRoot.gameObject);

            UIRoot = null;
        }

        public void Register<TView, TViewModel>(
            string address,
            EUILayer layer,
            Func<TViewModel> viewModelFactory,
            EUISubLayer subLayer = EUISubLayer.Layer1)
            where TView : UIView<TViewModel>
            where TViewModel : UIViewModelBase
        {
            var viewType = typeof(TView);
            if (string.IsNullOrWhiteSpace(address) || viewModelFactory == null || _elements.ContainsKey(viewType))
                return;

            _elements.Add(viewType, new UIEntry(
                viewType,
                address,
                layer,
                subLayer,
                () => viewModelFactory()));
        }

        public async UniTask<TView> OpenAsync<TView>() where TView : UIViewBase
        {
            if (!_elements.TryGetValue(typeof(TView), out var entry))
            {
                Debug.LogWarning($"UI 尚未注册：{typeof(TView).Name}");
                return null;
            }

            entry.OpenRequested = true;
            var view = await LoadViewAsync(entry);
            if (view == null || !entry.OpenRequested)
                return null;

            if (!entry.IsOpen)
            {
                entry.ViewModel.OnOpen();
                if (!view.BindViewModel(entry.ViewModel))
                {
                    entry.ViewModel.OnClose();
                    Debug.LogError($"UI ViewModel 类型不匹配：{typeof(TView).Name}");
                    return null;
                }

                entry.IsOpen = true;
                view.gameObject.SetActive(true);
            }

            return view as TView;
        }

        public async UniTask<TView> PushScreenAsync<TView>() where TView : UIViewBase
        {
            if (!TryGetEntry<TView>(EUILayer.Screen, out var entry))
                return null;

            var view = await OpenAsync<TView>();
            if (view == null || !entry.IsOpen || _screenStack.Contains(typeof(TView)))
                return view;

            while (_windowStack.Count > 0)
            {
                var windowType = _windowStack.Pop();
                if (_elements.TryGetValue(windowType, out var window))
                    CloseEntry(window);
            }

            if (_screenStack.Count > 0 && _elements.TryGetValue(_screenStack.Peek(), out var current))
                current.ViewModel?.OnPause();

            _screenStack.Push(typeof(TView));
            return view;
        }

        public async UniTask<TView> OpenWindowAsync<TView>() where TView : UIViewBase
        {
            if (_screenStack.Count == 0 || !TryGetEntry<TView>(EUILayer.Window, out var entry))
                return null;

            var view = await OpenAsync<TView>();
            if (view != null && entry.IsOpen && !_windowStack.Contains(typeof(TView)))
                _windowStack.Push(typeof(TView));

            return view;
        }

        public void Close<TView>() where TView : UIViewBase
        {
            var viewType = typeof(TView);
            if (!_elements.TryGetValue(viewType, out var entry))
                return;

            if (_windowStack.Count > 0 && _windowStack.Peek() == viewType)
            {
                _windowStack.Pop();
                CloseEntry(entry);
                return;
            }

            if (_screenStack.Count > 0 && _screenStack.Peek() == viewType)
            {
                PopScreen();
                return;
            }

            RemoveFromStack(_windowStack, viewType);
            RemoveFromStack(_screenStack, viewType);
            CloseEntry(entry);
        }

        public void Destroy<TView>() where TView : UIViewBase
        {
            var viewType = typeof(TView);
            if (!_elements.TryGetValue(viewType, out var entry))
                return;

            Close<TView>();
            DestroyEntry(entry);
            _elements.Remove(viewType);
        }

        public bool TryGet<TView>(out TView view) where TView : UIViewBase
        {
            if (_elements.TryGetValue(typeof(TView), out var entry))
            {
                view = entry.View as TView;
                return view != null;
            }

            view = null;
            return false;
        }

        public bool IsOpen<TView>() where TView : UIViewBase
        {
            return _elements.TryGetValue(typeof(TView), out var entry) && entry.IsOpen;
        }

        public bool PopScreen()
        {
            if (_screenStack.Count == 0)
                return false;

            while (_windowStack.Count > 0)
            {
                var windowType = _windowStack.Pop();
                if (_elements.TryGetValue(windowType, out var window))
                    CloseEntry(window);
            }

            var screenType = _screenStack.Pop();
            if (_elements.TryGetValue(screenType, out var screen))
                CloseEntry(screen);

            if (_screenStack.Count > 0 && _elements.TryGetValue(_screenStack.Peek(), out var previous))
                previous.ViewModel?.OnResume();

            return true;
        }

        public void Back()
        {
            if (_windowStack.Count > 0)
            {
                var windowType = _windowStack.Pop();
                if (_elements.TryGetValue(windowType, out var window))
                    CloseEntry(window);

                return;
            }

            PopScreen();
        }

        private async UniTask<UIViewBase> LoadViewAsync(UIEntry entry)
        {
            if (entry.View != null)
                return entry.View;

            if (entry.IsLoading)
                return await entry.LoadTask;

            entry.IsLoading = true;
            entry.LoadTask = LoadViewInternalAsync(entry).Preserve();
            var view = await entry.LoadTask;
            entry.IsLoading = false;
            return view;
        }

        private async UniTask<UIViewBase> LoadViewInternalAsync(UIEntry entry)
        {
            if (_resourceModule == null || UIRoot == null)
                return null;

            var parent = UIRoot.GetLayer(entry.Layer, entry.SubLayer);
            if (parent == null)
                return null;

            var lease = await _resourceModule.LoadAssetAsync<GameObject>(entry.Address);
            if (lease == null)
                return null;

            if (entry.IsDestroyed)
            {
                lease.Dispose();
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(lease.Asset, parent, false);
            var view = instance.GetComponent(entry.ViewType) as UIViewBase;
            if (view == null)
            {
                UnityEngine.Object.Destroy(instance);
                lease.Dispose();
                Debug.LogError($"UI Prefab 缺少组件：{entry.ViewType.Name}，Address: {entry.Address}");
                return null;
            }

            var viewModel = entry.ViewModelFactory();
            if (viewModel == null)
            {
                UnityEngine.Object.Destroy(instance);
                lease.Dispose();
                Debug.LogError($"UI ViewModel 创建失败：{entry.ViewType.Name}");
                return null;
            }

            instance.SetActive(false);
            entry.Lease = lease;
            entry.View = view;
            entry.ViewModel = viewModel;
            return view;
        }

        private bool TryGetEntry<TView>(EUILayer layer, out UIEntry entry) where TView : UIViewBase
        {
            if (_elements.TryGetValue(typeof(TView), out entry) && entry.Layer == layer)
                return true;

            entry = null;
            return false;
        }

        private static void CloseEntry(UIEntry entry)
        {
            entry.OpenRequested = false;
            if (!entry.IsOpen || entry.View == null)
                return;

            entry.View.UnbindViewModel();
            entry.ViewModel.OnClose();
            entry.View.gameObject.SetActive(false);
            entry.IsOpen = false;
        }

        private static void DestroyEntry(UIEntry entry)
        {
            entry.IsDestroyed = true;
            CloseEntry(entry);
            entry.ViewModel?.Dispose();

            if (entry.View != null)
                UnityEngine.Object.Destroy(entry.View.gameObject);

            entry.Lease?.Dispose();
            entry.View = null;
            entry.ViewModel = null;
            entry.Lease = null;
        }

        private static void RemoveFromStack(Stack<Type> stack, Type type)
        {
            if (!stack.Contains(type))
                return;

            var values = stack.ToArray();
            stack.Clear();

            for (var i = values.Length - 1; i >= 0; i--)
            {
                if (values[i] != type)
                    stack.Push(values[i]);
            }
        }

        private sealed class UIEntry
        {
            public readonly Type ViewType;
            public readonly string Address;
            public readonly EUILayer Layer;
            public readonly EUISubLayer SubLayer;
            public readonly Func<UIViewModelBase> ViewModelFactory;

            public UIViewBase View;
            public UIViewModelBase ViewModel;
            public IAssetLease<GameObject> Lease;
            public UniTask<UIViewBase> LoadTask;
            public bool IsLoading;
            public bool IsOpen;
            public bool OpenRequested;
            public bool IsDestroyed;

            public UIEntry(
                Type viewType,
                string address,
                EUILayer layer,
                EUISubLayer subLayer,
                Func<UIViewModelBase> viewModelFactory)
            {
                ViewType = viewType;
                Address = address;
                Layer = layer;
                SubLayer = subLayer;
                ViewModelFactory = viewModelFactory;
            }
        }
    }
}
