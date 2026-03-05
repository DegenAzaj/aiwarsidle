using System;
using UnityEngine;

namespace AIWarsIdle.UI.Splash
{
    public static class AppBuildNumber
    {
        public static long GetCurrent()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return 0;

                using var pm = activity.Call<AndroidJavaObject>("getPackageManager");
                var packageName = activity.Call<string>("getPackageName");
                if (pm == null || string.IsNullOrWhiteSpace(packageName)) return 0;

                using var packageInfo = pm.Call<AndroidJavaObject>("getPackageInfo", packageName, 0);
                if (packageInfo == null) return 0;

                try
                {
                    var longVersionCode = packageInfo.Get<long>("longVersionCode");
                    if (longVersionCode > 0) return longVersionCode;
                }
                catch
                {
                    // ignore; fall back
                }

                try
                {
                    var versionCode = packageInfo.Get<int>("versionCode");
                    return versionCode;
                }
                catch
                {
                    return 0;
                }
            }
            catch
            {
                return 0;
            }
#elif UNITY_EDITOR
            try
            {
                if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
                {
                    return UnityEditor.PlayerSettings.Android.bundleVersionCode;
                }

                if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS)
                {
                    if (long.TryParse(UnityEditor.PlayerSettings.iOS.buildNumber, out var n)) return n;
                }
            }
            catch
            {
                // ignore
            }

            return 0;
#else
            return 0;
#endif
        }
    }
}

