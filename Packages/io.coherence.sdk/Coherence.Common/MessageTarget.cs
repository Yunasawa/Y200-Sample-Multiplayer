// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence
{
    using System;

    /// <summary>
    ///     Defines recipients of the message.
    /// </summary>
    public enum MessageTarget : byte
    {
        /// <summary>
        ///     Message will be sent only to the owner of an entity.
        /// </summary>
        StateAuthorityOnly = 0,

        /// <summary>
        ///     Message will be sent to everyone, including the client sending it.
        /// </summary>
        All = 1,

        /// <summary>
        ///     Message will be sent to everyone, excluding the client sending it.
        /// </summary>
        Other = 2,

        /// <summary>
        ///     Message will be sent only to the client with input authority.
        /// </summary>
        InputAuthorityOnly = 3,

        [Deprecated("07/2025", 1, 8, 0, Reason = "Replaced by " + nameof(StateAuthorityOnly))]
        AuthorityOnly = 0,
    }
}
