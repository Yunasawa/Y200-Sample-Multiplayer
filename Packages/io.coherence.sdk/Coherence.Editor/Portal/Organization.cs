// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Portal
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Represents an organization option that can be selected in the coherence Hub window ('Coherence > Hub').
    /// </summary>
    /// <para>
    /// Organizations can be managed using the Online Dashboard at <see href="https://coherence.io/dashboard/"/>.
    /// </para>
    [Serializable]
    internal class Organization : IEquatable<Organization>
    {
        [Tooltip("Identifier of the organization.")]
        [SerializeField]
        internal string id;

        [Tooltip("Name of the organization.")]
        [SerializeField]
        internal string name;

        [SerializeField] internal string slug;
        [SerializeField] internal ProjectInfo[] projects;

        /// <summary>
        /// Identifier of the organization.
        /// </summary>
        public string Id => id ?? "";

        /// <summary>
        /// Name of the organization.
        /// </summary>
        public string Name => name ?? "";

        public string Slug => slug ?? "";
        public ProjectInfo[] Projects => projects ?? Array.Empty<ProjectInfo>();

        public Organization()
        {
            id = "";
            name = "";
            slug = "";
            projects = Array.Empty<ProjectInfo>();
        }

        public Organization(string id, string name, string slug, ProjectInfo[] projects)
        {
            this.id = id;
            this.name = name;
            this.slug = slug;
            this.projects = projects;
        }

        public static bool operator ==(Organization a, Organization b) => a is null ? b is null : a.Equals(b);
        public static bool operator !=(Organization a, Organization b) => a is null ? b is not null : !a.Equals(b);
        public bool Equals(Organization other) => string.IsNullOrEmpty(Id) ? string.IsNullOrEmpty(other?.Id) : string.Equals(Id, other?.Id);
        public override bool Equals(object obj) => Equals(obj as Organization);
        public override string ToString() => $"Organization: {Name} ({Id})";
        public override int GetHashCode() => string.IsNullOrEmpty(Id) ? 0 : Id.GetHashCode();
    }
}
