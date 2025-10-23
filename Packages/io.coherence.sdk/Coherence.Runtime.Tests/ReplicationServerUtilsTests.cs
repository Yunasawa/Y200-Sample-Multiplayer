// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System.Threading.Tasks;
    using Cloud;
    using Coherence.Editor;
    using Coherence.Tests;
    using Editor.ReplicationServer;
    using NUnit.Framework;
    using Toolkit.ReplicationServer;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.TestTools;

    // Excluding OSX because it looks like the RS application is not signed so it fails to run.
    [TestFixture, UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor)]
    public class ReplicationServerUtilsTests : CoherenceTest
    {
        private const int PingTimeOutSecs = 5;
        private static ReplicationServerConfig replicationServerConfig = EditorLauncher.CreateLocalRoomsConfig();
        private static IReplicationServer replicationServer;

        private static TestCaseData[] testCases =
        {
            new TestCaseData("localhost", replicationServerConfig.APIPort, true)
                .SetName("Valid Port and Host")
                .SetDescription($"localhost:{replicationServerConfig.APIPort}"),
            new TestCaseData("localhost", 9999, false)
                .SetName("Invalid Port")
                .SetDescription("localhost:9999"),
            new TestCaseData("192.168.0.0", replicationServerConfig.APIPort, false)
                .SetName("Invalid Host")
                .SetDescription($"foobar:{replicationServerConfig.APIPort}"),
            new TestCaseData("192.168.0.0", 9999, false)
                .SetName("Invalid Port and Host")
                .SetDescription("192.168.0.0:9999"),
        };

        private bool rsIsReady = false;
        private bool rsShutDown = false;

        public override void OneTimeSetUp()
        {
            rsIsReady = false;

            base.OneTimeSetUp();

            // Make sure the Gamhered.schema is there.  There was an
            // issue with it missing in CI.
            if (!BakeUtil.GatheredSchemaExists)
            {
                BakeUtil.GenerateSchema(out var _, out var _);
            }

#if UNITY_2022_1_OR_NEWER
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
#endif
            ReplicationServerUtils.Timeout = 1;
            replicationServerConfig.LogTargets = new LogTargetConfig[]
            {
                new LogTargetConfig()
                {
                    Target = LogTarget.Console,
                    Format = LogFormat.Plain,
                    LogLevel = Log.LogLevel.Info,
                },
            };
            replicationServer = Launcher.Create(replicationServerConfig);
            replicationServer.OnLog += (log) =>
            {
                Debug.Log($"RS: {log}");

                if (log.Contains("starting") && log.Contains("logger=apiServer"))
                {
                    rsIsReady = true; // catch when the RS starts up.
                }
            };
            replicationServer.OnExit += (code) =>
            {
                if (!rsShutDown && code != 0)
                {
                    Debug.LogError($"RS shutdown unexpectedly. Code: {code}");
                }
            };

            var started = replicationServer.Start();
            Assert.True(started, "expect the RS exe to start");
        }

        public override void OneTimeTearDown()
        {
            rsShutDown = true;
            bool success = replicationServer.Stop();
            if (!success)
            {
                Debug.LogError("failed to shut down RS");
            }

            base.OneTimeTearDown();
        }

        [Test, TestCaseSource(nameof(testCases))]
        public async Task PingHttpServer_Ends(string host, int port, bool shouldSucceed)
        {
            var endTime = EditorApplication.timeSinceStartup + PingTimeOutSecs;
            var pinged = false;
            var done = false;
            var success = false;

            do
            {
                if (rsIsReady && !pinged)
                {
                    ReplicationServerUtils.PingHttpServer(host, port, ok =>
                    {
                        done = true;
                        success = ok;
                    });
                    pinged = true;
                }

                await Task.Yield();
            } while (!done && EditorApplication.timeSinceStartup < endTime);

            Assert.IsTrue(rsIsReady, "Test if the RS was ready.");
            Assert.IsTrue(success == shouldSucceed, "Did the test succeed as intended?");
        }

        [Test, TestCaseSource(nameof(testCases))]
        public async Task PingHttpServerAsync_Ends(string host, int port, bool shouldSucceed)
        {
            var endTime = EditorApplication.timeSinceStartup + PingTimeOutSecs;
            var done = false;
            var success = false;

            do
            {
                if (rsIsReady)
                {
                    success = await ReplicationServerUtils.PingHttpServerAsync(host, port);
                    done = true;
                }
                else
                {
                    await Task.Yield();
                }
            } while (!done && EditorApplication.timeSinceStartup < endTime);

            Assert.IsTrue(rsIsReady, "Test if the RS was ready.");
            Assert.IsTrue(success == shouldSucceed, "Did the test succeed as intended?");
        }
    }
}
