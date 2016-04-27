//-----------------------------------------------------------------------
// <copyright file="BindingControllerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using EnvDTE;
using Microsoft.TeamFoundation.Client.CommandTarget;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Binding;
using SonarLint.VisualStudio.Integration.ProfileConflicts;
using SonarLint.VisualStudio.Integration.Resources;
using SonarLint.VisualStudio.Integration.Service;
using SonarLint.VisualStudio.Integration.TeamExplorer;
using SonarLint.VisualStudio.Integration.WPF;
using SonarLint.VisualStudio.Progress.Controller;
using System;
using System.Linq;
using System.Windows.Threading;

namespace SonarLint.VisualStudio.Integration.UnitTests.Binding
{
    [TestClass]
    public class BindingControllerTests
    {
        private ConfigurableHost host;
        private ConfigurableSonarQubeServiceWrapper sonarQubeService;
        private ConfigurableVsProjectSystemHelper projectSystemHelper;
        private TestBindingWorkflow workflow;
        private ConfigurableServiceProvider serviceProvider;
        private SolutionMock solutionMock;
        private ConfigurableVsMonitorSelection monitorSelection;
        private DTEMock dteMock;
        private ConfigurableRuleSetConflictsController conflictsController;

        [TestInitialize]
        public void TestInitialize()
        {
            KnownUIContextsAccessor.Reset();
            this.sonarQubeService = new ConfigurableSonarQubeServiceWrapper();
            this.workflow = new TestBindingWorkflow();
            this.serviceProvider = new ConfigurableServiceProvider();
            this.dteMock = new DTEMock();
            this.serviceProvider.RegisterService(typeof(DTE), this.dteMock);
            this.solutionMock = new SolutionMock();
            this.monitorSelection = KnownUIContextsAccessor.MonitorSelectionService;
            this.projectSystemHelper = new ConfigurableVsProjectSystemHelper(this.serviceProvider);
            this.conflictsController = new ConfigurableRuleSetConflictsController();
            this.serviceProvider.RegisterService(typeof(IProjectSystemHelper), this.projectSystemHelper);
            this.serviceProvider.RegisterService(typeof(IRuleSetConflictsController), this.conflictsController);
            this.serviceProvider.RegisterService(typeof(SVsGeneralOutputWindowPane), new ConfigurableVsGeneralOutputWindowPane());
            
            this.host = new ConfigurableHost(this.serviceProvider, Dispatcher.CurrentDispatcher);
            this.host.SonarQubeService = sonarQubeService;

            // Instead of ignored unexpected service, register one (for telemetry)
            this.serviceProvider.RegisterService(typeof(SComponentModel), new ConfigurableComponentModel());
        }

        #region Tests
        [TestMethod]
        public void BindingController_Ctor()
        {
            // Setup
            BindingController testSubject = this.CreateBindingController();

            // Verify
            Assert.IsNotNull(testSubject.BindCommand, "The Bind command should never be null");
        }

        [TestMethod]
        public void BindingController_Ctor_ArgumentChecks()
        {
            BindingController suppressAnalysisWarning;

            Exceptions.Expect<ArgumentNullException>(() =>
            {
                suppressAnalysisWarning = new BindingController(null);
            });
        }

        [TestMethod]
        public void BindingController_BindCommand_Status()
        {
            // Setup
            ProjectViewModel projectVM = CreateProjectViewModel();
            BindingController testSubject = this.PrepareCommandForExecution();

            // Case 1: All the requirements are set
            // Act + Verify
            Assert.IsTrue(testSubject.BindCommand.CanExecute(projectVM), "All the requirement should be satisfied for the command to be enabled");

            // Case 2: project is null
            // Act + Verify
            Assert.IsFalse(testSubject.BindCommand.CanExecute(null), "Project is null");

            // Case 3: No connection
            this.host.TestStateManager.IsConnected = false;
            // Act + Verify
            Assert.IsFalse(testSubject.BindCommand.CanExecute(projectVM), "No connection");

            // Case 4: busy
            this.host.TestStateManager.IsConnected = true;
            this.host.VisualStateManager.IsBusy = true;
            // Act + Verify
            Assert.IsFalse(testSubject.BindCommand.CanExecute(projectVM), "Connecting");
        }

