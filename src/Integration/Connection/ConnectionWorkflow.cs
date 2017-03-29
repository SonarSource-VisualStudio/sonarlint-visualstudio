/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto: contact AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using SonarLint.VisualStudio.Integration.Progress;
using SonarLint.VisualStudio.Integration.Resources;
using SonarLint.VisualStudio.Integration.Service;
using SonarLint.VisualStudio.Integration.Service.DataModel;
using SonarLint.VisualStudio.Integration.TeamExplorer;
using SonarLint.VisualStudio.Progress.Controller;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Input;

namespace SonarLint.VisualStudio.Integration.Connection
{
    /// <summary>
    /// Workflow execution for the connect command.
    /// </summary>
    internal class ConnectionWorkflow
    {
        private readonly IHost host;
        private readonly ICommand parentCommand;
        private readonly IProjectSystemHelper projectSystem;

        public ConnectionWorkflow(IHost host, ICommand parentCommand)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (parentCommand == null)
            {
                throw new ArgumentNullException(nameof(parentCommand));
            }

            this.host = host;
            this.parentCommand = parentCommand;
            this.projectSystem = this.host.GetService<IProjectSystemHelper>();
            this.projectSystem.AssertLocalServiceIsNotNull();
        }

        internal /*for testing purposes*/ ConnectionInformation ConnectedServer
        {
            get;
            set;
        }

        #region Start the workflow
        public IProgressEvents Run(ConnectionInformation information)
        {
            Debug.Assert(this.host.ActiveSection != null, "Expect the section to be attached at least until this method returns");

            this.OnProjectsChanged(information, null);
            IProgressEvents progress = ProgressStepRunner.StartAsync(this.host, this.host.ActiveSection.ProgressHost, (controller) => this.CreateConnectionSteps(controller, information));
            this.DebugOnly_MonitorProgress(progress);
            return progress;
        }

        [Conditional("DEBUG")]
        private void DebugOnly_MonitorProgress(IProgressEvents progress)
        {
            progress.RunOnFinished(r => VsShellUtils.WriteToSonarLintOutputPane(this.host, "DEBUGONLY: Connect workflow finished, Execution result: {0}", r));
        }

        private ProgressStepDefinition[] CreateConnectionSteps(IProgressController controller, ConnectionInformation connection)
        {
            string connectStepDisplayText = string.Format(CultureInfo.CurrentCulture, Resources.Strings.ConnectingToSever, connection.ServerUri);
            return new[]
            {
                    new ProgressStepDefinition(connectStepDisplayText, StepAttributes.Indeterminate | StepAttributes.BackgroundThread,
                        (cancellationToken, notifications) => this.ConnectionStep(controller, cancellationToken, connection, notifications)),

                    new ProgressStepDefinition(connectStepDisplayText, StepAttributes.BackgroundThread,
                        (token, notifications) => this.DownloadServiceParameters(controller, token, notifications)),

                };
        }

        #endregion

        #region Workflow events

        private void OnProjectsChanged(ConnectionInformation connection, ProjectInformation[] projects)
        {
            this.host.VisualStateManager.SetProjects(connection, projects);
        }

        #endregion

        #region Workflow steps

        internal /* for testing purposes */ void ConnectionStep(IProgressController controller, CancellationToken cancellationToken, ConnectionInformation connection, IProgressStepExecutionEvents notifications)
        {
            this.host.ActiveSection?.UserNotifications?.HideNotification(NotificationIds.FailedToConnectId);
            this.host.ActiveSection?.UserNotifications?.HideNotification(NotificationIds.BadServerPluginId);

            notifications.ProgressChanged(connection.ServerUri.ToString());

            if (!this.AreSolutionProjectsAndServerPluginsCompatible(controller, cancellationToken, connection, notifications))
            {
                return;
            }

            this.ConnectedServer = connection;

            ProjectInformation[] projects;
            if (!this.host.SonarQubeService.TryGetProjects(connection, cancellationToken, out projects))
            {
                notifications.ProgressChanged(cancellationToken.IsCancellationRequested ? Strings.ConnectionResultCancellation : Strings.ConnectionResultFailure);
                this.host.ActiveSection?.UserNotifications?.ShowNotificationError(Strings.ConnectionFailed, NotificationIds.FailedToConnectId, this.parentCommand);

                AbortWorkflow(controller, cancellationToken);
                return;
            }

            this.OnProjectsChanged(connection, projects);
            notifications.ProgressChanged(Strings.ConnectionResultSuccess);
        }

