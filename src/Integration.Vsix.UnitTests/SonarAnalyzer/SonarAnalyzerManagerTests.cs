/*
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
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Vsix;

namespace SonarLint.VisualStudio.Integration.UnitTests.SonarAnalyzer
{
    [TestClass]
    public class SonarAnalyzerManagerTests
    {
        [TestMethod]
        public void SonarAnalyzerManager_ArgChecks()
        {
            // Arrange
            var serviceProvider = new ConfigurableServiceProvider();
            serviceProvider.RegisterService(typeof(SVsOutputWindow), new ConfigurableVsOutputWindow());

            // Act + Assert
            Exceptions.Expect<ArgumentNullException>(() => new SonarAnalyzerManager(null));
            Exceptions.Expect<ArgumentNullException>(() => new SonarAnalyzerManager(serviceProvider, null));
        }

        [TestMethod]
        public void SonarAnalyzerManager_HasNoCollidingAnalyzerReference_OnEmptyList()
        {
            SonarAnalyzerManager.HasConflictingAnalyzerReference(
                SonarAnalyzerManager.GetProjectAnalyzerConflictStatus(null))
                .Should().BeFalse("Null analyzer reference list should not report conflicting analyzer packages");

            SonarAnalyzerManager.HasConflictingAnalyzerReference(
                SonarAnalyzerManager.GetProjectAnalyzerConflictStatus(new List<AnalyzerReference>()))
                .Should().BeFalse("Empty analyzer reference list should not report conflicting analyzer packages");
        }

        [TestMethod]
        public void SonarAnalyzerManager_HasCollidingAnalyzerReference()
        {
            var version = new Version("0.1.2.3");
            version.Should().NotBe(SonarAnalyzerManager.AnalyzerVersion,
                "Test input should be different from the expected analyzer version");

            IEnumerable<AnalyzerReference> references = new AnalyzerReference[]
            {
                new ConfigurableAnalyzerReference(
                    new AssemblyIdentity(SonarAnalyzerManager.AnalyzerName, version),
                    SonarAnalyzerManager.AnalyzerName)
            };

            SonarAnalyzerManager.HasConflictingAnalyzerReference(
                SonarAnalyzerManager.GetProjectAnalyzerConflictStatus(references))
                .Should().BeTrue("Conflicting analyzer package not found");
        }

        [TestMethod]
        public void SonarAnalyzerManager_HasNoCollidingAnalyzerReference_SameNameVersion()
        {
            IEnumerable<AnalyzerReference> references = new AnalyzerReference[]
            {
                new ConfigurableAnalyzerReference(
                    new AssemblyIdentity(SonarAnalyzerManager.AnalyzerName, SonarAnalyzerManager.AnalyzerVersion),
                    SonarAnalyzerManager.AnalyzerName)
            };

            SonarAnalyzerManager.HasConflictingAnalyzerReference(
                SonarAnalyzerManager.GetProjectAnalyzerConflictStatus(references))
                .Should().BeFalse("Same named and versioned analyzers should not be reported as conflicting ones");
        }

        [TestMethod]
        public void SonarAnalyzerManager_HasNoCollidingAnalyzerReference_SameVersionDifferentName()
        {
            var name = "Some test name";
            name.Should().NotBe(SonarAnalyzerManager.AnalyzerName,
                "Test input should be different from the expected analyzer name");

            IEnumerable<AnalyzerReference> references = new AnalyzerReference[]
            {
                new ConfigurableAnalyzerReference(
                    new AssemblyIdentity(name, SonarAnalyzerManager.AnalyzerVersion), name)
            };

            SonarAnalyzerManager.HasConflictingAnalyzerReference(
                SonarAnalyzerManager.GetProjectAnalyzerConflictStatus(references))
                .Should().BeFalse("Name is not considered in the conflict checking");
        }

        [TestMethod]
        public void SonarAnalyzerManager_HasNoCollidingAnalyzerReference_NoDisplayName()
        {
            var version = new Version("0.1.2.3");
            version.Should().NotBe(SonarAnalyzerManager.AnalyzerVersion,
                "Test input should be different from the expected analyzer version");

            IEnumerable<AnalyzerReference> references = new AnalyzerReference[]
            {
                new ConfigurableAnalyzerReference(
                    new AssemblyIdentity(SonarAnalyzerManager.AnalyzerName, version),
                    null)
            };

            SonarAnalyzerManager.HasConflictingAnalyzerReference(
                SonarAnalyzerManager.GetProjectAnalyzerConflictStatus(references))
                .Should().BeFalse("Null analyzer name should not report conflict");
        }

        [TestMethod]
        public void SonarAnalyzerManager_HasNoCollidingAnalyzerReference_NoAssemblyIdentity()
        {
            IEnumerable<AnalyzerReference> references = new AnalyzerReference[]
            {
                new ConfigurableAnalyzerReference(
                    new object(),
                    SonarAnalyzerManager.AnalyzerName)
            };

            SonarAnalyzerManager.HasConflictingAnalyzerReference(
                SonarAnalyzerManager.GetProjectAnalyzerConflictStatus(references))
                .Should().BeTrue("If no AssemblyIdentity is present, but the name matches, we should report a conflict");
        }

        [TestMethod]
        public void SonarAnalyzerManager_MultipleReferencesWithSameName_CollidingVersion()
        {
            var version = new Version("0.1.2.3");
            version.Should().NotBe(SonarAnalyzerManager.AnalyzerVersion,
                "Test input should be different from the expected analyzer version");

            IEnumerable<AnalyzerReference> references = new AnalyzerReference[]
            {
                new ConfigurableAnalyzerReference(
                    new AssemblyIdentity(SonarAnalyzerManager.AnalyzerName, version),
                    SonarAnalyzerManager.AnalyzerName),
                new ConfigurableAnalyzerReference(
                    new AssemblyIdentity(SonarAnalyzerManager.AnalyzerName, SonarAnalyzerManager.AnalyzerVersion),
                    SonarAnalyzerManager.AnalyzerName),
            };

            SonarAnalyzerManager.HasConflictingAnalyzerReference(
                SonarAnalyzerManager.GetProjectAnalyzerConflictStatus(references))
                .Should().BeFalse("Having already colliding references should not disable the embedded analyzer if one is of the same version");
        }

        [TestMethod]
        public void SonarAnalyzerManager_MultipleReferencesWithSameName_NonCollidingVersion()
        {
            var version1 = new Version("0.1.2.3");
            version1.Should().NotBe(SonarAnalyzerManager.AnalyzerVersion,
                "Test input should be different from the expected analyzer version");
            var version2 = new Version("1.2.3.4");
            version2.Should().NotBe(SonarAnalyzerManager.AnalyzerVersion,
                "Test input should be different from the expected analyzer version");

            IEnumerable<AnalyzerReference> references = new AnalyzerReference[]
            {
                new ConfigurableAnalyzerReference(
                    new AssemblyIdentity(SonarAnalyzerManager.AnalyzerName, version1),
                    SonarAnalyzerManager.AnalyzerName),
                new ConfigurableAnalyzerReference(
                    new AssemblyIdentity(SonarAnalyzerManager.AnalyzerName, version2),
                    SonarAnalyzerManager.AnalyzerName),
            };

            SonarAnalyzerManager.HasConflictingAnalyzerReference(
                SonarAnalyzerManager.GetProjectAnalyzerConflictStatus(references))
                .Should().BeTrue("Having only different reference versions should disable the embedded analyzer");
        }
    }
}