        [TestMethod]
        public void BindingController_BindCommand_Status_VsState()
        {
            // Setup
            ProjectViewModel projectVM = CreateProjectViewModel();
            BindingController testSubject = this.PrepareCommandForExecution();
            ProjectMock project1 = this.solutionMock.Projects.Single();

            // Case 1: SolutionExistsAndFullyLoaded is not active
            this.monitorSelection.SetContext(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_guid, false);
            // Act + Verify
            Assert.IsFalse(testSubject.BindCommand.CanExecute(projectVM), "No UI context: SolutionExistsAndFullyLoaded");

            // Case 2: SolutionExistsAndNotBuildingAndNotDebugging is not active
            this.monitorSelection.SetContext(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_guid, true);
            this.monitorSelection.SetContext(VSConstants.UICONTEXT.SolutionExistsAndNotBuildingAndNotDebugging_guid, false);
            // Act + Verify
            Assert.IsFalse(testSubject.BindCommand.CanExecute(projectVM), "No UI context: SolutionExistsAndNotBuildingAndNotDebugging");

            // Case 3: Non-managed project kind
            this.monitorSelection.SetContext(VSConstants.UICONTEXT.SolutionExistsAndNotBuildingAndNotDebugging_guid, true);
            this.projectSystemHelper.Projects = null;
            // Act + Verify
            Assert.IsFalse(testSubject.BindCommand.CanExecute(projectVM), "No managed projects");

            // Case 4: No projects at all
            solutionMock.RemoveProject(project1);
            // Act + Verify
            Assert.IsFalse(testSubject.BindCommand.CanExecute(projectVM), "No projects");
        }

        [TestMethod]
        public void BindingController_BindCommand_Execution()
        {
            // Setup
            BindingController testSubject = this.PrepareCommandForExecution();

            // Act
            var projectToBind1 = new ProjectInformation { Key = "1" };
            ProjectViewModel projectVM1 = CreateProjectViewModel(projectToBind1);
            testSubject.BindCommand.Execute(projectVM1);

            // Verify
            this.workflow.AssertBoundProject(projectToBind1);

            // Act, bind a different project
            var projectToBind2 = new ProjectInformation { Key = "2" };
            ProjectViewModel projectVM2 = CreateProjectViewModel(projectToBind2);
            testSubject.BindCommand.Execute(projectVM2);

            // Verify
            this.workflow.AssertBoundProject(projectToBind2);
        }

        [TestMethod]
        public void BindingController_SetBindingInProgress()
        {
            // Setup
            ProjectViewModel projectVM = CreateProjectViewModel();
            BindingController testSubject = this.PrepareCommandForExecution();
            var progressEvents = new ConfigurableProgressEvents();

            foreach (var controllerResult in (ProgressControllerResult[])Enum.GetValues(typeof(ProgressControllerResult)))
            {
                this.dteMock.ToolWindows.SolutionExplorer.Window.Active = false;

                // Sanity
                Assert.IsTrue(testSubject.BindCommand.CanExecute(projectVM));

                // Act - disable
                testSubject.SetBindingInProgress(progressEvents, projectVM.ProjectInformation);

                // Verify 
                Assert.IsFalse(testSubject.BindCommand.CanExecute(projectVM), "Binding is in progress so should not be enabled");

                // Act - finish
                progressEvents.SimulateFinished(controllerResult);

                // Verify 
                Assert.IsTrue(testSubject.BindCommand.CanExecute(projectVM), "Binding is finished with result: {0}", controllerResult);
                if (controllerResult == ProgressControllerResult.Succeeded)
                {
                    Assert.IsTrue(this.dteMock.ToolWindows.SolutionExplorer.Window.Active, "SolutionExplorer window supposed to be activated");
                }
                else
                {
                    Assert.IsFalse(this.dteMock.ToolWindows.SolutionExplorer.Window.Active, "SolutionExplorer window is not supposed to be activated");
                }
            }
        }