        internal /*for testing purposes*/ void DownloadServiceParameters(IProgressController controller, CancellationToken token, IProgressStepExecutionEvents notifications)
        {
            Debug.Assert(this.ConnectedServer != null);

            // Should never realistically take more than 1 second to match against a project name
            var timeout = TimeSpan.FromSeconds(1);
            var defaultRegex = new Regex(ServerProperty.TestProjectRegexDefaultValue, RegexOptions.IgnoreCase, timeout);

            notifications.ProgressChanged(Strings.DownloadingServerSettingsProgessMessage);

            ServerProperty[] properties;
            if (!this.host.SonarQubeService.TryGetProperties(this.ConnectedServer, token, out properties) || token.IsCancellationRequested)
            {
                AbortWorkflow(controller, token);
                return;
            }

            var testProjRegexProperty = properties.FirstOrDefault(x => StringComparer.Ordinal.Equals(x.Key, ServerProperty.TestProjectRegexKey));

            // Using this older API, if the property hasn't been explicitly set no default value is returned.
            // http://jira.sonarsource.com/browse/SONAR-5891
            var testProjRegexPattern = testProjRegexProperty?.Value ?? ServerProperty.TestProjectRegexDefaultValue;

            Regex regex = null;
            if (testProjRegexPattern != null)
            {
                // Try and create regex from provided server pattern.
                // No way to determine a valid pattern other than attempting to construct
                // the Regex object.
                try
                {
                    regex = new Regex(testProjRegexPattern, RegexOptions.IgnoreCase, timeout);
                }
                catch (ArgumentException)
                {
                    VsShellUtils.WriteToSonarLintOutputPane(this.host, Strings.InvalidTestProjectRegexPattern, testProjRegexPattern);
                }
            }

            var projectFilter = this.host.GetService<IProjectSystemFilter>();
            projectFilter.AssertLocalServiceIsNotNull();
            projectFilter.SetTestRegex(regex ?? defaultRegex);
        }

        #endregion

        #region Helpers

        private bool AreSolutionProjectsAndServerPluginsCompatible(IProgressController controller, CancellationToken cancellationToken, ConnectionInformation connection, IProgressStepExecutionEvents notifications)
        {
            notifications.ProgressChanged(Strings.DetectingServerPlugins);

            ServerPlugin[] plugins;
            if (!this.host.SonarQubeService.TryGetPlugins(connection, cancellationToken, out plugins))
            {
                notifications.ProgressChanged(cancellationToken.IsCancellationRequested ? Strings.ConnectionResultCancellation : Strings.ConnectionResultFailure);
                this.host.ActiveSection?.UserNotifications?.ShowNotificationError(Strings.ConnectionFailed, NotificationIds.FailedToConnectId, this.parentCommand);

                AbortWorkflow(controller, cancellationToken);
                return false;
            }

            var csharpOrVbNetProjects = new HashSet<EnvDTE.Project>(this.projectSystem.GetSolutionProjects());
            this.host.SupportedPluginLanguages.UnionWith(new HashSet<Language>(MinimumSupportedServerPlugin.All
                                                                                                           .Where(lang => IsServerPluginSupported(plugins, lang))
                                                                                                           .Select(lang => lang.Language)));

            // If any of the project can be bound then return success
            if (csharpOrVbNetProjects.Select(Language.ForProject)
                                     .Any(this.host.SupportedPluginLanguages.Contains))
            {
                return true;
            }

            string errorMessage = GetPluginProjectMismatchErrorMessage(csharpOrVbNetProjects);
            this.host.ActiveSection?.UserNotifications?.ShowNotificationError(errorMessage, NotificationIds.BadServerPluginId, null);
            VsShellUtils.WriteToSonarLintOutputPane(this.host, Strings.SubTextPaddingFormat, errorMessage);
            notifications.ProgressChanged(Strings.ConnectionResultFailure);

            AbortWorkflow(controller, cancellationToken);
            return false;
        }

        private string GetPluginProjectMismatchErrorMessage(ISet<EnvDTE.Project> csharpOrVbNetProjects)
        {
            if (this.host.SupportedPluginLanguages.Count == 0)
            {
                return Strings.ServerHasNoSupportedPluginVersion;
            }

            if (csharpOrVbNetProjects.Count == 0)
            {
                return Strings.SolutionContainsNoSupportedProject;
            }

            return string.Format(Strings.OnlySupportedPluginHasNoProjectInSolution, this.host.SupportedPluginLanguages.First().Name);
        }

        private bool IsServerPluginSupported(ServerPlugin[] plugins, MinimumSupportedServerPlugin minimumSupportedPlugin)
        {
            var plugin = plugins.FirstOrDefault(x => StringComparer.Ordinal.Equals(x.Key, minimumSupportedPlugin.Key));
            var isPluginSupported = !string.IsNullOrWhiteSpace(plugin?.Version) && VersionHelper.Compare(plugin.Version, minimumSupportedPlugin.MinimumVersion) >= 0;

            var pluginSupportMessageFormat = string.Format(Strings.SubTextPaddingFormat, isPluginSupported ? Strings.SupportedPluginFoundMessage : Strings.UnsupportedPluginFoundMessage);
            VsShellUtils.WriteToSonarLintOutputPane(this.host, pluginSupportMessageFormat, minimumSupportedPlugin.ToString());

            return isPluginSupported;
        }

        private static void AbortWorkflow(IProgressController controller, CancellationToken token)
        {
            bool aborted = controller.TryAbort();
            Debug.Assert(aborted || token.IsCancellationRequested, "Failed to abort the workflow");
        }

        #endregion
    }
}
