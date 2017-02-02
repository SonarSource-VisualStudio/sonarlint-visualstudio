﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using FluentAssertions;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarLint.VisualStudio.Integration.UnitTests.LocalServices
{
    [TestClass]
    public class IProjectSystemHelperExtensionsTests
    {
        #region Test boilerplate

        private ConfigurableVsProjectSystemHelper projectSystem;

        [TestInitialize]
        public void TestInitialize()
        {
            var sp = new ConfigurableServiceProvider();
            this.projectSystem = new ConfigurableVsProjectSystemHelper(sp);
        }

        #endregion Test boilerplate

        [TestMethod]
        public void IProjectSystemHelperExtensions_IsKnownTestProject_ArgChecks()
        {
            // Arrange
            IVsHierarchy vsProject = new ProjectMock("myproject.proj");

            // Act + Assert
            Exceptions.Expect<ArgumentNullException>(() => IProjectSystemHelperExtensions.IsKnownTestProject(null, vsProject));
            Exceptions.Expect<ArgumentNullException>(() => IProjectSystemHelperExtensions.IsKnownTestProject(this.projectSystem, null));
        }

        [TestMethod]
        public void IProjectSystemHelperExtensions_IsKnownTestProject_IsTestProject_ReturnsTrue()
        {
            // Arrange
            var vsProject = new ProjectMock("myproject.proj");
            vsProject.SetAggregateProjectTypeGuids(ProjectSystemHelper.TestProjectKindGuid);

            // Act + Assert
            IProjectSystemHelperExtensions.IsKnownTestProject(this.projectSystem, vsProject).Should().BeTrue("Expected project with test project kind to be known test project");
        }

        [TestMethod]
        public void IProjectSystemHelperExtensions_IsKnownTestProject_IsNotTestProject_ReturnsFalse()
        {
            // Arrange
            var vsProject = new ProjectMock("myproject.proj");

            // Act + Assert
            IProjectSystemHelperExtensions.IsKnownTestProject(this.projectSystem, vsProject).Should().BeFalse("Expected project without test project kind NOT to be known test project");
        }
    }
}