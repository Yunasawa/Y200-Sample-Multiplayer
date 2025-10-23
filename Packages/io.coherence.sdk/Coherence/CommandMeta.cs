// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence
{
    using Connection;
    using SimulationFrame;

    /// <summary>
    /// Command metadata. If the first parameter of a function used for command is of this type, it will be
    /// injected on the function call with appropriate values.
    /// </summary>
    public struct CommandMeta
    {
        /// <summary>
        /// ID of the client that sent the command.
        /// </summary>
        public ClientID Sender;

        /// <summary>
        /// Simulation frame at which the command was sent.
        /// </summary>
        public AbsoluteSimulationFrame Frame;

        public CommandMeta(ClientID sender, AbsoluteSimulationFrame frame)
        {
            Sender = sender;
            Frame = frame;
        }

        public override string ToString() => $"Sender: {Sender}, Frame: {Frame}";
    }
}
