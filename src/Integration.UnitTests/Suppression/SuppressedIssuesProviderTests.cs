﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2020 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarLint.VisualStudio.Integration.NewConnectedMode;
using SonarLint.VisualStudio.Integration.Persistence;
using SonarLint.VisualStudio.Integration.Suppression;
using SonarQube.Client.Models;

namespace SonarLint.VisualStudio.Integration.UnitTests.Suppression
{
    [TestClass]
    public class SuppressedIssuesProviderTests
    {
        private Mock<ISonarQubeIssuesProviderFactory> sonarQubeIssuesProviderFactory;
        private ConfigurableActiveSolutionBoundTracker activeSolutionBoundTracker;

        private SuppressedIssuesProvider testSubject;

        [TestInitialize]
        public void TestInitialize()
        {
            sonarQubeIssuesProviderFactory = new Mock<ISonarQubeIssuesProviderFactory>();
            activeSolutionBoundTracker = new ConfigurableActiveSolutionBoundTracker();

            testSubject = new SuppressedIssuesProvider(sonarQubeIssuesProviderFactory.Object, activeSolutionBoundTracker);
        }

        [TestMethod]
        public void Ctor_NullSonarQubeIssuesProviderFactory_ArgumentNullException()
        {
            Action act = () => new SuppressedIssuesProvider(null, null);

            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("sonarQubeIssuesProviderFactory");
        }

        [TestMethod]
        public void Ctor_NullActiveSolutionBoundTracker_ArgumentNullException()
        {
            Action act = () => new SuppressedIssuesProvider(sonarQubeIssuesProviderFactory.Object, null);

            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("activeSolutionBoundTracker");
        }

        [TestMethod]
        public void GetSuppressedIssues_NoSolutionBinding_EmptyList()
        {
            var actual = testSubject.GetSuppressedIssues("project guid", "file path");

            actual.Should().BeEmpty();
        }

        [DataTestMethod]
        [DataRow(SolutionBindingEventType.SolutionBindingUpdated)]
        [DataRow(SolutionBindingEventType.SolutionBindingChanged)]
        public void GetSuppressedIssues_SolutionBindingEvent_StandaloneMode_EmptyList(SolutionBindingEventType eventType)
        {
            SetupBindingEvent(eventType, BindingConfiguration.Standalone);

            var actual = testSubject.GetSuppressedIssues("project guid", "file path");

            actual.Should().BeEmpty();
        }

        [DataTestMethod]
        [DataRow(SolutionBindingEventType.SolutionBindingUpdated)]
        [DataRow(SolutionBindingEventType.SolutionBindingChanged)]
        public void GetSuppressedIssues_SolutionBindingEvent_StandaloneMode_ShouldNotCreateSonarQubeIssuesProvider(SolutionBindingEventType eventType)
        {
            SetupBindingEvent(eventType, BindingConfiguration.Standalone);

            testSubject.GetSuppressedIssues("project guid", "file path");

            sonarQubeIssuesProviderFactory.Verify(x =>
                    x.Create(It.IsAny<BindingConfiguration>()),
                Times.Never);
        }

        [DataTestMethod]
        [DataRow(SonarLintMode.Connected, SolutionBindingEventType.SolutionBindingUpdated)]
        [DataRow(SonarLintMode.Connected, SolutionBindingEventType.SolutionBindingChanged)]
        [DataRow(SonarLintMode.LegacyConnected, SolutionBindingEventType.SolutionBindingUpdated)]
        [DataRow(SonarLintMode.LegacyConnected, SolutionBindingEventType.SolutionBindingChanged)]
        public void GetSuppressedIssues_SolutionBindingEvent_ConnectedMode_ListFromNewBinding(SonarLintMode mode, SolutionBindingEventType eventType)
        {
            var bindingConfiguration = new BindingConfiguration(new BoundSonarQubeProject(), mode);
            var expectedIssues = SetupExpectedIssues(bindingConfiguration);

            SetupBindingEvent(eventType, bindingConfiguration);

            var actual = testSubject.GetSuppressedIssues("project guid", "file path");

            actual.Should().BeEquivalentTo(expectedIssues);
        }

