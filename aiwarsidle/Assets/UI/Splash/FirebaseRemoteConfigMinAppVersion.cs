using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace AIWarsIdle.UI.Splash
{
    public static class FirebaseRemoteConfigMinAppVersion
    {
        public static IEnumerator FetchAndGetLong(string key, float timeoutSeconds, Action<long?> onResult)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                onResult?.Invoke(null);
                yield break;
            }

            var deadline = timeoutSeconds > 0f ? Time.realtimeSinceStartup + timeoutSeconds : float.PositiveInfinity;

            if (!TryCheckAndFixFirebaseDependencies(out var depTask, out var depError))
            {
                if (!string.IsNullOrWhiteSpace(depError)) Debug.LogWarning(depError);
                onResult?.Invoke(null);
                yield break;
            }

            yield return WaitForTask(depTask, deadline);
            if (depTask.IsFaulted || depTask.IsCanceled)
            {
                onResult?.Invoke(null);
                yield break;
            }

            if (!IsDependencyStatusAvailable(depTask))
            {
                onResult?.Invoke(null);
                yield break;
            }

            if (!TryFetchAndActivateRemoteConfig(out var fetchTask, out var fetchError))
            {
                if (!string.IsNullOrWhiteSpace(fetchError)) Debug.LogWarning(fetchError);
                onResult?.Invoke(null);
                yield break;
            }

            yield return WaitForTask(fetchTask, deadline);
            if (fetchTask.IsFaulted || fetchTask.IsCanceled)
            {
                onResult?.Invoke(null);
                yield break;
            }

            if (!TryGetRemoteConfigLong(key, out var value, out var getError))
            {
                if (!string.IsNullOrWhiteSpace(getError)) Debug.LogWarning(getError);
                onResult?.Invoke(null);
                yield break;
            }

            onResult?.Invoke(value);
        }

        private static IEnumerator WaitForTask(Task task, float deadline)
        {
            if (task == null) yield break;

            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup >= deadline) yield break;
                yield return null;
            }
        }

        private static bool TryCheckAndFixFirebaseDependencies(out Task task, out string error)
        {
            task = null;
            error = null;

            try
            {
                var appType = FirebaseTypeResolver.GetTypeOrNull("Firebase.FirebaseApp", "Firebase.App");
                if (appType == null)
                {
                    error = "Firebase.App not found (Firebase not installed or not loaded).";
                    return false;
                }

                var method = appType.GetMethod("CheckAndFixDependenciesAsync", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    error = "FirebaseApp.CheckAndFixDependenciesAsync not found.";
                    return false;
                }

                var result = method.Invoke(null, null);
                task = result as Task;
                if (task == null)
                {
                    error = "FirebaseApp.CheckAndFixDependenciesAsync did not return a Task.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Firebase dependency check failed: {ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        private static bool IsDependencyStatusAvailable(Task dependencyTask)
        {
            if (dependencyTask == null) return false;

            try
            {
                var resultProp = dependencyTask.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                var result = resultProp?.GetValue(dependencyTask);
                if (result == null) return false;
                return string.Equals(result.ToString(), "Available", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryFetchAndActivateRemoteConfig(out Task task, out string error)
        {
            task = null;
            error = null;

            try
            {
                var rcType = FirebaseTypeResolver.GetTypeOrNull("Firebase.RemoteConfig.FirebaseRemoteConfig", "Firebase.RemoteConfig");
                if (rcType == null)
                {
                    error = "Firebase.RemoteConfig not found (Remote Config not installed or not loaded).";
                    return false;
                }

                var defaultInstanceProp = rcType.GetProperty("DefaultInstance", BindingFlags.Public | BindingFlags.Static);
                var instance = defaultInstanceProp?.GetValue(null);
                if (instance == null)
                {
                    error = "FirebaseRemoteConfig.DefaultInstance is null.";
                    return false;
                }

                TryConfigureRemoteConfigForDev(instance);

                var fetchAndActivate = rcType.GetMethod("FetchAndActivateAsync", BindingFlags.Public | BindingFlags.Instance);
                if (fetchAndActivate == null)
                {
                    error = "FirebaseRemoteConfig.FetchAndActivateAsync not found.";
                    return false;
                }

                var result = fetchAndActivate.Invoke(instance, null);
                task = result as Task;
                if (task == null)
                {
                    error = "FirebaseRemoteConfig.FetchAndActivateAsync did not return a Task.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Remote Config fetch failed: {ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        private static void TryConfigureRemoteConfigForDev(object remoteConfigInstance)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (remoteConfigInstance == null) return;

            try
            {
                var rcType = remoteConfigInstance.GetType();
                var setSettings = rcType.GetMethod("SetConfigSettingsAsync", BindingFlags.Public | BindingFlags.Instance);
                if (setSettings == null) return;

                var settingsType = FirebaseTypeResolver.GetTypeOrNull("Firebase.RemoteConfig.ConfigSettings", "Firebase.RemoteConfig");
                if (settingsType == null) return;

                var settings = Activator.CreateInstance(settingsType);
                if (settings == null) return;

                var minFetchProp = settingsType.GetProperty("MinimumFetchIntervalInSeconds", BindingFlags.Public | BindingFlags.Instance);
                minFetchProp?.SetValue(settings, 0L);

                // Some versions also expose FetchTimeoutInSeconds; best-effort.
                var fetchTimeoutProp = settingsType.GetProperty("FetchTimeoutInSeconds", BindingFlags.Public | BindingFlags.Instance);
                fetchTimeoutProp?.SetValue(settings, 10L);

                setSettings.Invoke(remoteConfigInstance, new[] { settings });
            }
            catch
            {
                // ignore best-effort config
            }
#endif
        }

        private static bool TryGetRemoteConfigLong(string key, out long value, out string error)
        {
            value = 0;
            error = null;

            try
            {
                var rcType = FirebaseTypeResolver.GetTypeOrNull("Firebase.RemoteConfig.FirebaseRemoteConfig", "Firebase.RemoteConfig");
                if (rcType == null)
                {
                    error = "Firebase.RemoteConfig not found.";
                    return false;
                }

                var defaultInstanceProp = rcType.GetProperty("DefaultInstance", BindingFlags.Public | BindingFlags.Static);
                var instance = defaultInstanceProp?.GetValue(null);
                if (instance == null)
                {
                    error = "FirebaseRemoteConfig.DefaultInstance is null.";
                    return false;
                }

                var getValue = rcType.GetMethod("GetValue", BindingFlags.Public | BindingFlags.Instance, binder: null, types: new[] { typeof(string) }, modifiers: null);
                if (getValue == null)
                {
                    error = "FirebaseRemoteConfig.GetValue(string) not found.";
                    return false;
                }

                var configValue = getValue.Invoke(instance, new object[] { key });
                if (configValue == null)
                {
                    error = $"Remote Config key '{key}' returned null value.";
                    return false;
                }

                // Prefer LongValue if present; otherwise parse StringValue/DoubleValue.
                var longProp = configValue.GetType().GetProperty("LongValue", BindingFlags.Public | BindingFlags.Instance);
                if (longProp != null)
                {
                    var raw = longProp.GetValue(configValue);
                    if (raw is long l)
                    {
                        value = l;
                        return true;
                    }

                    if (raw != null && long.TryParse(raw.ToString(), out var parsedLong))
                    {
                        value = parsedLong;
                        return true;
                    }
                }

                var stringProp = configValue.GetType().GetProperty("StringValue", BindingFlags.Public | BindingFlags.Instance);
                if (stringProp != null)
                {
                    var s = stringProp.GetValue(configValue)?.ToString();
                    if (!string.IsNullOrWhiteSpace(s) && long.TryParse(s, out var parsed))
                    {
                        value = parsed;
                        return true;
                    }
                }

                var doubleProp = configValue.GetType().GetProperty("DoubleValue", BindingFlags.Public | BindingFlags.Instance);
                if (doubleProp != null)
                {
                    var raw = doubleProp.GetValue(configValue);
                    if (raw is double d)
                    {
                        value = (long)Math.Floor(d);
                        return true;
                    }

                    if (raw != null && double.TryParse(raw.ToString(), out var parsedDouble))
                    {
                        value = (long)Math.Floor(parsedDouble);
                        return true;
                    }
                }

                error = $"Remote Config key '{key}' is not a number.";
                return false;
            }
            catch (Exception ex)
            {
                error = $"Remote Config get failed: {ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }
    }
}
