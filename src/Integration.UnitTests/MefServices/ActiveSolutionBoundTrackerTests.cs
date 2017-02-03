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
using System.Windows.Threading;
using FluentAssertions;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Persistence;
using SonarLint.VisualStudio.Integration.Service;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    [TestClass]
    public class ActiveSolutionBoundTrackerTests
    {
        private ConfigurableServiceProvider serviceProvider;
        private SolutionMock solutionMock;
        private ConfigurableActiveSolutionTracker activeSolutionTracker;
        private ConfigurableHost host;
        private ConfigurableErrorListInfoBarController errorListController;
        private ConfigurableSolutionBindingInformationProvider solutionBindingInformationProvider;

        [TestInitialize]
        public void TestInitialize()
        {
            this.serviceProvider = new ConfigurableServiceProvider(false);
            this.host = new ConfigurableHost(this.serviceProvider, Dispatcher.CurrentDispatcher);
            var mefExport1 = MefTestHelpers.CreateExport<IHost>(this.host);

            this.activeSolutionTracker = new ConfigurableActiveSolutionTracker();
            var mefExport2 = MefTestHelpers.CreateExport<IActiveSolutionTracker>(this.activeSolutionTracker);

            var mefModel = ConfigurableComponentModel.CreateWithExports(mefExport1, mefExport2);

            this.serviceProvider.RegisterService(typeof(SComponentModel), mefModel, replaceExisting: true);

            this.solutionMock = new SolutionMock();
            this.serviceProvider.RegisterService(typeof(SVsSolution), this.solutionMock);

            this.errorListController = new ConfigurableErrorListInfoBarController();
            this.serviceProvider.RegisterService(typeof(IErrorListInfoBarController), this.errorListController);

            this.solutionBindingInformationProvider = new ConfigurableSolutionBindingInformationProvider();
            this.serviceProvider.RegisterService(typeof(ISolutionBindingInformationProvider), this.solutionBindingInformationProvider);
        }

        [TestMethod]
        public void ActiveSolutionBoundTracker_ArgChecls()
        {
            // Arrange
            Exceptions.Expect<ArgumentNullException>(() =>
                new ActiveSolutionBoundTracker(null, new ConfigurableActiveSolutionTracker()));
            Exceptions.Expect<ArgumentNullException>(() =>
                new ActiveSolutionBoundTracker(this.host, null));
        }

        [TestMethod]
        public void ActiveSolutionBoundTracker_Unbound()
        {
            // Arrange
            host.VisualStateManager.ClearBoundProject();

            // Act
            var testSubject = new ActiveSolutionBoundTracker(this.host, this.activeSolutionTracker);

            // Assert
            testSubject.IsActiveSolutionBound.Should().BeFalse("Unbound solution should report false activation");
            this.errorListController.RefreshCalledCount.Should().Be(1);
            this.errorListController.ResetCalledCount.Should().Be(0);
        }

        [TestMethod]
        public void ActiveSolutionBoundTracker_Bound()
        {
            // Arrange
            this.solutionBindingInformationProvider.SolutionBound = true;
            this.host.VisualStateManager.SetBoundProject(new ProjectInformation());

            // Act
            var testSubject = new ActiveSolutionBoundTracker(this.host, this.activeSolutionTracker);

            // Assert
            testSubject.IsActiveSolutionBound.Should().BeTrue("Bound solution should report true activation");
            this.errorListController.RefreshCalledCount.Should().Be(1);
            this.errorListController.ResetCalledCount.Should().Be(0);
        }

        [TestMethod]
        public void ActiveSolutionBoundTracker_Changes()
        {
            var solutionBinding = new ConfigurableSolutionBindingSerializer
            {
                CurrentBinding = new BoundSonarQubeProject()
            };
            this.serviceProvider.RegisterService(typeof(ISolutionBindingSerializer), solutionBinding);
            this.solutionBindingInformationProvider.SolutionBound = true;
            var testSubject = new ActiveSolutionBoundTracker(this.host, this.activeSolutionTracker);
            var reanalysisEventCalledCount = 0;
            testSubject.SolutionBindingChanged += (obj, args) => { reanalysisEventCalledCount++; };

            // Sanity
            testSubject.IsActiveSolutionBound.Should().BeTrue("Initially bound");
            this.errorListController.RefreshCalledCount.Should().Be(1);
            this.errorListController.ResetCalledCount.Should().Be(0);
            reanalysisEventCalledCount.Should().Be(0, "No reanalysis forced");

            // Case 1: Clear bound project
            solutionBinding.CurrentBinding = null;
            this.solutionBindingInformationProvider.SolutionBound = false;
            // Act
            host.VisualStateManager.ClearBoundProject();

            // Assert
            testSubject.IsActiveSolutionBound.Should().BeFalse("Unbound solution should report false activation");
            this.errorListController.RefreshCalledCount.Should().Be(1);
            this.errorListController.ResetCalledCount.Should().Be(0);
            reanalysisEventCalledCount.Should().Be(1, "Unbind should trigger reanalysis");

            // Case 2: Set bound project
            solutionBinding.CurrentBinding = new BoundSonarQubeProject();
            this.solutionBindingInformationProvider.SolutionBound = true;
            // Act
            host.VisualStateManager.SetBoundProject(new ProjectInformation());

            // Assert
            testSubject.IsActiveSolutionBound.Should().BeTrue("Bound solution should report true activation");
            this.errorListController.RefreshCalledCount.Should().Be(1);
            this.errorListController.ResetCalledCount.Should().Be(0);
            reanalysisEventCalledCount.Should().Be(2, "Bind should trigger reanalysis");

            // Case 3: Solution unloaded
            solutionBinding.CurrentBinding = null;
            this.solutionBindingInformationProvider.SolutionBound = false;
            // Act
            activeSolutionTracker.SimulateActiveSolutionChanged();

            // Assert
            testSubject.IsActiveSolutionBound.Should().BeFalse("Should respond to solution change event and report unbound");
            this.errorListController.RefreshCalledCount.Should().Be(2);
            this.errorListController.ResetCalledCount.Should().Be(0);
            reanalysisEventCalledCount.Should().Be(3, "Solution change should trigger reanalysis");

            // Case 4: Solution loaded
            solutionBinding.CurrentBinding = new BoundSonarQubeProject();
            this.solutionBindingInformationProvider.SolutionBound = true;
            // Act
            activeSolutionTracker.SimulateActiveSolutionChanged();

            // Assert
            testSubject.IsActiveSolutionBound.Should().BeTrue("Bound respond to solution change event and report bound");
            this.errorListController.RefreshCalledCount.Should().Be(3);
            this.errorListController.ResetCalledCount.Should().Be(0);
            reanalysisEventCalledCount.Should().Be(4, "Solution change should trigger reanalysis");

            // Case 5: Dispose and change
            // Act
            testSubject.Dispose();
            solutionBinding.CurrentBinding = null;
            this.solutionBindingInformationProvider.SolutionBound = true;
            host.VisualStateManager.ClearBoundProject();

            // Assert
            reanalysisEventCalledCount.Should().Be(4, "Once disposed should stop raising the event");
            this.errorListController.RefreshCalledCount.Should().Be(3);
            this.errorListController.ResetCalledCount.Should().Be(1);
        }
    }
}