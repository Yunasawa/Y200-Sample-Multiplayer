// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using UnityEngine;
    using System.Collections;
    using Logger = Log.Logger;

    internal class SimulatorFramerate
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            SimulatorFramerateLimiter.Init();
        }

        public class SimulatorFramerateLimiter : MonoBehaviour
        {
            private Logger logger;
            private Coroutine loop;
            private bool changed;
            private RuntimeSettings runtimeSettings;

            public static void Init()
            {
                if (!SimulatorUtility.IsSimulator)
                {
                    return;
                }

                var go = new GameObject();
                _ = go.AddComponent<SimulatorFramerateLimiter>();
                go.hideFlags = HideFlags.HideInHierarchy;
                DontDestroyOnLoad(go);
            }

            private void Awake()
            {
                runtimeSettings = RuntimeSettings.Instance;
                logger = Log.Log.GetLogger<SimulatorFramerateLimiter>();
                logger.Context = this;

                if (runtimeSettings.SimulatorLockTargetFramerate)
                {
                    var targetFrameRate = runtimeSettings.SimulatorTargetFramerate;
                    logger.Info($"Forcing simulator target frame rate to {targetFrameRate}. This behaviour can be configured via RuntimeSettings.");
                    Application.targetFrameRate = targetFrameRate;
                }
            }

            private void OnEnable()
            {
                loop = StartCoroutine(ForceTargetFrameRateLoop());
            }

            private void OnDisable()
            {
                if (loop != null)
                {
                    StopCoroutine(loop);
                    loop = null;
                }
            }

            private IEnumerator ForceTargetFrameRateLoop()
            {
                while (true)
                {
                    yield return new WaitForEndOfFrame();
                    ForceTargetFrameRate();
                }
            }

            private void ForceTargetFrameRate()
            {
                // We might be late into the game quitting, so check if the RuntimeSettings instance is still available.
                if (!runtimeSettings)
                {
                    return;
                }

                if (!runtimeSettings.SimulatorLockTargetFramerate)
                {
                    return;
                }

                var targetFrameRate = runtimeSettings.SimulatorTargetFramerate;
                if (Application.targetFrameRate != targetFrameRate)
                {
                    if (!changed)
                    {
                        changed = true;
                        logger.Warning(Log.Warning.SimulatorFrameRateChanged,
                            $"Detected target frame rate {Application.targetFrameRate}.\n" +
                            $"Forcing framerate to {targetFrameRate}.\n" +
                            "This behavior can be configured via RuntimeSettings.");
                    }

                    Application.targetFrameRate = targetFrameRate;
                }
            }
        }
    }
}
