// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Tests.Editor
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Coherence.Editor;
    using Common;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.TestTools;
    using static DocumentationUrlTest;

    public class DocumentationTests : CoherenceTest
    {
        private const string docsUrlFingerprint = "//docs.coherence.io/";

        [Test]
        public void DocsVersion_Equals_PackageVersion()
        {
            var version = SemVersion.Parse(RuntimeSettings.Instance.VersionInfo.Sdk);
            var docsVersion = SemVersion.Parse(RuntimeSettings.Instance.VersionInfo.DocsSlug);

            Assert.AreEqual(version.Major, docsVersion.Major);
            Assert.AreEqual(version.Minor, docsVersion.Minor);
        }

        [UnityTest]
#if COHERENCE_SKIP_LONG_UNIT_TESTS
        [Ignore("Long running test")]
#endif
        public IEnumerator DocumentationLinks_Valid()
        {
            DocumentationUrlTest[] requests = null;

            while (requests == null || requests.Length == 0)
            {
                requests = DocumentationLinks.ActiveKeys?
                    .Select(key => new DocumentationUrlTest(DocumentationLinks.GetDocsUrl(key)))
                    .ToArray();
            }

            var throttle = new DocumentationUrlTestRequestThrottle(requests);
            yield return new WaitUntil(throttle.CheckDone);

            AssertRequests(requests);
        }

        [UnityTest]
        public IEnumerator InvalidDocumentationLink_Invalid()
        {
            const string url = "https://docs.coherence.io/lorem/ipsum";

            var request = new DocumentationUrlTest(url, true);
            yield return new WaitUntil(request.CheckDone);

            Assert.That(request.Result, Is.EqualTo(TestResult.Success),
                $"Expected invalid documentation link, got ok: '{url}'");
        }

        [UnityTest]
#if COHERENCE_SKIP_LONG_UNIT_TESTS
        [Ignore("Long running test")]
#endif
        public IEnumerator HelpUrls_Valid()
        {
            var documentedTypes = TypeCache
                .GetTypesWithAttribute<HelpURLAttribute>()
                .ToDictionary(type => type, type => type.GetCustomAttribute<HelpURLAttribute>().URL)
                .Where(pair => pair.Value.Contains(docsUrlFingerprint));

            var baseUrl = DocumentationLinks.GetDocsUrl();

            var requests = documentedTypes.Select(pair => new DocumentationUrlTest(pair.Value)).ToList();

            var allValid = true;
            requests.ForEach(req =>
            {
                if (!req.Url.StartsWith(baseUrl))
                {
                    Debug.LogWarning($"'{req.Url}' doesn't conform to '{baseUrl}'");
                    allValid = false;
                }
            });

            Assert.That(allValid, "Invalid in-code documentation links encountered. See logs for details.");

            var throttle = new DocumentationUrlTestRequestThrottle(requests);
            yield return new WaitUntil(throttle.CheckDone);

            AssertRequests(requests);
        }

        [UnityTest]
#if COHERENCE_SKIP_LONG_UNIT_TESTS
        [Ignore("Long running test")]
#endif
        public IEnumerator ExternalLinks_Valid()
        {
            var requests = typeof(ExternalLinks)
                .GetFields()
                .Select(field => new DocumentationUrlTest((string)field.GetValue(null))
                {
                    ResponseValidator = _ => true,
                })
                .ToArray();

            var throttle = new DocumentationUrlTestRequestThrottle(requests);
            yield return new WaitUntil(throttle.CheckDone);

            Assert.That(requests.All(req => req.Result is TestResult.Success),
                "Invalid external links encountered. See logs for details.");
        }

        [UnityTest]
#if COHERENCE_SKIP_LONG_UNIT_TESTS
        [Ignore("Long running test")]
#endif
        public IEnumerator SampleAssetsLinks_Valid()
        {
            var requests = UIUtils.SampleAssets
                .SelectMany(asset => asset.Links)
                .Select(link => new DocumentationUrlTest(link.GetResolvedUrl()))
                .ToArray();

            var throttle = new DocumentationUrlTestRequestThrottle(requests);
            yield return new WaitUntil(throttle.CheckDone);

            Assert.That(requests.All(req => req.Result is TestResult.Success),
                "Invalid sample links encountered. See logs for details.");
        }

        private static void AssertRequests(IEnumerable<DocumentationUrlTest> requests)
        {
            Assert.NotNull(requests);

            if (requests.Any(req => req.Result is TestResult.Failure))
            {
                Assert.Fail("Invalid documentation links encountered. See logs for details.");
            }
        }
    }
}
