// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using System.IO;
    using System.Linq;
    using NUnit.Framework;

    public class PostProcessorTests
    {
        [TestCase(true), TestCase(false)]
        public void PostProcessor_Updates_RuntimeSettings_Schemas_ContainsDefaults(bool isBuildingPlayer)
        {
            var expectedSchemas = Paths.AllSchemas.Where(File.Exists).Select(Path.GetFileNameWithoutExtension);
            Postprocessor.UpdateRuntimeSettings(isBuildingPlayer);

            var actualSchemas = RuntimeSettings.Instance.DefaultSchemas.Select(schema => schema.name);
            Assert.That(actualSchemas, Is.EquivalentTo(expectedSchemas).IgnoreCase);
        }
    }
}
