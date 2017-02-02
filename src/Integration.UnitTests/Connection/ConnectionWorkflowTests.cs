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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Threading;
using FluentAssertions;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Connection;
using SonarLint.VisualStudio.Integration.Resources;
using SonarLint.VisualStudio.Integration.Service;
using SonarLint.VisualStudio.Integration.Service.DataModel;
using SonarLint.VisualStudio.Integration.TeamExplorer;
using SonarLint.VisualStudio.Integration.WPF;

namespace SonarLint.VisualStudio.Integration.UnitTests.Connection
{
    [TestClass]
    public class ConnectionWorkflowTests
    {
        private ConfigurableServiceProvider serviceProvider;
        private ConfigurableSonarQubeServiceWrapper sonarQubeService;
        private ConfigurableHost host;
        private ConfigurableIntegrationSettings settings;
        private ConfigurableProjectSystemFilter filter;
        private ConfigurableVsOutputWindowPane outputWindowPane;
        private ConfigurableVsProjectSystemHelper projectSystemHelper;

        [TestInitialize]
        public void TestInit()
        {
            this.serviceProvider = new ConfigurableServiceProvider();
            this.sonarQubeService = new ConfigurableSonarQubeServiceWrapper();
            this.host = new ConfigurableHost(this.serviceProvider, Dispatcher.CurrentDispatcher);
            this.host.SetActiveSection(ConfigurableSectionController.CreateDefault());
            this.host.SonarQubeService = this.sonarQubeService;
            this.projectSystemHelper = new ConfigurableVsProjectSystemHelper(this.serviceProvider);

            this.sonarQubeService.RegisterServerPlugin(new ServerPlugin { Key = MinimumSupportedServerPlugin.CSharp.Key, Version = MinimumSupportedServerPlugin.CSharp.MinimumVersion });
            this.sonarQubeService.RegisterServerPlugin(new ServerPlugin { Key = MinimumSupportedServerPlugin.VbNet.Key, Version = MinimumSupportedServerPlugin.VbNet.MinimumVersion });
            this.settings = new ConfigurableIntegrationSettings();

            var mefExports = MefTestHelpers.CreateExport<IIntegrationSettings>(settings);
            var mefModel = ConfigurableComponentModel.CreateWithExports(mefExports);
            this.serviceProvider.RegisterService(typeof(SComponentModel), mefModel);

            this.filter = new ConfigurableProjectSystemFilter();
            this.serviceProvider.RegisterService(typeof(IProjectSystemFilter), this.filter);

            var outputWindow = new ConfigurableVsOutputWindow();
            this.outputWindowPane = outputWindow.GetOrCreateSonarLintPane();
            this.serviceProvider.RegisterService(typeof(SVsOutputWindow), outputWindow);
            this.serviceProvider.RegisterService(typeof(IProjectSystemHelper), this.projectSystemHelper);
        }

