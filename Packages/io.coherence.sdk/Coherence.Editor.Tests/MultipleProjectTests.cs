// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using Coherence.Tests;
    using Editor;
    using Editor.Portal;
    using NUnit.Framework;

    /// <summary>
    /// Edit-mode unit tests for <see cref="ProjectSettings.MultipleProjects"/>.
    /// </summary>
    public sealed class MultipleProjectTests : CoherenceTest
    {
        [TestCase(false), TestCase( true)]
        public void When_UsingMultipleProjects_Is_True_MultipleProjects_Contains_Release_And_Development_Projects(bool isDevelopmentMode)
        {
            var projectSettings = ProjectSettings.instance;
            var wasUsingMultipleProjects = projectSettings.UsingMultipleProjects;
            try
            {
                projectSettings.UsingMultipleProjects = true;
                Assert.That(projectSettings.UsingMultipleProjects, Is.True);
                Assert.That(projectSettings.MultipleProjects, Has.Length.EqualTo(2));

                var developmentProject = projectSettings.MultipleProjects[0];
                Assert.That(developmentProject.Label.text, Is.EqualTo(ConditionalProject.DevelopmentLabelText));
                Assert.That(developmentProject.HasConditions, Is.True);

                var releaseProject = projectSettings.MultipleProjects[1];
                Assert.That(releaseProject.Label.text, Is.EqualTo(ConditionalProject.ReleaseLabelText));
                Assert.That(releaseProject.HasConditions, Is.False);
                Assert.That(releaseProject.AreConditionsMet(isDevelopmentMode, resultIfHasNoConditions: false), Is.False);
                Assert.That(releaseProject.AreConditionsMet(isDevelopmentMode, resultIfHasNoConditions: true), Is.True);

                Assert.That(developmentProject.AreConditionsMet(isDevelopmentMode, resultIfHasNoConditions: false), Is.EqualTo(isDevelopmentMode));
                Assert.That(developmentProject.AreConditionsMet(isDevelopmentMode, resultIfHasNoConditions: true), Is.EqualTo(isDevelopmentMode));

                var activeProject = projectSettings.GetActiveConditionalProject(isDevelopmentMode);
                Assert.That(activeProject, Is.Not.Null);
                var expectedActiveProject = isDevelopmentMode ? developmentProject : releaseProject;
                Assert.That(activeProject, Is.EqualTo(expectedActiveProject));
            }
            finally
            {
                projectSettings.UsingMultipleProjects = wasUsingMultipleProjects;
            }
        }
    }
}
