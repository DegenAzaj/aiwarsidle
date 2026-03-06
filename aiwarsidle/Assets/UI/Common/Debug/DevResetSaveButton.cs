using System;
using System.IO;
using AIWarsIdle.Bootstrap;
using AIWarsIdle.Persistence.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace AIWarsIdle.UI.DebugTools
{
    public sealed class DevResetSaveButton : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Optional. If null, will try to find one in the scene at runtime.")]
        [SerializeField] private GameBootstrapper _bootstrapper;

        [Header("Visibility")]
        [Tooltip("Shows only in Editor and Development builds.")]
        [SerializeField] private bool _enabledInDev = true;

        [Header("Behavior")]
        [Tooltip("If enabled, reloads the current scene after deleting save files (recommended).")]
        [SerializeField] private bool _reloadSceneAfterReset = true;

        [Tooltip("If enabled, also clears all PlayerPrefs (UI mode etc.).")]
        [SerializeField] private bool _clearPlayerPrefs = false;

        [Header("Layout")]
        [SerializeField] private Vector2 _size = new(240f, 44f);
        [SerializeField] private Vector2 _anchoredPosition = new(0f, -60f);

        private GameObject _buttonGo;

        private void Start()
        {
            if (!ShouldShow()) return;

            if (_bootstrapper == null)
            {
                _bootstrapper = FindBootstrapper();
            }

            if (_bootstrapper == null)
            {
                Debug.LogWarning($"{nameof(DevResetSaveButton)}: No {nameof(GameBootstrapper)} found in scene.");
                return;
            }

            EnsureEventSystem();

            var canvas = FindObjectOfType<Canvas>(includeInactive: true);
            if (canvas == null)
            {
                Debug.LogWarning($"{nameof(DevResetSaveButton)}: No Canvas found in scene.");
                return;
            }

            EnsureCanvasRaycaster(canvas);
            _buttonGo = CreateButtonUnder(canvas.transform);
        }

        private bool ShouldShow()
        {
            if (!_enabledInDev) return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return Debug.isDebugBuild;
#endif
        }

        private void EnsureEventSystem()
        {
            var eventSystem = FindObjectOfType<EventSystem>(includeInactive: true);
            if (eventSystem != null)
            {
                EnsureCompatibleInputModule(eventSystem);
                EnsureUiActions(eventSystem);
                return;
            }

            var go = new GameObject("EventSystem");
            eventSystem = go.AddComponent<EventSystem>();
            AddAppropriateInputModule(go);
            EnsureCompatibleInputModule(eventSystem);
            EnsureUiActions(eventSystem);
        }

        private static void EnsureCompatibleInputModule(EventSystem eventSystem)
        {
            if (eventSystem == null) return;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                if (Application.isPlaying) Destroy(legacy);
                else DestroyImmediate(legacy);
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
#else
            if (eventSystem.GetComponent<StandaloneInputModule>() == null && eventSystem.GetComponent<BaseInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }

        private static void EnsureUiActions(EventSystem eventSystem)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (eventSystem == null) return;
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null) return;
            if (module.actionsAsset != null) return;
            module.AssignDefaultActions();
#endif
        }

        private static void AddAppropriateInputModule(GameObject eventSystemGo)
        {
            if (eventSystemGo == null) return;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemGo.AddComponent<StandaloneInputModule>();
#endif
        }

        private static void EnsureCanvasRaycaster(Canvas canvas)
        {
            if (canvas == null) return;
            if (canvas.GetComponent<GraphicRaycaster>() != null) return;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        private GameObject CreateButtonUnder(Transform parent)
        {
            var go = new GameObject("DEV_ResetSaveButton");
            go.transform.SetParent(parent, worldPositionStays: false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = _size;
            rect.anchoredPosition = _anchoredPosition;

            var image = go.AddComponent<Image>();
            image.sprite = WhiteSpriteCache.Sprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(ResetAndReload);

            var colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.12f, 0.12f, 0.85f);
            colors.highlightedColor = new Color(0.18f, 0.18f, 0.18f, 0.90f);
            colors.pressedColor = new Color(0.10f, 0.10f, 0.10f, 0.95f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.10f, 0.10f, 0.10f, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, worldPositionStays: false);

            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;

            var text = labelGo.AddComponent<Text>();
            text.text = "DEV: Reset Save";
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 16;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;

            return go;
        }

        private void ResetAndReload()
        {
            if (!ShouldShow()) return;

            try
            {
                ResetSaveFiles();

                if (_clearPlayerPrefs)
                {
                    PlayerPrefs.DeleteAll();
                    PlayerPrefs.Save();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{nameof(DevResetSaveButton)}: Reset failed: {ex.GetType().Name}: {ex.Message}");
            }

            if (_reloadSceneAfterReset)
            {
                ReloadScene();
            }
        }

        private void ResetSaveFiles()
        {
            if (_bootstrapper == null)
            {
                Debug.LogWarning($"{nameof(DevResetSaveButton)}: Missing {nameof(GameBootstrapper)}.");
                return;
            }

            _bootstrapper.Loop?.Dispose();

            var dir = Path.Combine(Application.persistentDataPath, _bootstrapper.SaveSubdirectory);
            var paths = new SaveFilePaths(dir, _bootstrapper.SaveFileNameWithoutExtension);

            TryDelete(paths.SavePath);
            TryDelete(paths.BackupPath);
            TryDelete(paths.TempPath);

            try
            {
                if (Directory.Exists(paths.DirectoryPath)
                    && Directory.GetFiles(paths.DirectoryPath).Length == 0
                    && Directory.GetDirectories(paths.DirectoryPath).Length == 0)
                {
                    Directory.Delete(paths.DirectoryPath);
                }
            }
            catch
            {
                // ignore best-effort cleanup
            }

            Debug.Log($"[Dev] Save reset: deleted {paths.SavePath}");
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // ignore best-effort delete failures
            }
        }

        private static void ReloadScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(scene.name);
            }
        }

        private static GameBootstrapper FindBootstrapper()
        {
#if UNITY_2023_1_OR_NEWER
            var found = FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Exclude);
            if (found != null) return found;
            return FindAnyObjectByType<GameBootstrapper>(FindObjectsInactive.Exclude);
#else
            return FindObjectOfType<GameBootstrapper>();
#endif
        }

        private static class WhiteSpriteCache
        {
            private static Sprite _sprite;
            public static Sprite Sprite => _sprite != null ? _sprite : (_sprite = CreateWhiteSprite());

            private static Sprite CreateWhiteSprite()
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                tex.name = "UI_White_1x1_Runtime";

                var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit: 100f);
                sprite.name = "UI_WhiteSprite_Runtime";
                return sprite;
            }
        }
    }
}
