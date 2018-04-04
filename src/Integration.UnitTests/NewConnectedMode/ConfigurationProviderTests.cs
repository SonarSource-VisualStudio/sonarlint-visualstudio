﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2018 SonarSource SA
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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.NewConnectedMode;
using SonarLint.VisualStudio.Integration.Persistence;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    [TestClass]
    public class ConfigurationProviderTests
    {
        private ConfigurableSolutionBindingSerializer legacySerializer;
        private ConfigurableConfigurationProvider newSerializer;
        private ConfigurationProvider testSubject;

        [TestInitialize]
        public void TestInitialize()
        {
            legacySerializer = new ConfigurableSolutionBindingSerializer { CurrentBinding = null };
            newSerializer = new ConfigurableConfigurationProvider { ModeToReturn = SonarLintMode.Standalone };
            testSubject = new ConfigurationProvider(legacySerializer, newSerializer);
        }

        [TestMethod]
        public void Ctor_InvalidArgs_NullLegacySerializer_Throws()
        {
            // Arrange
            Action act = () => new ConfigurationProvider(null, new ConfigurableConfigurationProvider());

            // Act & Assert
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("legacySerializer");
        }

        [TestMethod]
        public void Ctor_InvalidArgs_NullConnectedModeSerializer_Throws()
        {
            // Arrange
            var serializer = new ConfigurableSolutionBindingSerializer();
            Action act = () => new ConfigurationProvider(serializer, null);

            // Act & Assert
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("wrappedProvider");
        }

        [TestMethod]
        public void GetConfig_NoLegacyConfig_ReturnsWrapped()
        {
            // Arrange
            legacySerializer.CurrentBinding = null;
            newSerializer.ModeToReturn = SonarLintMode.Standalone;

            // Act
            var actual = testSubject.GetConfiguration();

            // Assert
            actual.Should().NotBeNull();
            actual.Project.Should().BeNull();
            actual.Mode.Should().Be(SonarLintMode.Standalone);
        }

        [TestMethod]
        public void GetConfig_WhenLegacyConfigExists_BothProvidersReturnSameLegacyConfig()
        {
            // Note that this should not happen in practice - we only expect the legacy
            // or new bindings to present. However, the legacy should take priority.

            // Arrange
            legacySerializer.CurrentBinding = new BoundSonarQubeProject();
            newSerializer.ModeToReturn = SonarLintMode.Connected;
            newSerializer.ProjectToReturn = new BoundSonarQubeProject();
            newSerializer.SavedConfiguration = null;

            // Act - get config, expecting the legacy binding
            var actual = testSubject.GetConfiguration();

            // Assert
            actual.Should().NotBeNull();
            actual.Project.Should().NotBeNull();
            actual.Project.Should().BeSameAs(legacySerializer.CurrentBinding);
            actual.Mode.Should().Be(SonarLintMode.LegacyConnected);

            newSerializer.SavedConfiguration.Project.Should().BeSameAs(legacySerializer.CurrentBinding);
            newSerializer.SavedConfiguration.Mode.Should().Be(SonarLintMode.LegacyConnected);
        }

        [TestMethod]
        public void GetConfig_NewConfigOnly_ReturnsWrapped()
        {
            // Arrange
            legacySerializer.CurrentBinding = null;
            newSerializer.ModeToReturn = SonarLintMode.Connected;
            newSerializer.ProjectToReturn = new BoundSonarQubeProject();

            // Act
            var actual = testSubject.GetConfiguration();

            // Assert
            actual.Should().NotBeNull();
            actual.Project.Should().NotBeNull();
            actual.Project.Should().BeSameAs(newSerializer.ProjectToReturn);
            actual.Mode.Should().Be(SonarLintMode.Connected);
        }

        [TestMethod]
        public void WriteConfig_InvalidArg_Throws()
        {
            // Arrange
            legacySerializer.CurrentBinding = null;

            // Act
            Action act = () => testSubject.WriteConfiguration(null);

            // Assert
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("configuration");
        }

        [TestMethod]
        public void WriteConfig_StandaloneConfig_CallsWrappedProviderOnly()
        {
            newSerializer.ModeToReturn = SonarLintMode.Connected;
            newSerializer.ProjectToReturn = new BoundSonarQubeProject();
            newSerializer.WriteResultToReturn = true;

            // Act
            var actual = testSubject.WriteConfiguration(BindingConfiguration.Standalone);

            // Assert
            actual.Should().BeTrue();
            newSerializer.SavedConfiguration.Mode.Should().Be(SonarLintMode.Standalone);
            newSerializer.SavedConfiguration.Project.Should().BeNull();
            legacySerializer.WrittenFilesCount.Should().Be(0);
        }

        [TestMethod]
        public void WriteConfig_LegacyConfig_CallsLegacySerializerAndNewSerializer()
        {
            // Arrange
            legacySerializer.WriteSolutionBindingAction = p => "filename.txt";

            var config = BindingConfiguration.CreateBoundConfiguration(new BoundSonarQubeProject(), isLegacy: true);

            // Act
            var actual = testSubject.WriteConfiguration(config);

            // Assert
            actual.Should().BeTrue();
            legacySerializer.WrittenFilesCount.Should().Be(1);
            newSerializer.SavedConfiguration.Should().BeSameAs(config);
        }

        [TestMethod]
        public void WriteConfig_NewConnectedModeConfig_CallsWrappedProviderOnly()
        {
            // Arrange
            newSerializer.WriteResultToReturn = true;
            var config = BindingConfiguration.CreateBoundConfiguration(new BoundSonarQubeProject(), isLegacy: false);

            // Act
            var actual = testSubject.WriteConfiguration(config);

            // Assert
            actual.Should().BeTrue();
            legacySerializer.WrittenFilesCount.Should().Be(0);
            newSerializer.SavedConfiguration.Project.Should().Be(config.Project);
            newSerializer.SavedConfiguration.Mode.Should().Be(SonarLintMode.Connected);
        }

    }
}
