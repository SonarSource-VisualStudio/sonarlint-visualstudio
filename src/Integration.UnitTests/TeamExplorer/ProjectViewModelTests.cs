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
using System.Globalization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Resources;
using SonarLint.VisualStudio.Integration.Service;
using SonarLint.VisualStudio.Integration.TeamExplorer;

namespace SonarLint.VisualStudio.Integration.UnitTests.TeamExplorer
{
    [TestClass]
    public class ProjectViewModelTests
    {
        [TestMethod]
        public void ProjectViewModel_Ctor_NullArgumentChecks()
        {
            Exceptions.Expect<ArgumentNullException>(() =>
            {
                new ProjectViewModel(null, new ProjectInformation());
            });

            Exceptions.Expect<ArgumentNullException>(() =>
            {
                new ProjectViewModel(new ServerViewModel(new ConnectionInformation(new Uri("http://www.com"))), null);
            });
        }

        [TestMethod]
        public void ProjectViewModel_Ctor()
        {
            // Arrange
            var projectInfo = new ProjectInformation
            {
                Key = "P1",
                Name = "Project1"
            };
            var serverVM = CreateServerViewModel();

            // Act
            var viewModel = new ProjectViewModel(serverVM, projectInfo);

            // Assert
            viewModel.IsBound.Should().BeFalse();
            viewModel.Key.Should().Be(projectInfo.Key);
            viewModel.ProjectName.Should().Be(projectInfo.Name);
            viewModel.ProjectInformation.Should().Be(projectInfo);
            viewModel.Owner.Should().Be(serverVM);
        }

        [TestMethod]
        public void ProjectViewModel_ToolTipProjectName_RespectsIsBound()
        {
            // Arrange
            var projectInfo = new ProjectInformation
            {
                Key = "P1",
                Name = "Project1"
            };
            var viewModel = new ProjectViewModel(CreateServerViewModel(), projectInfo);

            // Test Case 1: When project is bound, should show message with 'bound' marker
            // Act
            viewModel.IsBound = true;

            // Assert
            StringAssert.Contains(viewModel.ToolTipProjectName, viewModel.ProjectName, "ToolTip message should include the project name");
            viewModel.ToolTipProjectName.Should().NotBe(viewModel.ProjectName, "ToolTip message should also indicate that the project is 'bound'");

            // Test Case 2: When project is NOT bound, should show project name only
            // Act
            viewModel.IsBound = false;

            // Assert
            viewModel.ToolTipProjectName.Should().Be(viewModel.ProjectName, "ToolTip message should be exactly the same as the project name");
        }

        [TestMethod]
        public void ProjectViewModel_AutomationName()
        {
            // Arrange
            var projectInfo = new ProjectInformation
            {
                Key = "P1",
                Name = "Project1"
            };
            var testSubject = new ProjectViewModel(CreateServerViewModel(), projectInfo);

            var expectedNotBound = projectInfo.Name;
            var expectedBound = string.Format(CultureInfo.CurrentCulture, Strings.AutomationProjectBoundDescription, projectInfo.Name);

            // Test case 1: bound
            // Act
            testSubject.IsBound = true;
            var actualBound = testSubject.AutomationName;

            // Assert
            actualBound.Should().Be(expectedBound, "Unexpected bound SonarQube project description");

            // Test case 2: not bound
            // Act
            testSubject.IsBound = false;
            var actualNotBound = testSubject.AutomationName;

            // Assert
            actualNotBound.Should().Be(expectedNotBound, "Unexpected unbound SonarQube project description");
        }

        private static ServerViewModel CreateServerViewModel()
        {
            return new ServerViewModel(new ConnectionInformation(new Uri("http://123")));
        }
    }
}