        [TestMethod]
        public void BindingController_BindingFinished()
        {
            // Setup
            ServerViewModel serverVM = CreateServerViewModel();
            serverVM.SetProjects(new[]
            {
                new ProjectInformation { Key = "key1" }
            });
            ProjectViewModel projectVM = serverVM.Projects.First();
            BindingController testSubject = this.PrepareCommandForExecution();
            this.host.VisualStateManager.ManagedState.ConnectedServers.Add(serverVM);
            var progressEvents = new ConfigurableProgressEvents();

            foreach (ProgressControllerResult result in Enum.GetValues(typeof(ProgressControllerResult)).OfType<ProgressControllerResult>())
            {
                // Setup
                testSubject.SetBindingInProgress(progressEvents, projectVM.ProjectInformation);
                Assert.IsTrue(testSubject.IsBindingInProgress);

                // Act
                progressEvents.SimulateFinished(result);


                // Verify 
                Assert.IsFalse(testSubject.IsBindingInProgress);

                if (result == ProgressControllerResult.Succeeded)
                {
                    this.host.TestStateManager.AssertBoundProject(projectVM.ProjectInformation);
                }
                else
                {
                    this.host.TestStateManager.AssertNoBoundProject();
                }
            }
        }

        [TestMethod]
        public void BindingController_BindingFinished_Navigation()
        {
            // Setup
            ServerViewModel serverVM = CreateServerViewModel();
            serverVM.SetProjects(new[]
            {
                new ProjectInformation { Key = "key1" }
            });
            ProjectViewModel projectVM = serverVM.Projects.First();
            BindingController testSubject = this.PrepareCommandForExecution();
            this.host.VisualStateManager.ManagedState.ConnectedServers.Add(serverVM);
            var progressEvents = new ConfigurableProgressEvents();
            var teController = new ConfigurableTeamExplorerController();

            var mefExports = MefTestHelpers.CreateExport<ITeamExplorerController>(teController);
            var mefModel = ConfigurableComponentModel.CreateWithExports(mefExports);
            serviceProvider.RegisterService(typeof(SComponentModel), mefModel, replaceExisting: true);

            // Case 1: On non-successful binding no navigation will occur
            foreach(ProgressControllerResult nonSuccuess in new[] { ProgressControllerResult.Cancelled, ProgressControllerResult.Failed } )
            {
                // Act
                testSubject.SetBindingInProgress(progressEvents, projectVM.ProjectInformation);
                progressEvents.SimulateFinished(nonSuccuess);

                // Verify
                teController.AssertExpectedNumCallsShowConnectionsPage(0);
                Assert.IsFalse(this.dteMock.ToolWindows.SolutionExplorer.Window.Active);
            }

            // Case 2: Has conflicts (should navigate to team explorer page)
            this.conflictsController.HasConflicts = true;

            // Act
            testSubject.SetBindingInProgress(progressEvents, projectVM.ProjectInformation);
            progressEvents.SimulateFinished(ProgressControllerResult.Succeeded);

            // Verify 
            teController.AssertExpectedNumCallsShowConnectionsPage(1);
            Assert.IsFalse(this.dteMock.ToolWindows.SolutionExplorer.Window.Active);

            // Case 3: Has no conflicts (should navigate to solution explorer)
            this.conflictsController.HasConflicts = false;

            // Act
            testSubject.SetBindingInProgress(progressEvents, projectVM.ProjectInformation);
            progressEvents.SimulateFinished(ProgressControllerResult.Succeeded);

            // Verify 
            teController.AssertExpectedNumCallsShowConnectionsPage(1);
            Assert.IsTrue(this.dteMock.ToolWindows.SolutionExplorer.Window.Active);
        }

