using BorFramework;
using GameLogic.GameFlow;
using GameLogic.Navigation;
using GameLogic.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameLogic
{
    public sealed class GameBoot : SingletonMono<GameBoot>
    {
        private const string DefaultInputAssetName = "Input/DefultInputSystem";

        [SerializeField]
        private InputActionAsset _inputActions;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private UnitSpawnPanel _unitSpawnPanel;
        private UnitCombatLog _unitCombatLog;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateAutomatically()
        {
            if (Ins != null)
                return;

            var go = new GameObject("GameBoot");
            DontDestroyOnLoad(go);
            go.AddComponent<GameBoot>();
        }

        protected override void Awake()
        {
            base.Awake();

            if (Ins != this)
                return;

            DontDestroyOnLoad(gameObject);
            Init();
        }

        private void Start()
        {
            if (Ins != this || !GameHub.Ins.IsRunning)
                return;

            var eventModule = GameHub.Ins.GetModule<IEventModule>();
            eventModule?.Publish(new FrameworkReadyEvent());
        }

        private void Init()
        {
            if (GameHub.Ins.IsInitialized)
                return;

            GameHub.Ins.Init();

            GameHub.Ins.RegisterModule<ILogModule>(new LogModule());

            var monoModule = new MonoModule();
            var inputModule = new InputModule(ResolveInputActions());
            var entityModule = new EntityModule(monoModule);
            var resourceModule = new ResourceModule();
            var prefabPoolModule = new PrefabPoolModule(resourceModule);
            var sceneModule = new SceneModule(resourceModule);
            var uiModule = new UIModule(resourceModule);
            var gameSystemModule = new GameSystemModule();
            var eventModule = new EventModule();

            GameHub.Ins.RegisterModule<IMonoModule>(monoModule);
            GameHub.Ins.RegisterModule<IEventModule>(eventModule);
            GameHub.Ins.RegisterModule<IInputModule>(inputModule);
            GameHub.Ins.RegisterModule<IEntityModule>(entityModule);
            GameHub.Ins.RegisterModule<IResourceModule>(resourceModule);
            GameHub.Ins.RegisterModule<IPrefabPoolModule>(prefabPoolModule);
            GameHub.Ins.RegisterModule<ISceneModule>(sceneModule);
            GameHub.Ins.RegisterModule<IUIModule>(uiModule);
            GameHub.Ins.RegisterModule<IGameSystemModule>(gameSystemModule);

            INavigationSystem navigationSystem = new NavigationSystem();
            var unitSystem = new UnitSystem(
                resourceModule,
                prefabPoolModule,
                entityModule,
                inputModule,
                eventModule,
                monoModule,
                navigationSystem);
            gameSystemModule.AddSystem<INavigationSystem>(navigationSystem);
            gameSystemModule.AddSystem<IUnitSystem>(unitSystem);
            gameSystemModule.AddSystem(new GameFlowSystem(sceneModule, uiModule, monoModule, unitSystem));

            GameHub.Ins.InitModules();
            GameHub.Ins.StartModules();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _unitCombatLog = new UnitCombatLog(eventModule, unitSystem);
            _unitSpawnPanel = new UnitSpawnPanel(resourceModule, unitSystem);
            _unitSpawnPanel.Start();
#endif

            var logger = GameHub.Ins.GetModule<ILogModule>();
            logger?.Log("GameHub初始化完成");
        }

        private InputActionAsset ResolveInputActions()
        {
            if (_inputActions != null)
                return _inputActions;

            _inputActions = Resources.Load<InputActionAsset>(DefaultInputAssetName);
            return _inputActions;
        }

        private void FixedUpdate()
        {
            if (!GameHub.Ins.IsRunning)
                return;

            GameHub.Ins.GetModule<IMonoModule>()?.DoFixedUpdate(Time.fixedDeltaTime);
        }

        private void Update()
        {
            if (!GameHub.Ins.IsRunning)
                return;

            GameHub.Ins.GetModule<IMonoModule>()?.DoUpdate(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (!GameHub.Ins.IsRunning)
                return;

            GameHub.Ins.GetModule<IMonoModule>()?.DoLateUpdate(Time.deltaTime);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            _unitSpawnPanel?.Draw();
        }
#endif

        private void OnDestroy()
        {
            if (Ins != this)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _unitCombatLog?.Dispose();
            _unitCombatLog = null;
            _unitSpawnPanel?.Dispose();
            _unitSpawnPanel = null;
#endif

            GameHub.Ins.StopModules();
            GameHub.Ins.DisposeModules();
        }
    }
}
