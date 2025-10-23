// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;

    internal sealed class SimulatorPlayerAccountProvider : IPlayerAccountProvider
    {
        internal static readonly CloudUniqueId SimulatorInCloudUniqueId = new("SimulatorInCloud");

        private CloudService services;
        private PlayerAccount playerAccount;
        private readonly Func<CloudService> getServices;
        private string projectId;

        // If CloudService is initialized lazily, wait for it to become available.
        public bool IsReady => Services is not null;

        public string ProjectId
        {
            get
            {
                if (string.IsNullOrEmpty(projectId) && Services is { } services)
                {
                    projectId = services.RuntimeSettings.ProjectID;
                }

                return projectId;
            }
        }

        public CloudUniqueId CloudUniqueId => SimulatorInCloudUniqueId;

        private CloudService Services => services ??= getServices();

        public SimulatorPlayerAccountProvider(Func<CloudService> getServices) => this.getServices = getServices;

        public SimulatorPlayerAccountProvider(CloudService services) => this.services = services;

        public PlayerAccount GetPlayerAccount(LoginInfo loginInfo)
        {
            if (playerAccount != null)
            {
                return playerAccount;
            }

            playerAccount = PlayerAccount.Find(loginInfo);
            if (playerAccount is not null)
            {
                playerAccount.CloudUniqueId = SimulatorInCloudUniqueId;
                if (ProjectId is { Length: > 0 } projectId)
                {
                    playerAccount.projectId = projectId;
                }

                playerAccount.Services ??= Services;
            }
            else
            {
                playerAccount = new(loginInfo, CloudUniqueId, ProjectId, Services);
                PlayerAccount.Register(playerAccount);
            }

            playerAccount.OnDisposed += OnPlayerAccountDisposed;
            return playerAccount;
        }

        public void Dispose()
        {
            if (playerAccount != null)
            {
                var playerAccountToDispose = playerAccount;
                playerAccount = null;
                playerAccountToDispose.OnDisposed -= OnPlayerAccountDisposed;
                playerAccountToDispose.Dispose();
            }
        }

        private void OnPlayerAccountDisposed() => playerAccount = null;
    }
}
