// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using Connection;
    using Core;
    using Entities;
    using Log;
    using Moq;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using Object = UnityEngine.Object;

    public sealed class MockBridgeBuilder : IDisposable
    {
        private bool isSimulatorOrHost;
        private ConnectionType connectionType;
        private Scene scene;
        private ClientID? clientID;
        private bool isConnected;
        private CoherenceSyncConfig clientConnectionEntry;

        public MockBridgeBuilder SetScene(Scene scene)
        {
            this.scene = scene;
            return this;
        }

        public MockBridgeBuilder SetIsSimulatorOrHost(bool isSimulatorOrHost = true)
        {
            this.isSimulatorOrHost = isSimulatorOrHost;
            return this;
        }

        public MockBridgeBuilder SetConnectionType(ConnectionType connectionType)
        {
            this.connectionType = connectionType;
            return this;
        }

        public MockBridgeBuilder SetClientID(ClientID id)
        {
            this.clientID = id;
            return this;
        }

        public MockBridgeBuilder Connected()
        {
            this.isConnected = true;
            return this;
        }

        public Result Build()
        {
            var logger = Log.GetLogger<MockBridgeBuilder>();
            var idGenerator = new EntityIDGenerator(Entity.ClientInitialIndex, Entity.MaxRelativeID, Entity.Relative, logger);

            var mockClient = new Mock<IClient>(MockBehavior.Strict);
            _ = mockClient.Setup(client => client.IsConnected()).Returns(() => true);
            _ = mockClient.Setup(client => client.CreateEntity(It.IsAny<ICoherenceComponentData[]>(), It.IsAny<bool>())).Returns(() =>
            {
                _ = idGenerator.GetEntity(out var id);
                return id;
            });

            _ = mockClient.SetupAdd(client => client.OnEntityCreated += It.IsAny<Action<Entity, IncomingEntityUpdate>>());

            clientConnectionEntry = (CoherenceSyncConfig)ScriptableObject.CreateInstance(typeof(CoherenceSyncConfig));

            var mockBridge = new Mock<ICoherenceBridge>(MockBehavior.Strict);
            if (clientID.HasValue)
            {
                _ = mockBridge.Setup(bridge => bridge.ClientID).Returns(clientID.Value);
            }

            _ = mockBridge.Setup(bridge => bridge.Client).Returns(mockClient.Object);
            _ = mockBridge.Setup(bridge => bridge.ClientFixedSimulationFrame).Returns(0);
            _ = mockBridge.Setup(bridge => bridge.GetClientConnectionEntry()).Returns(clientConnectionEntry);
            _ = mockBridge.Setup(bridge => bridge.EnableClientConnections).Returns(true);
            _ = mockBridge.Setup(bridge => bridge.InstantiationScene).Returns(scene);
            _ = mockBridge.Setup(bridge => bridge.IsSimulatorOrHost).Returns(isSimulatorOrHost);
            _ = mockBridge.Setup(bridge => bridge.ConnectionType).Returns(connectionType);
            mockBridge.Setup(bridge => bridge.OnNetworkEntityCreatedInvoke(It.IsAny<NetworkEntityState>())).Verifiable();
            _ = mockBridge.Setup(bridge => bridge.IsConnected).Returns(isConnected);

            var networkTime = new NetworkTime();
            _ = mockBridge.Setup(bridge => bridge.NetworkTime).Returns(networkTime);

            return new(mockBridge, mockClient);
        }

        public readonly struct Result
        {
            public readonly Mock<ICoherenceBridge> mockBridge;
            public readonly Mock<IClient> mockClient;

            public Result(Mock<ICoherenceBridge> mockBridge, Mock<IClient> mockClient)
            {
                this.mockBridge = mockBridge;
                this.mockClient = mockClient;
            }

            public void Deconstruct(out Mock<ICoherenceBridge> mockBridge, out Mock<IClient> mockClient)
            {
                mockBridge = this.mockBridge;
                mockClient = this.mockClient;
            }

            public static implicit operator Mock<ICoherenceBridge>(Result result) => result.mockBridge;
        }

        public void Dispose()
        {
            if (clientConnectionEntry)
            {
                Object.DestroyImmediate(clientConnectionEntry);
                clientConnectionEntry = null;
            }
        }
    }
}
