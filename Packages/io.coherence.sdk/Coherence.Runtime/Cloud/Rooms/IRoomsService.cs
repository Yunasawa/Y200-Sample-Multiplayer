// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IRoomsService
    {
        IReadOnlyList<RoomData> CachedRooms { get; }
        void RemoveRoom(ulong uniqueID, string secret, Action<RequestResponse<string>> onRequestFinished);
        Task RemoveRoomAsync(ulong uniqueID, string secret);

        void CreateRoom(Action<RequestResponse<RoomData>> onRequestFinished, RoomCreationOptions roomCreationOptions);

        void CreateRoom(Action<RequestResponse<RoomData>> onRequestFinished, RoomCreationOptions roomCreationOptions, CancellationToken cancellationToken)
        {
            // Default implementation for backwards compatibility:
            if (!cancellationToken.CanBeCanceled || onRequestFinished is null)
            {
                CreateRoom(onRequestFinished, roomCreationOptions);
                return;
            }

            CreateRoom(response =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    onRequestFinished(response);
                }
            }, roomCreationOptions);
        }

        Task<RoomData> CreateRoomAsync(RoomCreationOptions roomCreationOptions);

        async Task<RoomData> CreateRoomAsync(RoomCreationOptions roomCreationOptions, CancellationToken cancellationToken)
        {
            // Default implementation for backwards compatibility:
            var result = await CreateRoomAsync(roomCreationOptions);

            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException("The operation was canceled.", null, cancellationToken);
            }

            return result;
        }

        /// <summary>Fetch available rooms.</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished, it includes the fetched rooms.</param>
        /// <param name="tags">Filter the results by a list of tags.</param>
        void FetchRooms(Action<RequestResponse<IReadOnlyList<RoomData>>> onRequestFinished, string[] tags = null);

        /// <summary>Fetch available rooms .</summary>
        /// <param name="onRequestFinished">Callback that will be invoked when the request finished, it includes the fetched rooms.</param>
        /// <param name="tags">Filter the results by a list of tags.</param>
        /// <param name="cancellationToken"> Cancellation token to cancel the request.</param>
        void FetchRooms(Action<RequestResponse<IReadOnlyList<RoomData>>> onRequestFinished, [MaybeNull] string[] tags, CancellationToken cancellationToken)
        {
            // Default implementation for backwards compatibility:
            if (!cancellationToken.CanBeCanceled || onRequestFinished is null)
            {
                FetchRooms(onRequestFinished, tags);
                return;
            }

            FetchRooms(response =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    onRequestFinished(response);
                }
            }, tags);
        }

        /// <summary>Fetch available rooms asynchronously.</summary>
        /// <param name="tags">Filter the results by a list of tags.</param>
        Task<IReadOnlyList<RoomData>> FetchRoomsAsync(string[] tags = null);

        /// <summary>Fetch available rooms asynchronously.</summary>
        /// <param name="tags">Filter the results by a list of tags.</param>
        /// <param name="cancellationToken"> Cancellation token to cancel the request.</param>
        async Task<IReadOnlyList<RoomData>> FetchRoomsAsync([MaybeNull] string[] tags, CancellationToken cancellationToken)
        {
            // Default implementation for backwards compatibility:
            var result = await FetchRoomsAsync(tags);

            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException("The operation was canceled.", null, cancellationToken);
            }

            return result;
        }
    }
}
