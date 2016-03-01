//-----------------------------------------------------------------------
// <copyright file="BindingWorkflow.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Alm.Authentication;
using Microsoft.VisualStudio.CodeAnalysis.RuleSets;
using SonarLint.VisualStudio.Integration.Persistence;
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;

namespace SonarLint.VisualStudio.Integration.Binding
{
    /// <summary>
    /// Workflow execution for the bind command
    /// </summary>
    internal class BindingWorkflow
    {
        private readonly BindingController owner;
        private readonly ProjectInformation project;
        private readonly IProjectSystemHelper projectSystemHelper;
        private readonly SolutionRuleSetWriter solutionRuleSetWriter;
        private readonly ProjectRuleSetWriter projectRuleSetWriter;

        internal readonly Dictionary<string, RuleSetGroup> LanguageToGroupMapping = new Dictionary<string, RuleSetGroup>
        {
            {SonarQubeServiceWrapper.CSharpLanguage, RuleSetGroup.CSharp },
            {SonarQubeServiceWrapper.VBLanguage, RuleSetGroup.VB }
        };

        public BindingWorkflow(BindingController owner, ProjectInformation project, SolutionRuleSetWriter solutionRuleSetWriter = null, ProjectRuleSetWriter projectRuleSetWriter = null, IProjectSystemHelper projectSystemHelper = null)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            this.owner = owner;
            this.project = project;
            this.projectSystemHelper = projectSystemHelper ?? new ProjectSystemHelper(this.owner.ServiceProvider);
            this.solutionRuleSetWriter = solutionRuleSetWriter ?? new SolutionRuleSetWriter(this.project);
            this.projectRuleSetWriter = projectRuleSetWriter ?? new ProjectRuleSetWriter();
        }

        #region Workflow state

        public Dictionary<RuleSetGroup, RuleSet> Rulesets
        {
            get;
        } = new Dictionary<RuleSetGroup, RuleSet>();

        public List<NuGetPackageInfo> NuGetPackages
        {
            get;
        } = new List<NuGetPackageInfo>();

        public Dictionary<RuleSetGroup, string> SolutionRulesetPaths
        {
            get;
        } = new Dictionary<RuleSetGroup, string>();

        internal RuleSetInjection.RuleSetInjector RuleSetInjector
        {
            get;
            private set;
        }

        #endregion

        #region Workflow startup

        public IProgressEvents Run()
        {
            this.owner.UserNotification.HideNotification(NotificationIds.FailedToBindId);

            List<string> languages = new List<string>();
            if (this.projectSystemHelper.GetSolutionManagedProjects().Any(p => ProjectSystemHelper.IsCSharpProject(p)))
            {
                languages.Add(SonarQubeServiceWrapper.CSharpLanguage);
            }

            if (this.projectSystemHelper.GetSolutionManagedProjects().Any(p => ProjectSystemHelper.IsVBProject(p)))
            {
                languages.Add(SonarQubeServiceWrapper.VBLanguage);
            }

            Debug.Assert(languages.Count > 0, "Expecting managed projects in solution");

            IProgressEvents progress = ProgressStepRunner.StartAsync(this.owner.ServiceProvider,
                this.owner.ProgressControlHost,
                (controller) => this.CreateWorkflowSteps(controller, languages));

            this.DebugOnly_MonitorProgress(progress);

            return progress;
        }

        [Conditional("DEBUG")]
        private void DebugOnly_MonitorProgress(IProgressEvents progress)
        {
            progress.RunOnFinished(r => VsShellUtils.WriteToGeneralOutputPane(this.owner.ServiceProvider, "DEBUGONLY: Binding workflow finished, Execution result: {0}", r));
        }

        private ProgressStepDefinition[] CreateWorkflowSteps(IProgressController controller, IEnumerable<string> languages)
        {
            StepAttributes IndeterminateNonCancellableUIStep = StepAttributes.Indeterminate | StepAttributes.NonCancellable;
            StepAttributes HiddenNonImpactingBackgroundStep = StepAttributes.BackgroundThread | StepAttributes.Hidden | StepAttributes.NoProgressImpact;

            return new ProgressStepDefinition[]
            {
                new ProgressStepDefinition(null, HiddenNonImpactingBackgroundStep,
                        (token, notifications) => notifications.ProgressChanged(Strings.StartedSolutionBindingWorkflow, double.NaN)),

                new ProgressStepDefinition(null, HiddenNonImpactingBackgroundStep,
                        (token, notifications) => this.VerifyServerPlugins(controller, token, notifications)),

                new ProgressStepDefinition(Strings.BindingProjectsDisplayMessage, StepAttributes.BackgroundThread,
                        (token, notifications) => this.DownloadRuleSet(controller, token, notifications, languages)),

                new ProgressStepDefinition(null, IndeterminateNonCancellableUIStep,
                        (token, notifications) => { NuGetHelper.LoadService(this.owner.ServiceProvider); /*The service needs to be loaded on UI thread*/ }),

                new ProgressStepDefinition(Strings.BindingProjectsDisplayMessage, StepAttributes.BackgroundThread,
                        (token, notifications) => this.InstallPackages(controller, token, notifications)),

                new ProgressStepDefinition(Strings.BindingProjectsDisplayMessage, IndeterminateNonCancellableUIStep,
                        (token, notifications) => this.PrepareRuleSetInjector(controller, notifications)),

                new ProgressStepDefinition(Strings.BindingProjectsDisplayMessage, StepAttributes.BackgroundThread | StepAttributes.Indeterminate,
                        (token, notifications) => this.PrepareRuleSets(controller, token, notifications)),

                new ProgressStepDefinition(null, IndeterminateNonCancellableUIStep | StepAttributes.Hidden | StepAttributes.NoProgressImpact,
                        (token, notifications) => this.FinishBindingOnUIThread(controller, notifications)),

                new ProgressStepDefinition(null, HiddenNonImpactingBackgroundStep,
                        (token, notifications) => notifications.ProgressChanged(Strings.FinishedSolutionBindingWorkflow, double.NaN))
            };
        }

