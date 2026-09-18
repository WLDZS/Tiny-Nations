using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

namespace BorFramework.Editor
{
    public static class BootToolbar
    {
        private const string BootScenePath = "Assets/GameAsset/Boot/Boot.unity";
        private const string BootStartKey = "BorFramework.BootStart";

        [MainToolbarElement("Play Mode Controls/Boot",
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = -1,
            ussName = "PlayMode")]
        public static IEnumerable<MainToolbarElement> CreateBootButton()
        {
            var content = new MainToolbarContent("Boot", "从Boot场景启动");
            yield return new MainToolbarButton(content, PlayFromBoot);
        }

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void PlayFromBoot()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var bootScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
            if (bootScene == null)
            {
                Debug.LogError($"找不到Boot场景：{BootScenePath}");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            SessionState.SetBool(BootStartKey, true);
            EditorSceneManager.playModeStartScene = bootScene;
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(BootStartKey, false))
                return;

            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(BootStartKey, false);
        }
    }
}
