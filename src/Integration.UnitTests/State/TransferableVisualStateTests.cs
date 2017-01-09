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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.State;
using SonarLint.VisualStudio.Integration.TeamExplorer;

namespace SonarLint.VisualStudio.Integration.UnitTests.State
{
    [TestClass]
    public class TransferableVisualStateTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            ThreadHelper.SetCurrentThreadAsUIThread();
        }

        [TestMethod]
        public void TransferableVisualState_DefaultState()
        {
            // Setup
            var testSubject = new TransferableVisualState();

            // Verify
            Assert.IsFalse(testSubject.HasBoundProject);
            Assert.IsFalse(testSubject.IsBusy);
            Assert.IsNotNull(testSubject.ConnectedServers);
            Assert.AreEqual(0, testSubject.ConnectedServers.Count);
        }

        [TestMethod]
        public void TransferableVisualState_BoundProjectManagement()
        {
            // Setup
            var testSubject = new TransferableVisualState();
            var server = new ServerViewModel(new Integration.Service.ConnectionInformation(new System.Uri("http://server")));
            var project1 = new ProjectViewModel(server, new Integration.Service.ProjectInformation());
            var project2 = new ProjectViewModel(server, new Integration.Service.ProjectInformation());

            // Act (bind to something)
            testSubject.SetBoundProject(project1);

            // Verify
            Assert.IsTrue(testSubject.HasBoundProject);
            Assert.IsTrue(project1.IsBound);
            Assert.IsFalse(project2.IsBound);
            Assert.IsFalse(server.ShowAllProjects);

            // Act (bind to something else)
            testSubject.SetBoundProject(project2);

            // Verify
            Assert.IsTrue(testSubject.HasBoundProject);
            Assert.IsFalse(project1.IsBound);
            Assert.IsTrue(project2.IsBound);
            Assert.IsFalse(server.ShowAllProjects);

            // Act(clear binding)
            testSubject.ClearBoundProject();

            // Verify
            Assert.IsFalse(testSubject.HasBoundProject);
            Assert.IsFalse(project1.IsBound);
            Assert.IsFalse(project2.IsBound);
            Assert.IsTrue(server.ShowAllProjects);
        }
    }
}
