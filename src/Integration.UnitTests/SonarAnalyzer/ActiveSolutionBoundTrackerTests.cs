//-----------------------------------------------------------------------
// <copyright file="ActiveSolutionBoundTrackerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Persistence;
using SonarLint.VisualStudio.Integration.Service;
using SonarLint.VisualStudio.Integration.SonarAnalyzer;
using System;
using System.Windows.Threading;

namespace SonarLint.VisualStudio.Integration.UnitTests.SonarAnalyzer
{
    [TestClass]
    public class ActiveSolutionBoundTrackerTests
    {
        private ConfigurableServiceProvider serviceProvider;
        private SolutionMock solutionMock;
        private ConfigurableActiveSolutionTracker activeSolutionTracker;
        private ConfigurableHost host;

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
        }

        [TestMethod]
        public void ActiveSolutionBoundTracker_ArgChecls()
        {
            // Setup
            Exceptions.Expect<ArgumentNullException>(() => new ActiveSolutionBoundTracker(null, new ConfigurableActiveSolutionTracker()));
            Exceptions.Expect<ArgumentNullException>(() => new ActiveSolutionBoundTracker(this.host, null));
        }

        [TestMethod]
        public void ActiveSolutionBoundTracker_Unbound()
        {
            // Setup
            var solutionBinding = new ConfigurableSolutionBinding
            {
                CurrentBinding = null
            };
            this.serviceProvider.RegisterService(typeof(ISolutionBinding), solutionBinding);
            host.VisualStateManager.SetBoundProject(new ProjectInformation());
            var testSubject = new ActiveSolutionBoundTracker(this.host, this.activeSolutionTracker);

            // Verify
            Assert.IsFalse(testSubject.IsActiveSolutionBound, "Unbound solution should report false activation");
        }

        [TestMethod]
        public void ActiveSolutionBoundTracker_Bound()
        {
            // Setup
            var solutionBinding = new ConfigurableSolutionBinding
            {
                CurrentBinding = new BoundSonarQubeProject()
            };
            this.serviceProvider.RegisterService(typeof(ISolutionBinding), solutionBinding);
            host.VisualStateManager.SetBoundProject(new ProjectInformation());
            var testSubject = new ActiveSolutionBoundTracker(this.host, this.activeSolutionTracker);

            // Verify
            Assert.IsTrue(testSubject.IsActiveSolutionBound, "Bound solution should report true activation");
        }

        [TestMethod]
        public void ActiveSolutionBoundTracker_Changes()
        {
            // Setup
            var solutionBinding = new ConfigurableSolutionBinding
            {
                CurrentBinding = new BoundSonarQubeProject()
            };
            this.serviceProvider.RegisterService(typeof(ISolutionBinding), solutionBinding);
            var testSubject = new ActiveSolutionBoundTracker(this.host, this.activeSolutionTracker);

            // Sanity
            Assert.IsTrue(testSubject.IsActiveSolutionBound, "Initially bound");

            // Case 1: Clear bound project
            // Act
            solutionBinding.CurrentBinding = null;
            host.VisualStateManager.ClearBoundProject();

            // Verify
            Assert.IsFalse(testSubject.IsActiveSolutionBound, "Unbound solution should report false activation");

            // Case 2: Set bound project
            solutionBinding.CurrentBinding = new BoundSonarQubeProject();
            // Act
            host.VisualStateManager.SetBoundProject(new ProjectInformation());

            // Verify
            Assert.IsTrue(testSubject.IsActiveSolutionBound, "Bound solution should report true activation");

            // Case 3: Dispose and change
            // Act
            testSubject.Dispose();
            solutionBinding.CurrentBinding = null;
            host.VisualStateManager.ClearBoundProject();

            // Verify
            Assert.IsTrue(testSubject.IsActiveSolutionBound, "Once disposed should stop tracking and remain as it was");
        }
    }
}
