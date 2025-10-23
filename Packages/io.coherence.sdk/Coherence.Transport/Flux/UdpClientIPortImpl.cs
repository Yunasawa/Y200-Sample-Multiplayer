// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

using System.Net.Sockets;
using Coherence.Log;

namespace Coherence.Flux
{
    using System;
    using System.Net;
    using Coherence.Transport;

    /*
     * Implementation of a IPort that reads data asynchronously by making use of
     * the BeginReceive() and EndReceive() APIs.
     * Objects of this class can be re-used by calling Start() and Stop(). The reason
     * why we rely on calling Stop() is so that we have full control over when we
     * stop receiving messages.
     */
    internal class UdpClient : IPort
    {
        private System.Net.Sockets.UdpClient udp;
        private UdpAsyncRead read;
        private Logger logger;

        public UdpClient(Logger logger)
        {
            this.logger = logger.With<UdpClient>();
        }

        public void Open(PortReceivedDataCallback userCallback, ushort maxBufferSize, object userData)
        {
            if (udp != null)
            {
                throw new InvalidOperationException($"Can't open multiple times. Use '{nameof(Close)}' first.");
            }

            udp = new System.Net.Sockets.UdpClient();

            SocketUtils.SetupUDPClientBuffers(maxBufferSize, udp.Client);

            SetupUdpClient();

            logger.Debug("Open", ("localEp", udp.Client.LocalEndPoint));

            read = new UdpAsyncRead(udp, userCallback, userData, ReceiveCallback);
            read.Begin();
        }

        public void Listen(IPEndPoint endPoint, PortReceivedDataCallback userCallback, ushort maxBufferSize, object userData)
        {
            if (udp != null)
            {
                throw new InvalidOperationException($"Can't open multiple times. Use '{nameof(Close)}' first.");
            }

            var localBindPoint = endPoint;
            udp = new System.Net.Sockets.UdpClient(localBindPoint);

            SocketUtils.SetupUDPClientBuffers(maxBufferSize, udp.Client);

            logger.Debug("Listen", ("localEp", udp.Client.LocalEndPoint));

            read = new UdpAsyncRead(udp, userCallback, userData, ReceiveCallback);
            read.Begin();
        }

        public void Close()
        {
            if (udp == null)
            {
                return;
            }

            // Null-out to indicate closed client
            // clearing the read so it can be checked in
            // ReceiveCallback if ar.AsyncState doesn't return null
            read = null;

            udp.Close();
            udp = null;
        }

        public void Send(IPEndPoint endPoint, ArraySegment<byte> data)
        {
            _ = udp.Send(data.Array, data.Count, endPoint);
        }

        private void SetupUdpClient()
        {
            try
            {
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
            }
            catch (Exception exception)
            {
                logger.Warning(Warning.FluxDisableAddressReuse, ("exception", exception));
            }

            var localBindPoint = new IPEndPoint(IPAddress.Any, 0);
            udp.Client.Bind(localBindPoint);
        }

        private static void HandleReceivedData(IAsyncResult ar)
        {
            UdpAsyncRead read = (UdpAsyncRead)ar.AsyncState;
            read.End(ar);
            read.Report();
            read.Begin();
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            // Check the state of read and ar.AsyncState in case
            // the socket was closed early or the client port is closed.
            if (read == null || (UdpAsyncRead)ar.AsyncState == null)
            {
                // closed while reading.
                return;
            }

            try
            {
                HandleReceivedData(ar);
            }
            catch (ObjectDisposedException)
            {
                /*
                 * Closing the socket (see Stop()) will always trigger the callback
                 * expecting us to end it as per usual but if the socket has been closed
                 * a ObjectDisposedException exception will be thrown.
                 */
            }
            catch (SocketException socketException)
            {
                UdpAsyncRead read = (UdpAsyncRead)ar.AsyncState;
                if (socketException.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // That's fine, we couldn't reach remote
                    read.Begin();
                    return;
                }

                if (read != null)
                {
                    // null means we shut down the client and the read ended with an exception which is ok.
                    return;
                }

                logger.Error(Error.FluxSocketException, ("exception", socketException.ToString()));
            }
            catch (Exception exception)
            {
                logger.Error(Error.FluxSocketException, ("exception", exception.ToString()));
            }
        }
    }
}
