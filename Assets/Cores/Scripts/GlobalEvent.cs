using Coherence.Toolkit;
using System;

namespace Y200.ProjectMultiplayer
{
    public static class GlobalEvent
    {
        public static Action<Guid> OnWorldConnected { get; set; }
        public static Action<Guid> OnWorldDisconnected { get; set; }

        public static Action<Guid, byte> OnCharacterSelected { get; set; }
    }
}