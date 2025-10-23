// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
#define UNITY
#endif

namespace Coherence.Prefs
{
#if UNITY
    using System;
    using System.Globalization;
    using System.Threading;
    using Runtime;
    using Runtime.Utils;
    using UnityEngine;
#endif

    /// <summary>
    /// <para>
    /// Provides an identifier that is unique for each instance of the application currently running on this device.
    /// </para>
    /// <para>
    /// This can be used to generate a unique keys for storing data in <see cref="Coherence.Prefs.Prefs"/>
    /// that should be stored separately for each instance of the application running concurrently on the same device.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the editor and in standalone development builds, the first process that calls <see cref="Get"/>
    /// at runtime will be assigned the process ID of 1. Subsequent concurrent processes that call <see cref="Get"/>
    /// will be assigned incrementing IDs, starting from 2.
    /// </para>
    /// <para>
    /// Process IDs are project specific and stored in Player Prefs.
    /// Claimed process IDs are released when the application quits.
    /// Claimed process IDs also become available automatically if the process does not re-establish their claim for 10 seconds,
    /// so that process IDs also get released in case the process crashes or closes unexpectedly.
    /// If the application is paused, the process ID is claimed for 5 minutes, to prevent other
    /// processes from claiming the same ID for a reasonable amount of time while the application remains paused,
    /// while still making sure the ID will eventually get released if the application should crash while paused.
    /// </para>
    /// <para>
    /// On console and mobile platforms <see cref="Get"/> always returns <see cref="FirstProcessId"/>.
    /// </para>
    /// <para>
    /// In non-development standalone builds, the process IDs are only claimed when the application starts,
    /// and released when the application quits, but not periodically while the application is running.
    /// </para>
    /// </remarks>
    internal sealed class ProcessId
#if UNITY
        : MonoBehaviour
#endif
    {
        internal const int FirstProcessId = 1;

#if UNITY
#if DEBUG
        private const double ClaimForSecondsInitially = 10;
        private const double ClaimEverySeconds = 1;
        private const double ClaimForSecondsPeriodically = 10;
        private const double ClaimForSecondsOnPause = 60 * 5; // 5 minutes
#else
        private const double ClaimForSecondsInitially = 60 * 60 * 24; // 24 hours
        private const double ClaimEverySeconds = 60 * 60 * 23; // 23 hours
        private const double ClaimForSecondsPeriodically = 60 * 60 * 24; // 24 hours
        private const double ClaimForSecondsOnPause = 60d * 5d; // 5 minutes
#endif

        private const string RoundtripFormat = "o"; // Round-trip format (ISO 8601)
        private const int None = 0;

        private static readonly UnityPrefs UnityPrefs = new();

        private static int processId;

        private string prefsKey;
        private double nextClaimTime = ClaimEverySeconds;
#endif

        /// <returns>
        /// <see cref="FirstProcessId"/> if this is the only process currently running, or the first process for which
        /// <see cref="Get"/> was executed; otherwise, an incrementing integer that is unique for the current process.
        /// </returns>
        public static int Get()
        {
#if UNITY
            if (processId is not None)
            {
                return processId;
            }

            if (AlwaysUseSameId)
            {
                return processId = FirstProcessId;
            }

            try
            {
                // Use a mutex to ensure that only one process on this machine can claim a process ID at a time.
                using var mutex = new Mutex(false, "Coherence.ProcessId. " + RuntimeSettings.Instance.ProjectID);
                if (!mutex.WaitOne(millisecondsTimeout: 1000))
                {
                    // If current instance did not receive a signal just use FirstProcessId
                    return processId = FirstProcessId;
                }

                // Double-check that we still don't have a process id. Another thread could have changed it while we were waiting for the mutex.
                if (processId is None)
                {
                    processId = GetFirstUnclaimedId(UnityPrefs, RuntimeSettings.Instance.ProjectID);
                    var prefsKey = GetPrefsKey(RuntimeSettings.Instance.ProjectID, processId);
                    ClaimUntil(UnityPrefs, prefsKey, DateTime.UtcNow + TimeSpan.FromSeconds(ClaimForSecondsInitially));
                    UnityPrefs.Save();
                }

                return processId;
            }
            // If the platform does not support Mutex just use FirstProcessId
            catch (NotSupportedException)
            {
                return processId = FirstProcessId;
            }
#else
            return FirstProcessId;
#endif
        }

#if UNITY
        /// <summary>
        /// Gets a value indicating whether the concurrent process ID feature is enabled on this platform in the current context.
        /// When the value of this property is <see langword="false"/>, <see cref="Get"/> will always return <see cref="FirstProcessId"/>.
        /// </summary>
        /// <remarks>
        /// This feature is enabled in the Unity Editor in Play Mode, as well as in standalone development builds.
        /// It is disabled in builds for mobile platforms and console platforms, as well as in the Unity Editor in Edit Mode.
        /// </remarks>
        private static bool AlwaysUseSameId =>
            !Application.isPlaying
            || Application.isConsolePlatform
            || Application.isMobilePlatform
            || Application.platform is RuntimePlatform.WebGLPlayer;

        internal static int GetFirstUnclaimedId(IPrefsImplementation prefs, string projectId)
        {
            var result = FirstProcessId;
            while (IsClaimed(prefs, projectId, result))
            {
                result++;
            }

            return result;
        }

        internal static void ClaimUntil(IPrefsImplementation prefs, string prefsKey, DateTime time) => prefs.SetString(prefsKey, time.ToString(RoundtripFormat));

        internal static bool IsClaimed(IPrefsImplementation prefs, string projectId, int id) => IsClaimed(prefs, GetPrefsKey(projectId, id));

        internal static bool IsClaimed(IPrefsImplementation prefs, string prefsKey)
            => GetClaimedUntilTime(prefs, prefsKey) is { } claimedUntil && (claimedUntil - DateTime.UtcNow) is { TotalSeconds : > 0d and <= ClaimForSecondsOnPause };

        internal static DateTime? GetClaimedUntilTime(IPrefsImplementation prefs, string projectId, int id) => GetClaimedUntilTime(prefs, GetPrefsKey(projectId, id));

        internal static DateTime? GetClaimedUntilTime(IPrefsImplementation prefs, string prefsKey)
            => prefs.GetString(prefsKey, null) is { } timeStampString
               && DateTime.TryParse(timeStampString, null, DateTimeStyles.RoundtripKind, out var timeStamp)
                ? timeStamp : null;

        internal static void Release(IPrefsImplementation prefs, string prefsKey)
        {
            prefs.DeleteKey(prefsKey);
            prefs.Save();
        }

        /// <summary>
        /// Gets a prefs key specific to the current project and process ID.
        /// </summary>
        internal static string GetPrefsKey(string projectId, int id) => PrefsKeys.ProcessId.Format(projectId, id);

        // This helps ensure that ConcurrentProcessId.Get gets executed on the main thread the first time.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void ClaimProcessId() => _ = Get();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (AlwaysUseSameId)
            {
                return;
            }

            // Create a single instance of ProcessId that upholds the claim for the process continuously.
            var instance = new GameObject("ConcurrentProcessIdClaimUpdater").AddComponent<ProcessId>();
            DontDestroyOnLoad(instance);
            instance.hideFlags = HideFlags.HideInHierarchy;
        }

        private void Start()
        {
            _ = Get();
            prefsKey = GetPrefsKey(RuntimeSettings.Instance.ProjectID, processId);
        }

        private void FixedUpdate()
        {
            if (Time.timeAsDouble <= nextClaimTime)
            {
                return;
            }

            ClaimUntil(UnityPrefs, prefsKey, DateTime.UtcNow + TimeSpan.FromSeconds(ClaimForSecondsPeriodically));
            #if DEBUG
            UnityPrefs.Save();
            #endif
            nextClaimTime = Time.timeAsDouble + ClaimEverySeconds;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            var claimForSeconds = pauseStatus ? ClaimForSecondsOnPause : ClaimForSecondsPeriodically;
            ClaimUntil(UnityPrefs, prefsKey, DateTime.UtcNow + TimeSpan.FromSeconds(claimForSeconds));
        }

        private void OnApplicationQuit()
        {
            Release(UnityPrefs, prefsKey);
            processId = None;
        }
#endif
    }
}