        [TestMethod]
        public void BindingController_SetBindingInProgress_Notifications()
        {
            // Setup
            ServerViewModel serverVM = CreateServerViewModel();
            serverVM.SetProjects(new[]
            {
                new ProjectInformation { Key = "key1" }
            });
            ProjectViewModel projectVM = serverVM.Projects.ToArray()[0];
            BindingController testSubject = this.PrepareCommandForExecution();
            var section = ConfigurableSectionController.CreateDefault();
            this.host.SetActiveSection(section);
            var progressEvents = new ConfigurableProgressEvents();
            this.host.ActiveSection.UserNotifications.ShowNotificationError("Need to make sure that this is clear once started", NotificationIds.FailedToBindId, new RelayCommand(() => { }));
            ConfigurableUserNotification userNotifications = (ConfigurableUserNotification)section.UserNotifications;

            foreach (ProgressControllerResult result in Enum.GetValues(typeof(ProgressControllerResult)).OfType<ProgressControllerResult>())
            {
                // Act - start
                testSubject.SetBindingInProgress(progressEvents, projectVM.ProjectInformation);

                // Verify 
                userNotifications.AssertNoNotification(NotificationIds.FailedToBindId);

                // Act - finish
                progressEvents.SimulateFinished(result);

                // Verify 
                if (result == ProgressControllerResult.Succeeded)
                {
                    userNotifications.AssertNoNotification(NotificationIds.FailedToBindId);
                }
                else
                {
                    userNotifications.AssertNotification(NotificationIds.FailedToBindId, Strings.FailedToToBindSolution);
                }
            }
        }

        [TestMethod]
        public void BindingController_BindCommand_OnQueryStatus()
        {
            // Setup
            BindingController testSubject = this.CreateBindingController();
            bool canExecuteChanged = false;
            testSubject.BindCommand.CanExecuteChanged += (o, e) => canExecuteChanged = true;

            // Act
            Guid notUsed = Guid.Empty;
            ((IOleCommandTarget)testSubject).QueryStatus(ref notUsed, 0, new OLECMD[0], IntPtr.Zero);

            // Verify
            Assert.IsTrue(canExecuteChanged, "The command needs to invalidate the previous CanExecute state using CanExecuteChanged event");
        }

        #endregion

        #region Helpers
        private static ProjectViewModel CreateProjectViewModel(ProjectInformation projectInfo = null)
        {
            return new ProjectViewModel(CreateServerViewModel(), projectInfo ?? new ProjectInformation());
        }

        private static ServerViewModel CreateServerViewModel()
        {
            return new ServerViewModel(new ConnectionInformation(new Uri("http://123")));
        }

        private BindingController PrepareCommandForExecution()
        {
            this.host.TestStateManager.IsConnected = true;
            BindingController testSubject = this.CreateBindingController();
            this.host.SetActiveSection(ConfigurableSectionController.CreateDefault());
            this.monitorSelection.SetContext(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_guid, true);
            this.monitorSelection.SetContext(VSConstants.UICONTEXT.SolutionExistsAndNotBuildingAndNotDebugging_guid, true);
            ProjectMock project1 = this.solutionMock.AddOrGetProject("project1");
            this.projectSystemHelper.Projects = new[] { project1 };

            // Sanity
            Assert.IsTrue(testSubject.BindCommand.CanExecute(CreateProjectViewModel()), "All the requirement should be satisfied for the command to be enabled");

            return testSubject;
        }

        private class TestBindingWorkflow : IBindingWorkflowExecutor
        {
            public ProjectInformation BoundProject { get; private set; }

            #region IBindingWorkflowExecutor.
            void IBindingWorkflowExecutor.BindProject(ProjectInformation project)
            {
                this.BoundProject = project;
            }
            #endregion

            public void AssertBoundProject(ProjectInformation expected)
            {
                Assert.AreSame(expected, this.BoundProject, "Unexpected project binding");
            }
        }

        private BindingController CreateBindingController()
        {
            return new BindingController(this.host, this.workflow);
        }

        #endregion
    }
}
