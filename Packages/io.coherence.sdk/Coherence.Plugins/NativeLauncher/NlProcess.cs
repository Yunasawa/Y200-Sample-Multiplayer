// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Plugins.NativeLauncher
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Log;
    using Utils;

    public class NlProcess : IDisposable
    {
        private IntPtr processHandle;
        private AsyncReader asyncReader;
        private CancellationTokenSource processMonitorCts;

        private readonly bool raiseOnExit;
        private bool raiseOnExitDone;
        private int exited;

        private Logger logger = Log.GetLogger<NlProcess>();

        private bool HasExited => exited != 0;

        private NlProcessStartupInfo processStartupInfo;

        /// <summary>
        /// Exit code of the process (only valid if HasExited is true).
        /// </summary>
        public int ExitCode { get; private set; }

        /// <summary>
        /// Called when new data is available on the output stream (not thread-safe).
        /// </summary>
        public StreamDataReceivedEventHandler OutputDataReceived;

        /// <summary>
        /// Called when the process has exited.
        /// </summary>
        public EventHandler Exited;

        /// <summary>
        /// ID of the process.
        /// </summary>
        public int Id { get; private set; } = -1;

        public NlProcess(NlProcessStartupInfo startupInfo)
        {
            processStartupInfo = startupInfo;

            using var arguments = new CStringArray(startupInfo.Arguments.ToArray());
            using var envVars = new CStringArray(startupInfo.EnvironmentVariablesToArray());

            var startupParams = new InteropAPI.NlStartupParams
            {
                executablePath = startupInfo.ExecutablePath,
                arguments = arguments.Ptr,
                argumentsCount = (uint)arguments.Length,
                envVars = envVars.Ptr,
                envVarsCount = (uint)envVars.Length,
                nonBlocking = 1,
            };

            processHandle = InteropAPI.Create(startupParams);

            logger.Debug("Created process", ("handle", processHandle), ("startupInfo", startupInfo));

            raiseOnExit = startupInfo.RaiseOnExit;
        }

        public void Dispose()
        {
            processMonitorCts?.Cancel();
            processMonitorCts = null;

            asyncReader?.StopReading();
            asyncReader = null;

            if (processHandle != IntPtr.Zero)
            {
                InteropAPI.Destroy(processHandle);
                processHandle = IntPtr.Zero;
            }
        }

        public bool Start()
        {
            var res = InteropAPI.Start(processHandle, out var pid);
            var started = res > 0;

            if (started)
            {
                Id = pid;
            }
            else
            {
                logger.Error(Error.NlFailedToStartProcess,
                    ("result", (InteropAPI.NlError)res),
                    ("error", InteropAPI.GetErrorString(res)),
                    ("executable", processStartupInfo.ExecutablePath),
                    ("found", File.Exists(processStartupInfo.ExecutablePath)));
            }

            if (started && raiseOnExit && processMonitorCts is null)
            {
                processMonitorCts = new CancellationTokenSource();
                _ = Task
                    .Run(MonitorProcess, processMonitorCts.Token)
                    .ContinueWith(task =>
                    {
                        if (task.IsCompletedSuccessfully)
                        {
                            RaiseOnExited();
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
            }

            return started;
        }

        public bool Terminate(int timeout)
        {
            var res = InteropAPI.StopAndWait(processHandle, timeout);
            if (res != (int)InteropAPI.NlError.TimedOut)
            {
                OnProcessExited(res);
                RaiseOnExited();

                return true;
            }

            // timeout is not a success
            return false;
        }

        public void BeginOutputReadLine()
        {
            if (asyncReader is null)
            {
                asyncReader = new AsyncReader(processHandle, OutputReceivedCallback);
                asyncReader.StartReading();
            }
        }

        /// <summary>
        /// Checks if a process with the same executable path is already running on the system.
        /// </summary>
        /// <returns>True if a process with the same executable is already running, false otherwise.</returns>
        public bool IsProcessAlreadyRunning()
        {
            try
            {
                if (string.IsNullOrEmpty(processStartupInfo.ExecutablePath))
                {
                    return false;
                }

                var executableName = Path.GetFileNameWithoutExtension(processStartupInfo.ExecutablePath);
                var processes = Process.GetProcessesByName(executableName);

                // Check if any process with the same executable name is running
                // and exclude the current process if it's already started
                foreach (var process in processes)
                {
                    try
                    {
                        // Skip our own process if it's already running
                        if (Id > 0 && process.Id == Id)
                        {
                            continue;
                        }

                        // Compare the full executable path if accessible
                        if (string.Equals(process.MainModule?.FileName, processStartupInfo.ExecutablePath,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Access to process information might be denied, continue checking other processes
                        logger.Debug("Could not access process information", ("processId", process.Id), ("error", ex.Message));
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.Debug("Error checking if process is already running", ("error", ex.Message));
                return false;
            }
        }

        private async Task<int> MonitorProcess()
        {
            while (!HasExited)
            {
                var res = InteropAPI.Wait(processHandle, 0);
                if (res != (int)InteropAPI.NlError.TimedOut)
                {
                    OnProcessExited(res);
                    break;
                }

                await Task.Delay(33);
            }

            return ExitCode;
        }

        private void OutputReceivedCallback(string output) =>
            OutputDataReceived?.Invoke(this, new StreamDataReceivedEvent(output));

        private void OnProcessExited(int code)
        {
            if (Interlocked.CompareExchange(ref exited, 1, exited) == 0)
            {
                ExitCode = code;
            }
        }

        private void RaiseOnExited()
        {
            if (!raiseOnExit || raiseOnExitDone)
            {
                return;
            }

            raiseOnExitDone = true;
            Exited?.Invoke(this, EventArgs.Empty);
        }
    }
}
