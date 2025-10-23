// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Transport
{
    using System.Net.Sockets;

    internal static class SocketUtils
    {
        internal static void SetupUDPClientBuffers(ushort maxBufferSize, Socket socket)
        {
            // IP + UDP headers are up to 68 bytes plus we want to be able to hold at least one
            // packet in the buffer in case there's a delay sending or revceiving.
            const int IP_OVERHEAD = 68;
            const int NUM_BUFFERED_SEND_PACKETS = 3;
            const int NUM_BUFFERED_RECV_PACKETS = 10;

            var minSendBufferSize = (maxBufferSize + IP_OVERHEAD) * NUM_BUFFERED_SEND_PACKETS;
            if (socket.SendBufferSize < minSendBufferSize)
            {
                socket.SendBufferSize = minSendBufferSize;
            }

            var minRecvBufferSize = (maxBufferSize + IP_OVERHEAD) * NUM_BUFFERED_RECV_PACKETS;
            if (socket.ReceiveBufferSize < minRecvBufferSize)
            {
                socket.ReceiveBufferSize = minRecvBufferSize;
            }
        }
    }
}
