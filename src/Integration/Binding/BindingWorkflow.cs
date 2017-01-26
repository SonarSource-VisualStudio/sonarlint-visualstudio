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

using EnvDTE;
using Microsoft.VisualStudio.CodeAnalysis.RuleSets;
using Microsoft.VisualStudio.Shell;
using SonarLint.VisualStudio.Integration.Progress;
using SonarLint.VisualStudio.Integration.Resources;
using SonarLint.VisualStudio.Integration.Service;
using SonarLint.VisualStudio.Progress.Controller;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SonarLint.VisualStudio.Integration.Binding
{
    /// <summary>
    /// Workflow execution for the bind command
    /// </summary>
    internal class BindingWorkflow
    {
        private readonly IHost host;
        private readonly ConnectionInformation connectionInformation;
        private readonly ProjectInformation project;
        private readonly IProjectSystemHelper projectSystem;
        private readonly SolutionBindingOperation solutionBindingOperation;

        public BindingWorkflow(IHost host, ConnectionInformation connectionInformation, ProjectInformation project)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (connectionInformation == null)
            {
                throw new ArgumentNullException(nameof(connectionInformation));
            }

            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            this.host = host;
            this.connectionInformation = connectionInformation;
            this.project = project;
            this.projectSystem = this.host.GetService<IProjectSystemHelper>();
            this.projectSystem.AssertLocalServiceIsNotNull();

            this.solutionBindingOperation = new SolutionBindingOperation(
                    this.host,
                    this.connectionInformation,
                    this.project.Key);
        }

        #region Workflow state

        public ISet<Project> BindingProjects
        {
            get;
        } = new HashSet<Project>();

        public Dictionary<Language, RuleSet> Rulesets
        {
            get;
        } = new Dictionary<Language, RuleSet>();

        public Dictionary<Language, List<NuGetPackageInfo>> NuGetPackages
        {
            get;
        } = new Dictionary<Language, List<NuGetPackageInfo>>();

        public Dictionary<Language, string> SolutionRulesetPaths
        {
            get;
        } = new Dictionary<Language, string>();

        public Dictionary<Language, QualityProfile> QualityProfiles
        {
            get;
        } = new Dictionary<Language, QualityProfile>();

        internal /*for testing purposes*/ bool AllNuGetPackagesInstalled
        {
            get;
            set;
        } = true;

        #endregion

        #region Workflow startup

        public IProgressEvents Run()
        {
            Debug.Assert(this.host.ActiveSection != null, "Expect the section to be attached at least until this method returns");
            Debug.Assert(this.projectSystem.GetSolutionProjects().Any(), "Expecting projects in solution");

            IProgressEvents progress = ProgressStepRunner.StartAsync(this.host,
                this.host.ActiveSection.ProgressHost,
                controller => this.CreateWorkflowSteps(controller));

            this.DebugOnly_MonitorProgress(progress);

            return progress;
        }

        [Conditional("DEBUG")]
        private void DebugOnly_MonitorProgress(IProgressEvents progress)
        {
            progress.RunOnFinished(r => VsShellUtils.WriteToSonarLintOutputPane(this.host, "DEBUGONLY: Binding workflow finished, Execution result: {0}", r));
        }

        private ProgressStepDefinition[] CreateWorkflowSteps(IProgressController controller)
        {
            StepAttributes IndeterminateNonCancellableUIStep = StepAttributes.Indeterminate | StepAttributes.NonCancellable;
            StepAttributes HiddenIndeterminateNonImpactingNonCancellableUIStep = IndeterminateNonCancellableUIStep | StepAttributes.Hidden | StepAttributes.NoProgressImpact;
            StepAttributes HiddenNonImpactingBackgroundStep = StepAttributes.BackgroundThread | StepAttributes.Hidden | StepAttributes.NoProgressImpact;

            return new ProgressStepDefinition[]
            {
                new ProgressStepDefinition(null, HiddenNonImpactingBackgroundStep,
                        (token, notifications) => notifications.ProgressChanged(Strings.StartedSolutionBindingWorkflow)),

                new ProgressStepDefinition(null, StepAttributes.Indeterminate | StepAttributes.Hidden,
                        (token, notifications) => this.PromptSaveSolutionIfDirty(controller, token)),

                new ProgressStepDefinition(Strings.BindingProjectsDisplayMessage, StepAttributes.Indeterminate,
                        (token, notifications) => this.DiscoverProjects(controller, notifications)),

                new ProgressStepDefinition(Strings.BindingProjectsDisplayMessage, StepAttributes.BackgroundThread,
                        (token, notifications) => this.DownloadQualityProfile(controller, token, notifications, this.GetBindingLanguages())),

                new ProgressStepDefinition(null, HiddenIndeterminateNonImpactingNonCancellableUIStep,
                        (token, notifications) => { NuGetHelper.LoadService(this.host); /*The service needs to be loaded on UI thread*/ }),

                new ProgressStepDefinition(Strings.BindingProjectsDisplayMessage, StepAttributes.BackgroundThread,
                        (token, notifications) => this.InstallPackages(controller, token, notifications)),

                new ProgressStepDefinition(Strings.BindingProjectsDisplayMessage, IndeterminateNonCancellableUIStep,
                        (token, notifications) => this.InitializeSolutionBindingOnUIThread(notifications)),

                new ProgressStepDefinition(Strings.BindingProjectsDisplayMessage, StepAttributes.BackgroundThread | StepAttributes.Indeterminate,
                        (token, notifications) => this.PrepareSolutionBinding(token)),

                new ProgressStepDefinition(null, StepAttributes.Hidden | StepAttributes.Indeterminate,
                        (token, notifications) => this.FinishSolutionBindingOnUIThread(controller, token)),

                new ProgressStepDefinition(null, HiddenIndeterminateNonImpactingNonCancellableUIStep,
                        (token, notifications) => this.SilentSaveSolutionIfDirty()),

                new ProgressStepDefinition(null, HiddenNonImpactingBackgroundStep,
                        (token, notifications) => this.EmitBindingCompleteMessage(notifications))
            };
        }

        #endregion

        #region Workflow steps

        internal /*for testing purposes*/ void PromptSaveSolutionIfDirty(IProgressController controller, CancellationToken token)
        {
            if (!VsShellUtils.SaveSolution(this.host, silent: false))
            {
                VsShellUtils.WriteToSonarLintOutputPane(this.host, Strings.SolutionSaveCancelledBindAborted);

                this.AbortWorkflow(controller, token);
            }
        }

        internal /*for testing purposes*/ void DiscoverProjects(IProgressController controller, IProgressStepExecutionEvents notifications)
        {
            Debug.Assert(ThreadHelper.CheckAccess(), "Expected step to be run on the UI thread");

            notifications.ProgressChanged(Strings.DiscoveringSolutionProjectsProgressMessage);

            var patternFilteredProjects = this.projectSystem.GetFilteredSolutionProjects();
            var pluginAndPatternFilteredProjects =
                patternFilteredProjects.Where(p => this.host.SupportedPluginLanguages.Contains(Language.ForProject(p)));

            this.BindingProjects.UnionWith(pluginAndPatternFilteredProjects);
            this.InformAboutFilteredOutProjects();

            if (!this.BindingProjects.Any())
            {
                AbortWorkflow(controller, CancellationToken.None);
            }
        }

        internal /*for testing purposes*/ void DownloadQualityProfile(IProgressController controller, CancellationToken cancellationToken, IProgressStepExecutionEvents notificationEvents, IEnumerable<Language> languages)
        {
            Debug.Assert(controller != null);
            Debug.Assert(notificationEvents != null);

            bool failed = false;
            var rulesets = new Dictionary<Language, RuleSet>();
            var languageList = languages as IList<Language> ?? languages.ToList();
            DeterminateStepProgressNotifier notifier = new DeterminateStepProgressNotifier(notificationEvents, languageList.Count);

            notifier.NotifyCurrentProgress(Strings.DownloadingQualityProfileProgressMessage);
            foreach (var language in languageList)
            {
                QualityProfile qualityProfileInfo;
                if (!host.SonarQubeService.TryGetQualityProfile(this.connectionInformation, this.project, language, cancellationToken, out qualityProfileInfo))
                {
                    failed = true;
                    InformAboutQualityProfileToDownload(qualityProfileInfo, language.Name, true);
                    break;
                }
                this.QualityProfiles[language] = qualityProfileInfo;

                RoslynExportProfile export;
                if (!this.host.SonarQubeService.TryGetExportProfile(this.connectionInformation, qualityProfileInfo, language, cancellationToken, out export))
                {
                    failed = true;
                    InformAboutQualityProfileToDownload(qualityProfileInfo, language.Name, true);
                    break;
                }
                this.NuGetPackages.Add(language, export.Deployment.NuGetPackages);

                var tempRuleSetFilePath = Path.GetTempFileName();
                File.WriteAllText(tempRuleSetFilePath, export.Configuration.RuleSet.OuterXml);
                RuleSet ruleSet = RuleSet.LoadFromFile(tempRuleSetFilePath);

                // Remove/Move/Refactor code when XML ruleset file is no longer downloaded but the proper API is used to retrieve rules
                UpdateDownloadedSonarQubeQualityProfile(ruleSet, qualityProfileInfo);

                rulesets[language] = ruleSet;
                notifier.NotifyIncrementedProgress(string.Empty);
                if (rulesets[language] == null)
                {
                    failed = true;
                    InformAboutQualityProfileToDownload(qualityProfileInfo, language.Name, true);
                    break;
                }

                InformAboutQualityProfileToDownload(qualityProfileInfo, language.Name, false);
            }

            if (failed)
            {
                this.AbortWorkflow(controller, cancellationToken);
                return;
            }

            // Set the rule set which should be available for the following steps
            foreach (var keyValue in rulesets)
            {
                this.Rulesets[keyValue.Key] = keyValue.Value;
            }
        }

        private void UpdateDownloadedSonarQubeQualityProfile(RuleSet ruleSet, QualityProfile qualityProfile)
        {
            ruleSet.NonLocalizedDisplayName = string.Format(Strings.SonarQubeRuleSetNameFormat, this.project.Name, qualityProfile.Name);

            var ruleSetDescriptionBuilder = new StringBuilder();
            ruleSetDescriptionBuilder.AppendLine(ruleSet.Description);
            ruleSetDescriptionBuilder.AppendFormat(Strings.SonarQubeQualityProfilePageUrlFormat, this.connectionInformation.ServerUri, qualityProfile.Key);
            ruleSet.NonLocalizedDescription = ruleSetDescriptionBuilder.ToString();

            ruleSet.WriteToFile(ruleSet.FilePath);
        }

        private void InitializeSolutionBindingOnUIThread(IProgressStepExecutionEvents notificationEvents)
        {
            Debug.Assert(System.Windows.Application.Current?.Dispatcher.CheckAccess() ?? false, "Expected to run on UI thread");

            notificationEvents.ProgressChanged(Strings.RuleSetGenerationProgressMessage);

            this.solutionBindingOperation.RegisterKnownRuleSets(this.Rulesets);
            this.solutionBindingOperation.Initialize(this.BindingProjects, this.QualityProfiles);
        }

        private void PrepareSolutionBinding(CancellationToken token)
        {
            this.solutionBindingOperation.Prepare(token);
        }

        private void FinishSolutionBindingOnUIThread(IProgressController controller, CancellationToken token)
        {
            Debug.Assert(System.Windows.Application.Current?.Dispatcher.CheckAccess() ?? false, "Expected to run on UI thread");

            if (!this.solutionBindingOperation.CommitSolutionBinding())
            {
                AbortWorkflow(controller, token);
            }
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

            if (!this.BindingProjects.Any())
            {
                Debug.Fail("Not expected to be called when there are no projects");
                return;
            }

            var projectNugets = this.BindingProjects
                .SelectMany(bindingProject =>
                {
                    var projectLanguage = Language.ForProject(bindingProject);

                    List<NuGetPackageInfo> nugetPackages;
                    if (!this.NuGetPackages.TryGetValue(projectLanguage, out nugetPackages))
                    {
                        var message = string.Format(Strings.BindingProjectLanguageNotMatchingAnyQualityProfileLanguage, bindingProject.Name);
                        VsShellUtils.WriteToSonarLintOutputPane(this.host, Strings.SubTextPaddingFormat, message);
                        nugetPackages = new List<NuGetPackageInfo>();
                    }

                    return nugetPackages.Select(nugetPackage => new { Project = bindingProject, NugetPackage = nugetPackage });
                })
                .ToArray();

            DeterminateStepProgressNotifier progressNotifier = new DeterminateStepProgressNotifier(notificationEvents, projectNugets.Length);
            foreach (var projectNuget in projectNugets)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                string message = string.Format(CultureInfo.CurrentCulture, Strings.EnsuringNugetPackagesProgressMessage, projectNuget.NugetPackage.Id, projectNuget.Project.Name);
                VsShellUtils.WriteToSonarLintOutputPane(this.host, Strings.SubTextPaddingFormat, message);

                var isNugetInstallSuccessful = NuGetHelper.TryInstallPackage(this.host, projectNuget.Project, projectNuget.NugetPackage.Id, projectNuget.NugetPackage.Version);

                if (isNugetInstallSuccessful) // NuGetHelper.TryInstallPackage already displayed the error message so only take care of the success message
                {
                    message = string.Format(CultureInfo.CurrentCulture, Strings.SuccessfullyInstalledNugetPackageForProject, projectNuget.NugetPackage.Id, projectNuget.Project.Name);
                    VsShellUtils.WriteToSonarLintOutputPane(this.host, Strings.SubTextPaddingFormat, message);
                }

                // TODO: SVS-33 (https://jira.sonarsource.com/browse/SVS-33) Trigger a Team Explorer warning notification to investigate the partial binding in the output window.
                this.AllNuGetPackagesInstalled &= isNugetInstallSuccessful;

                progressNotifier.NotifyIncrementedProgress(string.Empty);
            }
        }

        internal /*for testing purposes*/ void SilentSaveSolutionIfDirty()
        {
            bool saved = VsShellUtils.SaveSolution(this.host, silent: true);
            Debug.Assert(saved, "Should not be cancellable");
        }

        internal /*for testing purposes*/ void EmitBindingCompleteMessage(IProgressStepExecutionEvents notifications)
        {
            var message = this.AllNuGetPackagesInstalled
                ? Strings.FinishedSolutionBindingWorkflowSuccessful
                : Strings.FinishedSolutionBindingWorkflowNotAllPackagesInstalled;
            notifications.ProgressChanged(message);
        }

        #endregion

        #region Helpers

        private void AbortWorkflow(IProgressController controller, CancellationToken token)
        {
            bool aborted = controller.TryAbort();
            Debug.Assert(aborted || token.IsCancellationRequested, "Failed to abort the workflow");
        }

        internal /*testing purposes*/ IEnumerable<Language> GetBindingLanguages()
        {
            return this.BindingProjects.Select(Language.ForProject)
                                       .Distinct()
                                       .Where(this.host.SupportedPluginLanguages.Contains);
        }

        private void InformAboutFilteredOutProjects()
        {
            var includedProjects = this.BindingProjects.ToList();
            var excludedProjects = this.projectSystem.GetSolutionProjects().Except(this.BindingProjects).ToList();

            var output = new StringBuilder();

            output.AppendFormat(Strings.SubTextPaddingFormat, Strings.DiscoveringSolutionIncludedProjectsHeader).AppendLine();
            if (includedProjects.Count == 0)
            {
                var message = string.Format(Strings.DiscoveredIncludedOrExcludedProjectFormat, Strings.NoProjectsApplicableForBinding);
                output.AppendFormat(Strings.SubTextPaddingFormat, message).AppendLine();
            }
            else
            {
                includedProjects.ForEach(
                    p =>
                    {
                        var message = string.Format(Strings.DiscoveredIncludedOrExcludedProjectFormat, p.UniqueName);
                        output.AppendFormat(Strings.SubTextPaddingFormat, message).AppendLine();
                    });
            }

            output.AppendFormat(Strings.SubTextPaddingFormat, Strings.DiscoveringSolutionExcludedProjectsHeader).AppendLine();
            if (excludedProjects.Count == 0)
            {
                var message = string.Format(Strings.DiscoveredIncludedOrExcludedProjectFormat, Strings.NoProjectsExcludedFromBinding);
                output.AppendFormat(Strings.SubTextPaddingFormat, message).AppendLine();
            }
            else
            {
                excludedProjects.ForEach(
                    p =>
                    {
                        var message = string.Format(Strings.DiscoveredIncludedOrExcludedProjectFormat, p.UniqueName);
                        output.AppendFormat(Strings.SubTextPaddingFormat, message).AppendLine();
                    });
            }
            output.AppendFormat(Strings.SubTextPaddingFormat, Strings.FilteredOutProjectFromBindingEnding);

            VsShellUtils.WriteToSonarLintOutputPane(this.host, output.ToString());
        }

        private void InformAboutQualityProfileToDownload(QualityProfile profile, string languageName, bool isDownloadFailed)
        {
            var output =
                profile == null
                    ? string.Format(Strings.CannotDownloadQualityProfileForLanguage, languageName)
                    : isDownloadFailed
                        ? string.Format(Strings.QualityProfileDownloadFailedMessageFormat, profile.Name, profile.Key, languageName)
                        : string.Format(Strings.QualityProfileDownloadSuccessfulMessageFormat, profile.Name, profile.Key, languageName);
            VsShellUtils.WriteToSonarLintOutputPane(this.host, string.Format(Strings.SubTextPaddingFormat, output));
        }

        #endregion

    }
}