        #endregion

        #region Workflow steps

        internal /*for testing purposes*/ void VerifyServerPlugins(IProgressController controller, CancellationToken token, IProgressStepExecutionEvents notifications)
        {
            var csPluginVersion = this.owner.SonarQubeService.GetPluginVersion(ServerPlugin.CSharpPluginKey, token);
            if (string.IsNullOrWhiteSpace(csPluginVersion) || VersionHelper.Compare(csPluginVersion, ServerPlugin.CSharpPluginMinimumVersion) < 0)
            {
                string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.ServerDoesNotHaveCorrectVersionOfCSharpPlugin, ServerPlugin.CSharpPluginMinimumVersion);
                VsShellUtils.WriteToGeneralOutputPane(this.owner.ServiceProvider, errorMessage);
                bool aborted = controller.TryAbort();
                Debug.Assert(aborted || token.IsCancellationRequested, "Failed to abort the workflow");
            }
        }

        internal /*for testing purposes*/ void DownloadRuleSet(IProgressController controller, CancellationToken cancellationToken, IProgressStepExecutionEvents notificationEvents, IEnumerable<string> languages)
        {
            Debug.Assert(controller != null);
            Debug.Assert(notificationEvents != null);

            bool failed = false;
            Dictionary<string, RuleSet> rulesets = new Dictionary<string, RuleSet>();
            DeterminateStepProgressNotifier notifier = new DeterminateStepProgressNotifier(notificationEvents, languages.Count());

            foreach (var language in languages)
            {
                notifier.NotifyCurrentProgress(string.Format(CultureInfo.CurrentCulture, Strings.DownloadingRulesProgressMessage, language));

                var export = this.owner.SonarQubeService.GetExportProfile(this.project, language, cancellationToken);

                if (export == null)
                {
                    failed = true;
                    break;
                }

                this.NuGetPackages.AddRange(export.Deployment.NuGetPackages);

                var tempRuleSetFilePath = Path.GetTempFileName();
                File.WriteAllText(tempRuleSetFilePath, export.Configuration.RuleSet.OuterXml);
                RuleSet ruleSet = RuleSet.LoadFromFile(tempRuleSetFilePath);

                rulesets[language] = ruleSet;
                notifier.NotifyIncrementedProgress(string.Empty);
                if (rulesets[language] == null)
                {
                    failed = true;
                    break;
                }
            }

            if (failed)
            {
                VsShellUtils.WriteToGeneralOutputPane(this.owner.ServiceProvider, Strings.RuleSetDownloadFailedMessage);
                bool aborted = controller.TryAbort();
                Debug.Assert(aborted || cancellationToken.IsCancellationRequested, "Failed to abort the workflow");
            }
            else
            {
                // Set the rule set which should be available for the following steps
                foreach(var keyValue in rulesets)
                {
                    this.Rulesets[this.LanguageToGroup(keyValue.Key)] = keyValue.Value;
                }

                notifier.NotifyCurrentProgress(Strings.RuleSetDownloadedSuccessfulMessage);
            }
        }

        private RuleSetGroup LanguageToGroup(string language)
        {
            RuleSetGroup group;
            if (!this.LanguageToGroupMapping.TryGetValue(language, out group))
            {
                Debug.Fail("Unsupported language: " + language);
                throw new InvalidOperationException();
            }
            return group;
        }

        private void PrepareRuleSetInjector(IProgressController controller, IProgressStepExecutionEvents notificationEvents)
        {
            Debug.Assert(System.Windows.Application.Current?.Dispatcher.CheckAccess() ?? false, "Expected to run on UI thread");

            notificationEvents.ProgressChanged(Strings.RuleSetGenerationProgressMessage, double.NaN);
            this.RuleSetInjector = new RuleSetInjection.RuleSetInjector(
                this.projectSystemHelper,
                this.SetSolutionRuleSet,
                this.UpdateProjectRuleSet);
        }

        private void PrepareRuleSets(IProgressController controller, CancellationToken token, IProgressStepExecutionEvents notificationEvents)
        {
            this.RuleSetInjector.PrepareUpdates(token);
        }

        private void FinishBindingOnUIThread(IProgressController controller, IProgressStepExecutionEvents notificationEvents)
        {
            Debug.Assert(System.Windows.Application.Current?.Dispatcher.CheckAccess() ?? false, "Expected to run on UI thread");

            this.RuleSetInjector.CommitUpdates();

            this.PersistBinding();
        }

        /// <summary>
        /// Will persist the binding information for next time usage
        /// </summary>
        internal /*for testing purposes*/ void PersistBinding(ICredentialStore credentialStore = null, IProjectSystemHelper projectSystemHelper = null)
        {
            Debug.Assert(this.owner.SonarQubeService.CurrentConnection != null, "Connection expected");
            ConnectionInformation connection = this.owner.SonarQubeService.CurrentConnection;

            BasicAuthCredentials credentials = connection.UserName == null? null : new BasicAuthCredentials(connection.UserName, connection.Password);

            SolutionBinding binding = new SolutionBinding(this.owner.ServiceProvider, credentialStore, projectSystemHelper);
            binding.WriteSolutionBinding(new BoundSonarQubeProject(connection.ServerUri, this.project.Key, credentials));
        }

        /// <summary>
        /// Will install the NuGet packages for the current managed projects.
        /// The packages that will be installed will be based on the information from <see cref="Analyzer.GetRequiredNuGetPackages"/> 
        /// and is specific to the <see cref="RuleSet"/>.
        /// </summary>
        internal /*for testing purposes*/ void InstallPackages(IProgressController controller, CancellationToken token, IProgressStepExecutionEvents notificationEvents)
        {
            if (!this.NuGetPackages.Any())
            {
                return;
            }

            Debug.Assert(this.NuGetPackages.Count == this.NuGetPackages.Distinct().Count(), "Duplicate NuGet packages specified");

            var managedProjects = this.projectSystemHelper.GetSolutionManagedProjects().ToArray();
            if (!managedProjects.Any())
            {
                Debug.Fail("Not expected to be called when there are no managed projects");
                return;
            }

            DeterminateStepProgressNotifier progressNotifier = new DeterminateStepProgressNotifier(notificationEvents, managedProjects.Length * this.NuGetPackages.Count);
            foreach (var project in managedProjects)
            {
                foreach (var packageInfo in this.NuGetPackages)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    string message = string.Format(CultureInfo.CurrentCulture, Strings.EnsuringNugetPackagesProgressMessage, packageInfo.Id, project.Name);
                    progressNotifier.NotifyCurrentProgress(message);
                    if (!NuGetHelper.TryInstallPackage(this.owner.ServiceProvider, project, packageInfo.Id, packageInfo.Version))
                    {
                        bool aborted = controller.TryAbort();
                        Debug.Assert(aborted, "Failed to abort the binding workflow");
                        break;
                    }
                    progressNotifier.NotifyIncrementedProgress(string.Empty);
                }
            }
        }

        internal /*for testing purposes*/ string SetSolutionRuleSet(RuleSetGroup group, string solutionFullPath)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(solutionFullPath), "Expecting a solution file path");
            Debug.Assert(this.Rulesets.ContainsKey(group) && this.Rulesets[group] != null, $"Rule set should have been stashed by previous step ({nameof(DownloadRuleSet)})");

            RuleSet ruleset;
            string path = null;
            if (this.Rulesets.TryGetValue(group, out ruleset) && ruleset!=null)
            {
                path = this.solutionRuleSetWriter.WriteSolutionLevelRuleSet(solutionFullPath, ruleset, fileNameSuffix: group.ToString());
                this.SolutionRulesetPaths[group] = path;
            }

            return path;
        }

        internal /*for testing purposes*/ string UpdateProjectRuleSet(RuleSetGroup group, string projectFullPath, string configurationName, string currentRuleSet)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(projectFullPath), "Expecting a project full path");
            Debug.Assert(this.SolutionRulesetPaths.ContainsKey(group) && this.SolutionRulesetPaths[group] != null, $"Rule set should have been stashed by previous step ({nameof(DownloadRuleSet)})");

            string solutionRuleSetPath = null;
            string projectRuleSetPath = null;
            if (this.SolutionRulesetPaths.TryGetValue(group, out solutionRuleSetPath))
            {
                projectRuleSetPath = this.projectRuleSetWriter.WriteProjectLevelRuleSet(projectFullPath, configurationName, solutionRuleSetPath, currentRuleSet);
            }

            return projectRuleSetPath;
        }

        #endregion
    }
}
