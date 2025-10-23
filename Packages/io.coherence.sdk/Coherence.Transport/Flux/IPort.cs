// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Flux
{
    using System;
    using System.Net;

    public delegate void PortReceivedDataCallback(byte[] data, IPEndPoint receivedFrom, object state);

    public interface IPort
    {
        void Open(PortReceivedDataCallback userCallback, ushort maxBufferSize, object userData);
        void Listen(IPEndPoint endPoint, PortReceivedDataCallback userCallback, ushort maxBufferSize, object userData);
        void Close();

        void Send(IPEndPoint endPoint, ArraySegment<byte> data);
    }
}