        [DataTestMethod]
        [DataRow(SonarLintMode.Standalone, SolutionBindingEventType.SolutionBindingUpdated)]
        [DataRow(SonarLintMode.Standalone, SolutionBindingEventType.SolutionBindingChanged)]
        [DataRow(SonarLintMode.LegacyConnected, SolutionBindingEventType.SolutionBindingUpdated)]
        [DataRow(SonarLintMode.LegacyConnected, SolutionBindingEventType.SolutionBindingChanged)]
        [DataRow(SonarLintMode.Connected, SolutionBindingEventType.SolutionBindingUpdated)]
        [DataRow(SonarLintMode.Connected, SolutionBindingEventType.SolutionBindingChanged)]
        public void SolutionBindingEvent_PreviousProviderIsDisposed(SonarLintMode newSonarLintMode, SolutionBindingEventType eventType)
        {
            var firstProvider = new Mock<ISonarQubeIssuesProvider>();
            var firstConfiguration = new BindingConfiguration(new BoundSonarQubeProject(), SonarLintMode.Connected);

            sonarQubeIssuesProviderFactory
                .Setup(x => x.Create(firstConfiguration))
                .Returns(firstProvider.Object);

            SetupBindingEvent(eventType, firstConfiguration);

            firstProvider.Verify(x=> x.Dispose(), Times.Never);

            var secondConfiguration = new BindingConfiguration(new BoundSonarQubeProject(), newSonarLintMode);

            SetupBindingEvent(eventType, secondConfiguration);

            firstProvider.Verify(x => x.Dispose(), Times.Once);
        }

        [TestMethod]
        public void Dispose_ProviderExists_ProviderIsDisposed()
        {
            var firstProvider = new Mock<ISonarQubeIssuesProvider>();
            var firstConfiguration = new BindingConfiguration(new BoundSonarQubeProject(), SonarLintMode.Connected);

            sonarQubeIssuesProviderFactory
                .Setup(x => x.Create(firstConfiguration))
                .Returns(firstProvider.Object);

            SetupBindingEvent(SolutionBindingEventType.SolutionBindingChanged, firstConfiguration);

            firstProvider.Verify(x => x.Dispose(), Times.Never);

            testSubject.Dispose();

            firstProvider.Verify(x => x.Dispose(), Times.Once);
        }

        [TestMethod]
        public void Dispose_EventsUnsubscribed()
        {
            var changedInvocationList = activeSolutionBoundTracker.GetSolutionBindingChangedInvocationList();
            var updatedInvocationList = activeSolutionBoundTracker.GetSolutionBindingUpdatedInvocationList();

            changedInvocationList.Should().NotBeEmpty();
            updatedInvocationList.Should().NotBeEmpty();

            testSubject.Dispose();

            changedInvocationList = activeSolutionBoundTracker.GetSolutionBindingChangedInvocationList();
            updatedInvocationList = activeSolutionBoundTracker.GetSolutionBindingUpdatedInvocationList();

            changedInvocationList.Should().BeNullOrEmpty();
            updatedInvocationList.Should().BeNullOrEmpty();
        }

        private void SetupBindingEvent(SolutionBindingEventType eventType, BindingConfiguration newConfiguration)
        {
            if (eventType == SolutionBindingEventType.SolutionBindingUpdated)
            {
                activeSolutionBoundTracker.CurrentConfiguration = newConfiguration;
                activeSolutionBoundTracker.SimulateSolutionBindingUpdated();
            }
            else
            {
                activeSolutionBoundTracker.SimulateSolutionBindingChanged(
                    new ActiveSolutionBindingEventArgs(newConfiguration));
            }
        }

        private List<SonarQubeIssue> SetupExpectedIssues(BindingConfiguration bindingConfiguration)
        {
            var expectedIssues = new List<SonarQubeIssue>
            {
                new SonarQubeIssue("file path", "hash", 1, "message", "module", "rule id", true),
                new SonarQubeIssue("file path2", "hash2", 2, "message2", "module2", "rule id2", false)
            };

            var issuesProvider = new Mock<ISonarQubeIssuesProvider>();
            issuesProvider
                .Setup(x => x.GetSuppressedIssues("project guid", "file path"))
                .Returns(expectedIssues);

            sonarQubeIssuesProviderFactory
                .Setup(x => x.Create(bindingConfiguration))
                .Returns(issuesProvider.Object);

            return expectedIssues;
        }

        public enum SolutionBindingEventType
        {
            SolutionBindingUpdated,
            SolutionBindingChanged
        }
    }
}
