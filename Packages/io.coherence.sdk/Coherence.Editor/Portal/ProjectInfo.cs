// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Portal
{
    [System.Serializable]
    internal class ProjectInfo
    {
        private const string getProjectInfoUrlPath = "/project";
#pragma warning disable 649

        /// <summary>
        /// The Project ID.
        /// </summary>
        public string id = "";

        /// <summary>
        /// The Project Token.
        /// </summary>
        public string portal_token = "";

        /// <summary>
        /// The Runtime Key.
        /// </summary>
        public string runtime_key = "";

        /// <summary>
        /// The name of the project.
        /// </summary>
        public string name = "";
#pragma warning restore 649
    }
}
