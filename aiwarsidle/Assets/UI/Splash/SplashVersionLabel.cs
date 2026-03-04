using System;
using UnityEngine;
using UnityEngine.UI;

namespace AIWarsIdle.UI.Splash
{
    public sealed class SplashVersionLabel : MonoBehaviour
    {
        [Tooltip("Optional. If null, will try to use Text/TMP_Text on this GameObject.")]
        [SerializeField] private Text _uiText;

        [Tooltip("Format: versionName (bundleNumber)")]
        [SerializeField] private string _format = "{0} ({1})";

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            var versionName = string.IsNullOrWhiteSpace(Application.version) ? "0.0.0" : Application.version;
            var bundleNumber = GetStoreBundleNumber();

            var value = string.Format(_format, versionName, bundleNumber);
            SetLabel(value);
        }

        private void SetLabel(string value)
        {
            if (_uiText == null)
            {
                _uiText = GetComponent<Text>();
            }

            if (_uiText != null)
            {
                _uiText.text = value;
                return;
            }

            // Optional TMP support without hard dependency on TextMeshPro.
            var tmpType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            if (tmpType == null) return;

            var tmpComponent = GetComponent(tmpType);
            if (tmpComponent == null) return;

            var prop = tmpType.GetProperty("text");
            prop?.SetValue(tmpComponent, value);
        }

        private static string GetStoreBundleNumber()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return "0";

                using var pm = activity.Call<AndroidJavaObject>("getPackageManager");
                var packageName = activity.Call<string>("getPackageName");
                if (pm == null || string.IsNullOrWhiteSpace(packageName)) return "0";

                using var packageInfo = pm.Call<AndroidJavaObject>("getPackageInfo", packageName, 0);
                if (packageInfo == null) return "0";

                // versionCode (int) is deprecated on newer Android, longVersionCode exists.
                try
                {
                    var longVersionCode = packageInfo.Get<long>("longVersionCode");
                    if (longVersionCode > 0) return longVersionCode.ToString();
                }
                catch
                {
                    // ignore; fall back to versionCode
                }

                try
                {
                    var versionCode = packageInfo.Get<int>("versionCode");
                    return versionCode.ToString();
                }
                catch
                {
                    return "0";
                }
            }
            catch
            {
                return "0";
            }
#elif UNITY_EDITOR
            try
            {
                // Best-effort editor preview.
                if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
                {
                    return UnityEditor.PlayerSettings.Android.bundleVersionCode.ToString();
                }

                if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS)
                {
                    return UnityEditor.PlayerSettings.iOS.buildNumber;
                }
            }
            catch
            {
                // ignore
            }

            return "0";
#else
            return "0";
#endif
        }
    }
}

