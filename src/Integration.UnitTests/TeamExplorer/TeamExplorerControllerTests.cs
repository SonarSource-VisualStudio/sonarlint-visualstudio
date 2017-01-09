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

using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Resources;
using SonarLint.VisualStudio.Integration.TeamExplorer;
using System;

namespace SonarLint.VisualStudio.Integration.UnitTests.TeamExplorer
{
    [TestClass]
    public class TeamExplorerControllerTests
    {
        [TestMethod]
        public void TeamExplorerController_Ctor_NullArgChecks()
        {
            Exceptions.Expect<ArgumentNullException>(() => new TeamExplorerController(null));
        }

        [TestMethod]
        public void TeamExplorerController_Ctor()
        {
            // Test case 1: no Team Explorer service
            // Setup
            var serviceProvider = new ConfigurableServiceProvider(false);

            // Act + Verify
            Exceptions.Expect<ArgumentException>(() => new TeamExplorerController(serviceProvider));

            // Test case 2: has TE service
            // Setup
            var teService = new ConfigurableTeamExplorer();
            serviceProvider.RegisterService(typeof(ITeamExplorer), teService);

            // Act + Verify
            var testSubject = new TeamExplorerController(serviceProvider);
            Assert.AreSame(teService, testSubject.TeamExplorer, "Unexpected Team Explorer service");
        }

        [TestMethod]
        public void TeamExplorerController_ShowConnectionsPage()
        {
            // Setup
            var startPageId = new Guid(TeamExplorerPageIds.GitCommits);

            var serviceProvider = new ConfigurableServiceProvider();
            var teService = new ConfigurableTeamExplorer(startPageId);
            serviceProvider.RegisterService(typeof(ITeamExplorer), teService);

            var sonarPageId = new Guid(SonarQubePage.PageId);
            var sonarPageInstance = new ConfigurableTeamExplorerPage(sonarPageId);
            teService.AvailablePages.Add(sonarPageId, sonarPageInstance);

            var testSubject = new TeamExplorerController(serviceProvider);

            // Act
            testSubject.ShowSonarQubePage();

            // Verify
            teService.AssertCurrentPage(sonarPageId);
        }
    }
}
