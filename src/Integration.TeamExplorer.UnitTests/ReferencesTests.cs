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

using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.TeamExplorer;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    [TestClass]
    public class ReferencesTests
    {
        [TestMethod]
        public void MicrosoftTeamFoundationClient_EnsureCorrectVersion()
        {
            var tfClientAssemblyVersion = typeof(TeamExplorerController)
                .Assembly
                .GetReferencedAssemblies()
                .FirstOrDefault(ra => ra.Name == "Microsoft.TeamFoundation.Client")
                ?.Version;

            AssertIsCorrectMajorVersion(tfClientAssemblyVersion.Major);
        }

        [TestMethod]
        public void MicrosoftTeamFoundationControls_EnsureCorrectVersion()
        {
            var tfControlsAssemblyVersion = typeof(TeamExplorerController)
                .Assembly
                .GetReferencedAssemblies()
                .FirstOrDefault(ra => ra.Name == "Microsoft.TeamFoundation.Controls")
                ?.Version;

            AssertIsCorrectMajorVersion(tfControlsAssemblyVersion.Major);
        }

        private void AssertIsCorrectMajorVersion(int dllMajorVersion)
        {
            var executingAssemblyLocation = Assembly.GetExecutingAssembly().Location;
            executingAssemblyLocation.Should()
                .NotBeNull()
                .And.EndWith("Integration.TeamExplorer.UnitTests.dll");

            if (executingAssemblyLocation.Contains("\\VS2015\\"))
            {
                dllMajorVersion.Should().Be(14);
            }
            else if (executingAssemblyLocation.Contains("\\VS2017\\"))
            {
                dllMajorVersion.Should().Be(15);
            }
            else if (executingAssemblyLocation.Contains("\\VS2019\\"))
            {
                dllMajorVersion.Should().Be(16);
            }
            else
            {
                Assert.Fail("Test setup error: Expecting the path to the test dll to contain one of 'VS2015', 'VS2017' or 'VS2019'.");
            }
        }
    }
}
