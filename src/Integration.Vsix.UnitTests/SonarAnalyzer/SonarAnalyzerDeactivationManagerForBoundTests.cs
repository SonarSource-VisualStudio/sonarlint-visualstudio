//-----------------------------------------------------------------------
// <copyright file="SonarAnalyzerDeactivationManagerForBoundTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Vsix;
using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace SonarLint.VisualStudio.Integration.UnitTests.SonarAnalyzer
{
    [TestClass]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", 
        "S2931:Classes with \"IDisposable\" members should implement \"IDisposable\"", 
        Justification = "By-design. Test classes will do it part of the test clean up", 
        Scope = "type", 
        Target = "~T:SonarLint.VisualStudio.Integration.UnitTests.SonarAnalyzer.SonarAnalyzerDeactivationManagerForBoundTests")]
    public class SonarAnalyzerDeactivationManagerForBoundTests
    {
        private ConfigurableServiceProvider serviceProvider;
        private ConfigurableHost host;
        private ConfigurableActiveSolutionBoundTracker activeSolutionBoundTracker;
        private SonarAnalyzerDeactivationManager testObject;

        [TestInitialize]
        public void TestInitialize()
        {
            this.serviceProvider = new ConfigurableServiceProvider(false);
            this.host = new ConfigurableHost(this.serviceProvider, Dispatcher.CurrentDispatcher);
            var mefExport1 = MefTestHelpers.CreateExport<IHost>(this.host);

            this.activeSolutionBoundTracker = new ConfigurableActiveSolutionBoundTracker();
            var mefExport2 = MefTestHelpers.CreateExport<IActiveSolutionBoundTracker>(this.activeSolutionBoundTracker);

            var mefModel = ConfigurableComponentModel.CreateWithExports(mefExport1, mefExport2);

            this.serviceProvider.RegisterService(typeof (SComponentModel), mefModel, replaceExisting: true);

            this.testObject = new SonarAnalyzerDeactivationManager(this.serviceProvider, new AdhocWorkspace());
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.activeSolutionBoundTracker.Dispose(); // be nice. and dispose
        }

        [TestMethod]
        public void SonarAnalyzerDeactivationManager_GetIsBoundWithoutAnalyzer_Unbound_Empty()
        {
            this.activeSolutionBoundTracker.IsActiveSolutionBound = false;

            Assert.IsFalse(
                this.testObject.GetIsBoundWithoutAnalyzer(
                    SonarAnalyzerDeactivationManager.GetProjectAnalyzerConflictStatus(null)),
                "Unbound solution should never return true");

            Assert.IsFalse(
                this.testObject.GetIsBoundWithoutAnalyzer(
                    SonarAnalyzerDeactivationManager.GetProjectAnalyzerConflictStatus(new List<AnalyzerReference>())),
                "Unbound solution should never return true");
        }

        [TestMethod]
        public void SonarAnalyzerDeactivationManager_GetIsBoundWithoutAnalyzer_Unbound_Conflicting()
        {
            this.activeSolutionBoundTracker.IsActiveSolutionBound = false;

            var version = new Version("0.1.2.3");
            Assert.AreNotEqual(SonarAnalyzerDeactivationManager.AnalyzerVersion, version,
                "Test input should be different from the expected analyzer version");

            IEnumerable<AnalyzerReference> references = new AnalyzerReference[]
            {
                new TestAnalyzerReference(
                    new AssemblyIdentity(SonarAnalyzerDeactivationManager.AnalyzerName, version),
                    SonarAnalyzerDeactivationManager.AnalyzerName)
            };

            Assert.IsFalse(
                this.testObject.GetIsBoundWithoutAnalyzer(
                    SonarAnalyzerDeactivationManager.GetProjectAnalyzerConflictStatus(references)),
                "Unbound solution should never return true");
        }

        [TestMethod]
        public void SonarAnalyzerDeactivationManager_GetIsBoundWithoutAnalyzer_Unbound_NonConflicting()
        {
            this.activeSolutionBoundTracker.IsActiveSolutionBound = false;

            IEnumerable<AnalyzerReference> references = new AnalyzerReference[]
            {
                new TestAnalyzerReference(
                    new AssemblyIdentity(SonarAnalyzerDeactivationManager.AnalyzerName, SonarAnalyzerDeactivationManager.AnalyzerVersion),
                    SonarAnalyzerDeactivationManager.AnalyzerName)
            };

            Assert.IsFalse(
                this.testObject.GetIsBoundWithoutAnalyzer(
                    SonarAnalyzerDeactivationManager.GetProjectAnalyzerConflictStatus(references)),
                "Unbound solution should never return true");
        }

        [TestMethod]
        public void SonarAnalyzerDeactivationManager_GetIsBoundWithoutAnalyzer_Bound_Empty()
        {
            this.activeSolutionBoundTracker.IsActiveSolutionBound = true;

            Assert.IsTrue(
                this.testObject.GetIsBoundWithoutAnalyzer(
                    SonarAnalyzerDeactivationManager.GetProjectAnalyzerConflictStatus(null)),
                "Bound solution with no reference should never return true");

            Assert.IsTrue(
                this.testObject.GetIsBoundWithoutAnalyzer(
                    SonarAnalyzerDeactivationManager.GetProjectAnalyzerConflictStatus(new List<AnalyzerReference>())),
                "Bound solution with no reference should never return true");
        }

        [TestMethod]
        public void SonarAnalyzerDeactivationManager_GetIsBoundWithoutAnalyzer_Bound_Conflicting()
        {
            this.activeSolutionBoundTracker.IsActiveSolutionBound = true;

            var version = new Version("0.1.2.3");
            Assert.AreNotEqual(SonarAnalyzerDeactivationManager.AnalyzerVersion, version,
                "Test input should be different from the expected analyzer version");

            IEnumerable<AnalyzerReference> references = new AnalyzerReference[]
            {
                new TestAnalyzerReference(
                    new AssemblyIdentity(SonarAnalyzerDeactivationManager.AnalyzerName, version),
                    SonarAnalyzerDeactivationManager.AnalyzerName)
            };

            Assert.IsFalse(
                this.testObject.GetIsBoundWithoutAnalyzer(
                    SonarAnalyzerDeactivationManager.GetProjectAnalyzerConflictStatus(references)),
                "Bound solution with conflicting analyzer name should never return true");
        }

        [TestMethod]
        public void SonarAnalyzerDeactivationManager_GetIsBoundWithoutAnalyzer_Bound_NonConflicting()
        {
            this.activeSolutionBoundTracker.IsActiveSolutionBound = true;

            IEnumerable<AnalyzerReference> references = new AnalyzerReference[]
            {
                new TestAnalyzerReference(
                    new AssemblyIdentity(SonarAnalyzerDeactivationManager.AnalyzerName, SonarAnalyzerDeactivationManager.AnalyzerVersion),
                    SonarAnalyzerDeactivationManager.AnalyzerName)
            };

            Assert.IsFalse(
                this.testObject.GetIsBoundWithoutAnalyzer(
                    SonarAnalyzerDeactivationManager.GetProjectAnalyzerConflictStatus(references)),
                "Bound solution with conflicting analyzer name should never return true");
        }
    }
}
