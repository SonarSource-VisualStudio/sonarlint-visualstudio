﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using SonarLint.VisualStudio.Core.Binding;
using SonarLint.VisualStudio.Integration.Helpers;
using SonarLint.VisualStudio.Integration.Resources;
using SonarQube.Client.Models;
using Language = SonarLint.VisualStudio.Core.Language;

namespace SonarLint.VisualStudio.Integration.Binding
{
    internal class BindingProcessImpl : IBindingProcess
    {
        private readonly IHost host;
        private readonly BindCommandArgs bindingArgs;
        private readonly IProjectSystemHelper projectSystem;
        private readonly ISolutionBindingOperation solutionBindingOperation;
        private readonly ISolutionBindingInformationProvider bindingInformationProvider;
        private readonly IRulesConfigurationProvider rulesConfigurationProvider;

        internal /*for testing*/ INuGetBindingOperation NuGetBindingOperation { get; }

        public BindingProcessImpl(IHost host,
            BindCommandArgs bindingArgs,
            ISolutionBindingOperation solutionBindingOperation,
            INuGetBindingOperation nugetBindingOperation,
            ISolutionBindingInformationProvider bindingInformationProvider,
            IRulesConfigurationProvider rulesConfigurationProvider,
            bool isFirstBinding = false)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.bindingArgs = bindingArgs ?? throw new ArgumentNullException(nameof(bindingArgs));
            this.solutionBindingOperation = solutionBindingOperation ?? throw new ArgumentNullException(nameof(solutionBindingOperation));
            this.NuGetBindingOperation = nugetBindingOperation ?? throw new ArgumentNullException(nameof(nugetBindingOperation));
            this.bindingInformationProvider = bindingInformationProvider ?? throw new ArgumentNullException(nameof(bindingInformationProvider));
            this.rulesConfigurationProvider = rulesConfigurationProvider ?? throw new ArgumentNullException(nameof(rulesConfigurationProvider));

            Debug.Assert(bindingArgs.ProjectKey != null);
            Debug.Assert(bindingArgs.ProjectName != null);
            Debug.Assert(bindingArgs.Connection != null);

            this.projectSystem = this.host.GetService<IProjectSystemHelper>();
            this.projectSystem.AssertLocalServiceIsNotNull();

            this.InternalState = new BindingProcessState(isFirstBinding);
        }

        // This property should not be used by product code
        internal /* for testing */ BindingProcessState InternalState { get;  }

        #region IBindingTemplate methods

        public bool PromptSaveSolutionIfDirty()
        {
            var result = VsShellUtils.SaveSolution(this.host, silent: false);
            if (!result)
            {
                this.host.Logger.WriteLine(Strings.SolutionSaveCancelledBindAborted);
            }
            return result;
        }

        public bool DiscoverProjects()
        {
            var patternFilteredProjects = this.projectSystem.GetFilteredSolutionProjects();
            var pluginAndPatternFilteredProjects =
                patternFilteredProjects.Where(p => this.host.SupportedPluginLanguages.Contains(ProjectToLanguageMapper.GetLanguageForProject(p)));

            this.InternalState.BindingProjects.UnionWith(pluginAndPatternFilteredProjects);
            this.InformAboutFilteredOutProjects();

            return this.InternalState.BindingProjects.Any();
        }

        public async Task<bool> DownloadQualityProfileAsync(IProgress<FixedStepsProgress> progress, CancellationToken cancellationToken)
        {
            var languageList = this.GetBindingLanguages();

            var languageCount = languageList.Count();
            int currentLanguage = 0;
            progress?.Report(new FixedStepsProgress(Strings.DownloadingQualityProfileProgressMessage, currentLanguage, languageCount));

            foreach (var language in languageList)
            {
                var serverLanguage = language.ToServerLanguage();

                // Download the quality profile for each language
                var qualityProfileInfo = await Core.WebServiceHelper.SafeServiceCallAsync(() =>
                    this.host.SonarQubeService.GetQualityProfileAsync(
                        this.bindingArgs.ProjectKey, this.bindingArgs.Connection.Organization?.Key, serverLanguage, cancellationToken),
                    this.host.Logger);
                if (qualityProfileInfo == null)
                {
                    this.host.Logger.WriteLine(string.Format(Strings.SubTextPaddingFormat,
                       string.Format(Strings.CannotDownloadQualityProfileForLanguage, language.Name)));
                    return false;
                }
                this.InternalState.QualityProfiles[language] = qualityProfileInfo;

                // Create the rules configuration for the language
                var rulesConfig = await this.rulesConfigurationProvider.GetRulesConfigurationAsync(qualityProfileInfo, this.bindingArgs.Connection.Organization?.Key, language, cancellationToken);
                if (rulesConfig == null)
                {
                    this.host.Logger.WriteLine(string.Format(Strings.SubTextPaddingFormat,
                        string.Format(Strings.FailedToCreateRulesConfigForLanguage, language.Name)));
                    return false;
                }

                this.InternalState.Rulesets[language] = rulesConfig;

                currentLanguage++;
                progress?.Report(new FixedStepsProgress(string.Empty, currentLanguage, languageCount));

                this.host.Logger.WriteLine(string.Format(Strings.SubTextPaddingFormat,
                    string.Format(Strings.QualityProfileDownloadSuccessfulMessageFormat, qualityProfileInfo.Name, qualityProfileInfo.Key, language.Name)));
            }

            return true;
        }