        #region Tests

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConnectionWorkflow_ConnectionStep_WhenGivenANullHost_ThrowsArgumentNullException()
        {
            // Arrange & Act
            ConnectionWorkflow testSubject = new ConnectionWorkflow(null, new RelayCommand(() => { }));

            // Assert
            FluentAssertions.Execution.Execute.Assertion.FailWith("Expected exception of type ArgumentNullException but no exception was thrown.");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConnectionWorkflow_ConnectionStep_WhenGivenANullParentCommand_ThrowsArgumentNullException()
        {
            // Arrange & Act
            ConnectionWorkflow testSubject = new ConnectionWorkflow(this.host, null);

            // Assert
            FluentAssertions.Execution.Execute.Assertion.FailWith("Expected exception of type ArgumentNullException but no exception was thrown.");
        }

        [TestMethod]
        public void ConnectionWorkflow_ConnectionStep_ConnectedServerCanBeGetAndSet()
        {
            // Arrange
            var connectionInfo = new ConnectionInformation(new Uri("http://server"));
            ConnectionWorkflow testSubject = new ConnectionWorkflow(this.host, new RelayCommand(() => { }));

            // Act
            testSubject.ConnectedServer = connectionInfo;

            // Assert
            connectionInfo.Should().Be(testSubject.ConnectedServer);
        }

        [TestMethod]
        public void ConnectionWorkflow_ConnectionStep_WhenCSharpPluginAndAnyCSharpProject_SuccessfulConnection()
        {
            ConnectionWorkflow_ConnectionStep_WhenXPluginAndAnyXProject_SuccessfulConnection("foo.csproj", ProjectSystemHelper.CSharpProjectKind);
        }

        [TestMethod]
        public void ConnectionWorkflow_ConnectionStep_WhenVBNetPluginAndAnyVBNetProject_SuccessfulConnection()
        {
            ConnectionWorkflow_ConnectionStep_WhenXPluginAndAnyXProject_SuccessfulConnection("foo.vbproj", ProjectSystemHelper.VbProjectKind);
        }

        private void ConnectionWorkflow_ConnectionStep_WhenXPluginAndAnyXProject_SuccessfulConnection(string projectName, string projectKind)
        {
            // Arrange
            var connectionInfo = new ConnectionInformation(new Uri("http://server"));
            var projects = new ProjectInformation[] { new ProjectInformation { Key = "project1" } };
            this.sonarQubeService.ReturnProjectInformation = projects;
            this.projectSystemHelper.Projects = new[] { new ProjectMock(projectName) { ProjectKind = projectKind } };
            bool projectChangedCallbackCalled = false;
            this.host.TestStateManager.SetProjectsAction = (c, p) =>
            {
                projectChangedCallbackCalled = true;
                c.Should().Be(connectionInfo, "Unexpected connection");
                CollectionAssert.AreEqual(projects, p.ToArray(), "Unexpected projects");
            };

            var controller = new ConfigurableProgressController();
            var executionEvents = new ConfigurableProgressStepExecutionEvents();
            string connectionMessage = connectionInfo.ServerUri.ToString();
            var testSubject = new ConnectionWorkflow(this.host, new RelayCommand(AssertIfCalled));

            // Act
            testSubject.ConnectionStep(controller, CancellationToken.None, connectionInfo, executionEvents);

            // Assert
            controller.NumberOfAbortRequests.Should().Be(0);
            executionEvents.AssertProgressMessages(
                connectionMessage,
                Strings.DetectingServerPlugins,
                Strings.ConnectionResultSuccess);
            projectChangedCallbackCalled.Should().BeTrue("ConnectedProjectsCallaback was not called");
            sonarQubeService.ConnectionRequestsCount.Should().Be(1);
            testSubject.ConnectedServer.Should().Be(connectionInfo);
            ((ConfigurableUserNotification)this.host.ActiveSection.UserNotifications).AssertNoShowErrorMessages();
            ((ConfigurableUserNotification)this.host.ActiveSection.UserNotifications).AssertNoNotification(NotificationIds.FailedToConnectId);
        }

        [TestMethod]
        public void ConnectionWorkflow_ConnectionStep_WhenMissingCSharpPluginAndVBNetPlugin_AbortsWorkflowAndDisconnects()
        {
            // Arrange
            var connectionInfo = new ConnectionInformation(new Uri("http://server"));
            ConnectionWorkflow testSubject = new ConnectionWorkflow(this.host, new RelayCommand(() => { }));
            var controller = new ConfigurableProgressController();
            this.sonarQubeService.AllowConnections = true;
            this.sonarQubeService.ReturnProjectInformation = new ProjectInformation[0];
            this.sonarQubeService.ClearServerPlugins();
            this.host.SetActiveSection(ConfigurableSectionController.CreateDefault());
            ConfigurableUserNotification notifications = (ConfigurableUserNotification)this.host.ActiveSection.UserNotifications;
            var executionEvents = new ConfigurableProgressStepExecutionEvents();

            // Act
            testSubject.ConnectionStep(controller, CancellationToken.None, connectionInfo, executionEvents);

            // Assert
            controller.NumberOfAbortRequests.Should().Be(1);
            executionEvents.AssertProgressMessages(
                connectionInfo.ServerUri.ToString(),
                Strings.DetectingServerPlugins,
                Strings.ConnectionResultFailure);
            notifications.AssertNotification(NotificationIds.BadServerPluginId, Strings.ServerHasNoSupportedPluginVersion);
        }

        [TestMethod]
        public void ConnectionWorkflow_ConnectionStep_WhenPluginOkAndNoProjects_AbortsWorkflowAndDisconnects()
        {
            // Arrange
            var connectionInfo = new ConnectionInformation(new Uri("http://server"));
            var projects = new ProjectInformation[] { new ProjectInformation { Key = "project1" } };
            this.sonarQubeService.ReturnProjectInformation = projects;
            bool projectChangedCallbackCalled = false;
            this.host.TestStateManager.SetProjectsAction = (c, p) =>
            {
                projectChangedCallbackCalled = true;
                c.Should().Be(connectionInfo, "Unexpected connection");
                CollectionAssert.AreEqual(projects, p.ToArray(), "Unexpected projects");
            };

            var controller = new ConfigurableProgressController();
            var executionEvents = new ConfigurableProgressStepExecutionEvents();
            string connectionMessage = connectionInfo.ServerUri.ToString();
            var testSubject = new ConnectionWorkflow(this.host, new RelayCommand(AssertIfCalled));
            ConfigurableUserNotification notifications = (ConfigurableUserNotification)this.host.ActiveSection.UserNotifications;

            // Act
            testSubject.ConnectionStep(controller, CancellationToken.None, connectionInfo, executionEvents);

            // Assert
            controller.NumberOfAbortRequests.Should().Be(1);
            executionEvents.AssertProgressMessages(
                connectionInfo.ServerUri.ToString(),
                Strings.DetectingServerPlugins,
                Strings.ConnectionResultFailure);
            projectChangedCallbackCalled.Should().BeFalse("ConnectedProjectsCallaback was called");
            notifications.AssertNotification(NotificationIds.BadServerPluginId, Strings.SolutionContainsNoSupportedProject);
        }

        [TestMethod]
        public void ConnectionWorkflow_ConnectionStep_WhenCSharpPluginAndNoCSharpProject_AbortsWorkflowAndDisconnects()
        {
            ConnectionWorkflow_ConnectionStep_WhenXPluginAndNoXProject_AbortsWorkflowAndDisconnects("foo.vbproj", ProjectSystemHelper.VbProjectKind, MinimumSupportedServerPlugin.CSharp);
        }

        [TestMethod]
        public void ConnectionWorkflow_ConnectionStep_WhenVBNetPluginAndNoVBNetProject_AbortsWorkflowAndDisconnects()
        {
            ConnectionWorkflow_ConnectionStep_WhenXPluginAndNoXProject_AbortsWorkflowAndDisconnects("foo.csproj", ProjectSystemHelper.CSharpProjectKind, MinimumSupportedServerPlugin.VbNet);
        }

        private void ConnectionWorkflow_ConnectionStep_WhenXPluginAndNoXProject_AbortsWorkflowAndDisconnects(string projectName, string projectKind, MinimumSupportedServerPlugin minimumSupportedServerPlugin)
        {
            // Arrange
            var connectionInfo = new ConnectionInformation(new Uri("http://server"));
            var projects = new ProjectInformation[] { new ProjectInformation { Key = "project1" } };
            this.sonarQubeService.ReturnProjectInformation = projects;
            this.sonarQubeService.ClearServerPlugins();
            this.sonarQubeService.RegisterServerPlugin(new ServerPlugin { Key = minimumSupportedServerPlugin.Key, Version = minimumSupportedServerPlugin.MinimumVersion });
            this.projectSystemHelper.Projects = new[] { new ProjectMock(projectName) { ProjectKind = projectKind } };
            bool projectChangedCallbackCalled = false;
            this.host.TestStateManager.SetProjectsAction = (c, p) =>
            {
                projectChangedCallbackCalled = true;
                c.Should().Be(connectionInfo, "Unexpected connection");
                CollectionAssert.AreEqual(projects, p.ToArray(), "Unexpected projects");
            };

            var controller = new ConfigurableProgressController();
            var executionEvents = new ConfigurableProgressStepExecutionEvents();
            string connectionMessage = connectionInfo.ServerUri.ToString();
            var testSubject = new ConnectionWorkflow(this.host, new RelayCommand(AssertIfCalled));
            ConfigurableUserNotification notifications = (ConfigurableUserNotification)this.host.ActiveSection.UserNotifications;

            // Act
            testSubject.ConnectionStep(controller, CancellationToken.None, connectionInfo, executionEvents);

            // Assert
            controller.NumberOfAbortRequests.Should().Be(1);
            executionEvents.AssertProgressMessages(
                connectionInfo.ServerUri.ToString(),
                Strings.DetectingServerPlugins,
                Strings.ConnectionResultFailure);
            projectChangedCallbackCalled.Should().BeFalse("ConnectedProjectsCallaback was called");
            notifications.AssertNotification(NotificationIds.BadServerPluginId, string.Format(Strings.OnlySupportedPluginHasNoProjectInSolution, minimumSupportedServerPlugin.Language.Name));
        }

        [TestMethod]
        public void ConnectionWorkflow_ConnectionStep_UnsuccessfulConnection()
        {
            // Arrange
            var connectionInfo = new ConnectionInformation(new Uri("http://server"));
            bool projectChangedCallbackCalled = false;
            this.host.TestStateManager.SetProjectsAction = (c, p) =>
            {
                projectChangedCallbackCalled = true;
                c.Should().Be(connectionInfo, "Unexpected connection");
                p.Should().BeNull("Not expecting any projects");
            };
            this.projectSystemHelper.Projects = new[] { new ProjectMock("foo.csproj") { ProjectKind = ProjectSystemHelper.CSharpProjectKind } };
            var controller = new ConfigurableProgressController();
            this.sonarQubeService.AllowConnections = false;
            var executionEvents = new ConfigurableProgressStepExecutionEvents();
            string connectionMessage = connectionInfo.ServerUri.ToString();
            var testSubject = new ConnectionWorkflow(this.host, new RelayCommand(AssertIfCalled));

            // Act
            testSubject.ConnectionStep(controller, CancellationToken.None, connectionInfo, executionEvents);

            // Assert
            executionEvents.AssertProgressMessages(
                connectionMessage,
                Strings.DetectingServerPlugins,
                Strings.ConnectionResultFailure);
            projectChangedCallbackCalled.Should().BeFalse("Callback should not have been called");
            this.sonarQubeService.ConnectionRequestsCount.Should().Be(1);
            this.host.VisualStateManager.IsConnected.Should().BeFalse();
            ((ConfigurableUserNotification)this.host.ActiveSection.UserNotifications).AssertNotification(NotificationIds.FailedToConnectId, Strings.ConnectionFailed);

            // Act (reconnect with same bad connection)
            executionEvents.Reset();
            projectChangedCallbackCalled = false;
            testSubject.ConnectionStep(controller, CancellationToken.None, connectionInfo, executionEvents);

            // Assert
            executionEvents.AssertProgressMessages(
                connectionMessage,
                Strings.DetectingServerPlugins,
                Strings.ConnectionResultFailure);
            projectChangedCallbackCalled.Should().BeFalse("Callback should not have been called");
            this.sonarQubeService.ConnectionRequestsCount.Should().Be(2);
            this.host.VisualStateManager.IsConnected.Should().BeFalse();
            ((ConfigurableUserNotification)this.host.ActiveSection.UserNotifications).AssertNotification(NotificationIds.FailedToConnectId, Strings.ConnectionFailed);

            // Canceled connections
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            executionEvents.Reset();
            projectChangedCallbackCalled = false;
            CancellationToken token = tokenSource.Token;
            tokenSource.Cancel();

            // Act
            testSubject.ConnectionStep(controller, token, connectionInfo, executionEvents);

            // Assert
            executionEvents.AssertProgressMessages(
                connectionMessage,
                Strings.DetectingServerPlugins,
                Strings.ConnectionResultCancellation);
            projectChangedCallbackCalled.Should().BeFalse("Callback should not have been called");
            this.sonarQubeService.ConnectionRequestsCount.Should().Be(3);
            this.host.VisualStateManager.IsConnected.Should().BeFalse();
            ((ConfigurableUserNotification)this.host.ActiveSection.UserNotifications).AssertNotification(NotificationIds.FailedToConnectId, Strings.ConnectionFailed);
        }

        [TestMethod]
        public void ConnectionWorkflow_DownloadServiceParameters_RegexPropertyNotSet_SetsFilterWithDefaultExpression()
        {
            // Arrange
            var controller = new ConfigurableProgressController();
            var progressEvents = new ConfigurableProgressStepExecutionEvents();
            var expectedExpression = ServerProperty.TestProjectRegexDefaultValue;
            ConnectionWorkflow testSubject = SetTestSubjectWithConnectedServer();

            // Sanity
            this.sonarQubeService.ServerProperties.Any(x => x.Key != ServerProperty.TestProjectRegexKey).Should().BeFalse();

            // Act
            testSubject.DownloadServiceParameters(controller, CancellationToken.None, progressEvents);

            // Assert
            filter.AssertTestRegex(expectedExpression, RegexOptions.IgnoreCase);
            progressEvents.AssertProgressMessages(Strings.DownloadingServerSettingsProgessMessage);
        }

        [TestMethod]
        public void ConnectionWorkflow_DownloadServiceParameters_CustomRegexProperty_SetsFilterWithCorrectExpression()
        {
            // Arrange
            var controller = new ConfigurableProgressController();
            var progressEvents = new ConfigurableProgressStepExecutionEvents();

            var expectedExpression = ".*spoon.*";
            this.sonarQubeService.RegisterServerProperty(new ServerProperty
            {
                Key = ServerProperty.TestProjectRegexKey,
                Value = expectedExpression
            });

            ConnectionWorkflow testSubject = SetTestSubjectWithConnectedServer();

            // Act
            testSubject.DownloadServiceParameters(controller, CancellationToken.None, progressEvents);

            // Assert
            filter.AssertTestRegex(expectedExpression, RegexOptions.IgnoreCase);
            progressEvents.AssertProgressMessages(Strings.DownloadingServerSettingsProgessMessage);
        }

        [TestMethod]
        public void ConnectionWorkflow_DownloadServiceParameters_InvalidRegex_UsesDefault()
        {
            // Arrange
            var controller = new ConfigurableProgressController();
            var progressEvents = new ConfigurableProgressStepExecutionEvents();

            var badExpression = "*-gf/d*-b/try\\*-/r-*yeb/\\";
            var expectedExpression = ServerProperty.TestProjectRegexDefaultValue;
            this.sonarQubeService.RegisterServerProperty(new ServerProperty
            {
                Key = ServerProperty.TestProjectRegexKey,
                Value = badExpression
            });

            ConnectionWorkflow testSubject = SetTestSubjectWithConnectedServer();

            // Act
            testSubject.DownloadServiceParameters(controller, CancellationToken.None, progressEvents);

            // Assert
            filter.AssertTestRegex(expectedExpression, RegexOptions.IgnoreCase);
            progressEvents.AssertProgressMessages(Strings.DownloadingServerSettingsProgessMessage);
            this.outputWindowPane.AssertOutputStrings(string.Format(CultureInfo.CurrentCulture, Strings.InvalidTestProjectRegexPattern, badExpression));
        }

        [TestMethod]
        public void ConnectionWorkflow_DownloadServiceParameters_Cancelled_AbortsWorkflow()
        {
            // Arrange
            var controller = new ConfigurableProgressController();
            var progressEvents = new ConfigurableProgressStepExecutionEvents();

            ConnectionWorkflow testSubject = SetTestSubjectWithConnectedServer();

            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            testSubject.DownloadServiceParameters(controller, cts.Token, progressEvents);

            // Assert
            progressEvents.AssertProgressMessages(Strings.DownloadingServerSettingsProgessMessage);
            controller.NumberOfAbortRequests.Should().Be(1);
        }

        #endregion Tests

        #region Helpers

        private static void AssertIfCalled()
        {
            FluentAssertions.Execution.Execute.Assertion.FailWith("Command not expected to be called");
        }

        private ConnectionWorkflow SetTestSubjectWithConnectedServer()
        {
            ConnectionWorkflow testSubject = new ConnectionWorkflow(this.host, new RelayCommand(AssertIfCalled));
            var connectionInfo = new ConnectionInformation(new Uri("http://server"));
            testSubject.ConnectedServer = connectionInfo;
            return testSubject;
        }

        #endregion Helpers
    }
}