        public void PrepareToInstallPackages()
        {
            this.NuGetBindingOperation.PrepareOnUIThread();
        }

        public void InstallPackages(IProgress<FixedStepsProgress> progress, CancellationToken cancellationToken)
        {
            this.InternalState.BindingOperationSucceeded = this.NuGetBindingOperation.InstallPackages(this.InternalState.BindingProjects, progress, cancellationToken);
        }

        public void InitializeSolutionBindingOnUIThread()
        {
            this.solutionBindingOperation.RegisterKnownRuleSets(this.InternalState.Rulesets);

            var projectsToUpdate = GetProjectsForRulesetBinding(this.InternalState.IsFirstBinding, this.InternalState.BindingProjects.ToArray(),
                this.bindingInformationProvider, this.host.Logger);

            this.solutionBindingOperation.Initialize(projectsToUpdate, this.InternalState.QualityProfiles);
        }

        public void PrepareSolutionBinding(CancellationToken cancellationToken)
        {
            this.solutionBindingOperation.Prepare(cancellationToken);
        }

        public bool FinishSolutionBindingOnUIThread()
        {
            return this.solutionBindingOperation.CommitSolutionBinding();
        }

        public void SilentSaveSolutionIfDirty()
        {
            bool saved = VsShellUtils.SaveSolution(this.host, silent: true);
            Debug.Assert(saved, "Should not be cancellable");
        }

        public bool BindOperationSucceeded => InternalState.BindingOperationSucceeded;

        #endregion

        #region Private methods

        private void InformAboutFilteredOutProjects()
        {
            var includedProjects = this.InternalState.BindingProjects.ToList();
            var excludedProjects = this.projectSystem.GetSolutionProjects().Except(this.InternalState.BindingProjects).ToList();

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

            this.host.Logger.WriteLine(output.ToString());
        }

        internal /* for testing */ IEnumerable<Language> GetBindingLanguages()
        {
            return this.InternalState.BindingProjects.Select(ProjectToLanguageMapper.GetLanguageForProject)
                                       .Distinct()
                                       .Where(this.host.SupportedPluginLanguages.Contains)
                                       .ToArray();
        }

        internal /* for testing purposes */ static Project[] GetProjectsForRulesetBinding(bool isFirstBinding,
            Project[] allSupportedProjects,
            ISolutionBindingInformationProvider bindingInformationProvider,
            ILogger logger)
        {
            // If we are already bound we don't need to update/create rulesets in projects
            // that already have the ruleset information configured
            var projectsToUpdate = allSupportedProjects;

            if (isFirstBinding)
            {
                logger.WriteLine(Strings.Bind_Ruleset_InitialBinding);
            }
            else
            {
                var unboundProjects = bindingInformationProvider.GetUnboundProjects()?.ToArray() ?? new Project[] { };
                projectsToUpdate = projectsToUpdate.Intersect(unboundProjects).ToArray();

                var upToDateProjects = allSupportedProjects.Except(unboundProjects);
                if (upToDateProjects.Any())
                {
                    logger.WriteLine(Strings.Bind_Ruleset_SomeProjectsDoNotNeedToBeUpdated);
                    var projectList = string.Join(", ", upToDateProjects.Select(p => p.Name));
                    logger.WriteLine($"  {projectList}");
                }
                else
                {
                    logger.WriteLine(Strings.Bind_Ruleset_AllProjectsNeedToBeUpdated);
                }
            }
            return projectsToUpdate;
        }

        #endregion

        #region Workflow state

        internal class BindingProcessState
        {
            public BindingProcessState(bool isFirstBinding)
            {
                this.IsFirstBinding = isFirstBinding;
            }

            public bool IsFirstBinding { get; }

            public ISet<Project> BindingProjects
            {
                get;
            } = new HashSet<Project>();

            public Dictionary<Language, IRulesConfigurationFile> Rulesets
            {
                get;
            } = new Dictionary<Language, IRulesConfigurationFile>();

            public Dictionary<Language, SonarQubeQualityProfile> QualityProfiles
            {
                get;
            } = new Dictionary<Language, SonarQubeQualityProfile>();

            public bool BindingOperationSucceeded
            {
                get;
                set;
            } = true;
        }

        #endregion
    }
}
