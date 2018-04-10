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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using EnvDTE;
using FluentAssertions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.CodeAnalysis.RuleSets;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGet;
using SonarLint.VisualStudio.Integration.Binding;
using SonarLint.VisualStudio.Integration.Helpers;
using SonarLint.VisualStudio.Integration.Resources;
using SonarLint.VisualStudio.Progress.Controller;
using SonarQube.Client.Messages;
using SonarQube.Client.Models;
using SonarQube.Client.Services;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    [TestClass]
    public class BindingWorkflowTests
    {
        private ConfigurableServiceProvider serviceProvider;
        private Mock<ISonarQubeService> sonarQubeServiceMock;
        private ConfigurableVsOutputWindowPane outputWindowPane;
        private ConfigurableVsProjectSystemHelper projectSystemHelper;
        private ConfigurableHost host;

        [TestInitialize]
        public void TestInitialize()
        {
            this.serviceProvider = new ConfigurableServiceProvider();

            var outputWindow = new ConfigurableVsOutputWindow();
            this.outputWindowPane = outputWindow.GetOrCreateSonarLintPane();
            this.serviceProvider.RegisterService(typeof(SVsOutputWindow), outputWindow);

            this.sonarQubeServiceMock = new Mock<ISonarQubeService>();
            this.projectSystemHelper = new ConfigurableVsProjectSystemHelper(this.serviceProvider);

            var sccFileSystem = new ConfigurableSourceControlledFileSystem();
            var ruleSerializer = new ConfigurableRuleSetSerializer(sccFileSystem);

            this.serviceProvider.RegisterService(typeof(ISourceControlledFileSystem), sccFileSystem);
            this.serviceProvider.RegisterService(typeof(IRuleSetSerializer), ruleSerializer);
            this.serviceProvider.RegisterService(typeof(IProjectSystemHelper), this.projectSystemHelper);

            this.host = new ConfigurableHost(this.serviceProvider, Dispatcher.CurrentDispatcher);
        }

        #region Tests

        [TestMethod]
        public void BindingWorkflow_ArgChecks()
        {
            var validHost = new ConfigurableHost();
            var bindingArgs = new BindCommandArgs("key", "name", new ConnectionInformation(new Uri("http://server")));

            Exceptions.Expect<ArgumentNullException>(() => new BindingWorkflow(null, bindingArgs));
            Exceptions.Expect<ArgumentNullException>(() => new BindingWorkflow(validHost, null));
        }

        [TestMethod]
        public async Task BindingWorkflow_DownloadQualityProfile_Success()
        {
            // Arrange
            const string QualityProfileName = "SQQualityProfileName";
            const string ProjectName = "SQProjectName";

            // Record all of the calls to NuGetBindingOperation.ProcessExport
            var actualProfiles = new List<Tuple<Language, RoslynExportProfileResponse>>();
            Mock<INuGetBindingOperation> nuGetOpMock = new Mock<INuGetBindingOperation>();
            nuGetOpMock.Setup(x => x.ProcessExport(It.IsAny<Language>(), It.IsAny<RoslynExportProfileResponse>()))
                .Callback<Language, RoslynExportProfileResponse>((l, r) => actualProfiles.Add(new Tuple<Language, RoslynExportProfileResponse>(l, r)))
                .Returns(true);

            BindingWorkflow testSubject = this.CreateTestSubject("key", ProjectName, nuGetOpMock.Object);


            ConfigurableProgressController controller = new ConfigurableProgressController();
            var notifications = new ConfigurableProgressStepExecutionEvents();

            RuleSet ruleSet = TestRuleSetHelper.CreateTestRuleSetWithRuleIds(new[] { "Key1", "Key2" });
            var expectedRuleSet = new RuleSet(ruleSet)
            {
                NonLocalizedDisplayName = string.Format(Strings.SonarQubeRuleSetNameFormat, ProjectName, QualityProfileName),
                NonLocalizedDescription = "\r\nhttp://connected/profiles/show?key="
            };
            var nugetPackages = new[] { new PackageName("myPackageId", new SemanticVersion("1.0.0")) };
            var additionalFiles = new[] { new AdditionalFileResponse { FileName = "abc.xml", Content = new byte[] { 1, 2, 3 } } };
            RoslynExportProfileResponse export = RoslynExportProfileHelper.CreateExport(ruleSet, nugetPackages, additionalFiles);

            var language = Language.VBNET;
            SonarQubeQualityProfile profile = this.ConfigureProfileExport(export, language, QualityProfileName);

            // Act
            await testSubject.DownloadQualityProfileAsync(controller, notifications, new[] { language }, CancellationToken.None);

            // Assert
            RuleSetAssert.AreEqual(expectedRuleSet, testSubject.Rulesets[language], "Unexpected rule set");
            testSubject.QualityProfiles[language].Should().Be(profile);
            VerifyNuGetPackgesDownloaded(nugetPackages, language, actualProfiles);

            controller.NumberOfAbortRequests.Should().Be(0);
            notifications.AssertProgress(0.0, 1.0);
            notifications.AssertProgressMessages(Strings.DownloadingQualityProfileProgressMessage, string.Empty);

            this.outputWindowPane.AssertOutputStrings(1);
            var expectedOutput = string.Format(Strings.SubTextPaddingFormat,
                string.Format(Strings.QualityProfileDownloadSuccessfulMessageFormat, QualityProfileName, string.Empty, language.Name));
            this.outputWindowPane.AssertOutputStrings(expectedOutput);
        }

        [TestMethod]
        public async Task BindingWorkflow_DownloadQualityProfile_WhenQualityProfileIsNotAvailable_Fails()
        {
            // Arrange
            BindingWorkflow testSubject = this.CreateTestSubject();
            ConfigurableProgressController controller = new ConfigurableProgressController();
            var notifications = new ConfigurableProgressStepExecutionEvents();

            var language = Language.CSharp;
            this.ConfigureProfileExport(null, Language.VBNET, "");

            // Act
            await testSubject.DownloadQualityProfileAsync(controller, notifications, new[] { language }, CancellationToken.None);

            // Assert
            testSubject.Rulesets.Should().NotContainKey(Language.VBNET, "Not expecting any rules for this language");
            testSubject.Rulesets.Should().NotContainKey(language, "Not expecting any rules");
            controller.NumberOfAbortRequests.Should().Be(1);

            notifications.AssertProgressMessages(Strings.DownloadingQualityProfileProgressMessage);

            this.outputWindowPane.AssertOutputStrings(1);
            var expectedOutput = string.Format(Strings.SubTextPaddingFormat,
                string.Format(Strings.CannotDownloadQualityProfileForLanguage, language.Name));
            this.outputWindowPane.AssertOutputStrings(expectedOutput);
        }

        [TestMethod]
        public async Task BindingWorkflow_DownloadQualityProfile_WhenProfileExportIsNotAvailable_Fails()
        {
            // Arrange
            BindingWorkflow testSubject = this.CreateTestSubject();
            ConfigurableProgressController controller = new ConfigurableProgressController();
            var notifications = new ConfigurableProgressStepExecutionEvents();

            var language = Language.CSharp;
            this.ConfigureProfileExport(null, language, "");

            // Act
            await testSubject.DownloadQualityProfileAsync(controller, notifications, new[] { language }, CancellationToken.None);

            // Assert
            testSubject.Rulesets.Should().NotContainKey(Language.VBNET, "Not expecting any rules for this language");
            testSubject.Rulesets.Should().NotContainKey(language, "Not expecting any rules");
            controller.NumberOfAbortRequests.Should().Be(1);

            notifications.AssertProgressMessages(Strings.DownloadingQualityProfileProgressMessage);

            this.outputWindowPane.AssertOutputStrings(1);
            var expectedOutput = string.Format(Strings.SubTextPaddingFormat,
                string.Format(Strings.QualityProfileDownloadFailedMessageFormat, string.Empty, string.Empty, language.Name));
            this.outputWindowPane.AssertOutputStrings(expectedOutput);
        }

        [TestMethod]
        public async Task BindingWorkflow_DownloadQualityProfile_WithNoRules_Fails()
        {
            // Arrange
            const string QualityProfileName = "SQQualityProfileName";
            const string ProjectName = "SQProjectName";
            BindingWorkflow testSubject = this.CreateTestSubject("key", ProjectName);
            ConfigurableProgressController controller = new ConfigurableProgressController();
            var notifications = new ConfigurableProgressStepExecutionEvents();

            RuleSet ruleSet = TestRuleSetHelper.CreateTestRuleSetWithRuleIds(Enumerable.Empty<string>());
            var nugetPackages = new[] { new PackageName("myPackageId", new SemanticVersion("1.0.0")) };
            var additionalFiles = new[] { new AdditionalFileResponse { FileName = "abc.xml", Content = new byte[] { 1, 2, 3 } } };
            RoslynExportProfileResponse export = RoslynExportProfileHelper.CreateExport(ruleSet, nugetPackages, additionalFiles);

            var language = Language.VBNET;
            SonarQubeQualityProfile profile = this.ConfigureProfileExport(export, language, QualityProfileName);

            // Act
            await testSubject.DownloadQualityProfileAsync(controller, notifications, new[] { language }, CancellationToken.None);

            // Assert
            testSubject.Rulesets.Should().NotContainKey(Language.VBNET, "Not expecting any rules for this language");
            testSubject.Rulesets.Should().NotContainKey(language, "Not expecting any rules");
            controller.NumberOfAbortRequests.Should().Be(1);

            notifications.AssertProgressMessages(Strings.DownloadingQualityProfileProgressMessage);

            this.outputWindowPane.AssertOutputStrings(1);
            var expectedOutput = string.Format(Strings.SubTextPaddingFormat,
                string.Format(Strings.NoSonarAnalyzerActiveRulesForQualityProfile, QualityProfileName, language.Name));
            this.outputWindowPane.AssertOutputStrings(expectedOutput);
        }

        [TestMethod]
        public async Task BindingWorkflow_DownloadQualityProfile_WithNoActiveRules_Fails()
        {
            // Arrange
            const string QualityProfileName = "SQQualityProfileName";
            const string ProjectName = "SQProjectName";
            BindingWorkflow testSubject = this.CreateTestSubject("key", ProjectName);
            ConfigurableProgressController controller = new ConfigurableProgressController();
            var notifications = new ConfigurableProgressStepExecutionEvents();

            RuleSet ruleSet = TestRuleSetHelper.CreateTestRuleSetWithRuleIds(new[] { "Key1", "Key2" });
            foreach (var rule in ruleSet.Rules)
            {
                rule.Action = RuleAction.None;
            }
            var expectedRuleSet = new RuleSet(ruleSet)
            {
                NonLocalizedDisplayName = string.Format(Strings.SonarQubeRuleSetNameFormat, ProjectName, QualityProfileName),
                NonLocalizedDescription = "\r\nhttp://connected/profiles/show?key="
            };
            var nugetPackages = new[] { new PackageName("myPackageId", new SemanticVersion("1.0.0")) };
            var additionalFiles = new[] { new AdditionalFileResponse { FileName = "abc.xml", Content = new byte[] { 1, 2, 3 } } };
            RoslynExportProfileResponse export = RoslynExportProfileHelper.CreateExport(ruleSet, nugetPackages, additionalFiles);

            var language = Language.VBNET;
            SonarQubeQualityProfile profile = this.ConfigureProfileExport(export, language, QualityProfileName);

            // Act
            await testSubject.DownloadQualityProfileAsync(controller, notifications, new[] { language }, CancellationToken.None);

            // Assert
            testSubject.Rulesets.Should().NotContainKey(Language.VBNET, "Not expecting any rules for this language");
            testSubject.Rulesets.Should().NotContainKey(language, "Not expecting any rules");
            controller.NumberOfAbortRequests.Should().Be(1);

            notifications.AssertProgressMessages(Strings.DownloadingQualityProfileProgressMessage);

            this.outputWindowPane.AssertOutputStrings(1);
            var expectedOutput = string.Format(Strings.SubTextPaddingFormat,
                string.Format(Strings.NoSonarAnalyzerActiveRulesForQualityProfile, QualityProfileName, language.Name));
            this.outputWindowPane.AssertOutputStrings(expectedOutput);
        }

        [TestMethod]
        public async Task BindingWorkflow_DownloadQualityProfile_WithNoNugetPackage_Fails()
        {
            // Arrange
            const string QualityProfileName = "SQQualityProfileName";
            const string ProjectName = "SQProjectName";
            BindingWorkflow testSubject = this.CreateTestSubject("key", ProjectName);
            ConfigurableProgressController controller = new ConfigurableProgressController();
            var notifications = new ConfigurableProgressStepExecutionEvents();

            RuleSet ruleSet = TestRuleSetHelper.CreateTestRuleSetWithRuleIds(new[] { "Key1", "Key2" });
            var expectedRuleSet = new RuleSet(ruleSet)
            {
                NonLocalizedDisplayName = string.Format(Strings.SonarQubeRuleSetNameFormat, ProjectName, QualityProfileName),
                NonLocalizedDescription = "\r\nhttp://connected/profiles/show?key="
            };
            var additionalFiles = new[] { new AdditionalFileResponse { FileName = "abc.xml", Content = new byte[] { 1, 2, 3 } } };
            RoslynExportProfileResponse export = RoslynExportProfileHelper.CreateExport(ruleSet, Enumerable.Empty<PackageName>(), additionalFiles);

            var language = Language.VBNET;
            SonarQubeQualityProfile profile = this.ConfigureProfileExport(export, language, QualityProfileName);

            // Act
            await testSubject.DownloadQualityProfileAsync(controller, notifications, new[] { language }, CancellationToken.None);

            // Assert
            testSubject.Rulesets.Should().NotContainKey(Language.VBNET, "Not expecting any rules for this language");
            testSubject.Rulesets.Should().NotContainKey(language, "Not expecting any rules");
            controller.NumberOfAbortRequests.Should().Be(1);

            notifications.AssertProgressMessages(Strings.DownloadingQualityProfileProgressMessage);

            this.outputWindowPane.AssertOutputStrings(1);
            var expectedOutput = string.Format(Strings.SubTextPaddingFormat,
                string.Format(Strings.NoNuGetPackageForQualityProfile, language.Name));
            this.outputWindowPane.AssertOutputStrings(expectedOutput);
        }

        [TestMethod]
        public void BindingWorkflow_InstallPackages_Succeeds_SuccessPropertyIsTrue()
        {
            // Arrange
            var bindingArgs = new BindCommandArgs("projectKey", "projectName", new ConnectionInformation(new Uri("http://connected")));

            Mock<INuGetBindingOperation> nugetMock = new Mock<INuGetBindingOperation>();
            nugetMock.Setup(x => x.InstallPackages(It.IsAny<ISet<Project>>(),
                It.IsAny<IProgressController>(),
                It.IsAny<IProgressStepExecutionEvents>(),
                It.IsAny<CancellationToken>())).Returns(true);

            var testSubject = new BindingWorkflow(this.host, bindingArgs, nugetMock.Object);

            ProjectMock project1 = new ProjectMock("project1") { ProjectKind = ProjectSystemHelper.CSharpProjectKind };
            testSubject.BindingProjects.Clear();
            testSubject.BindingProjects.Add(project1);

            var progressEvents = new ConfigurableProgressStepExecutionEvents();
            var cts = new CancellationTokenSource();

            testSubject.BindingOperationSucceeded = false;

            // Act
            testSubject.InstallPackages(new ConfigurableProgressController(), progressEvents, cts.Token);

            // Assert
            testSubject.BindingOperationSucceeded.Should().BeTrue();
        }

        [TestMethod]
        public void BindingWorkflow_InstallPackages_Succeeds_SuccessPropertyIsFalse()
        {
            // Arrange
            var bindingArgs = new BindCommandArgs("projectKey", "projectName", new ConnectionInformation(new Uri("http://connected")));

            Mock<INuGetBindingOperation> nugetMock = new Mock<INuGetBindingOperation>();
            nugetMock.Setup(x => x.InstallPackages(It.IsAny<ISet<Project>>(),
                It.IsAny<IProgressController>(),
                It.IsAny<IProgressStepExecutionEvents>(),
                It.IsAny<CancellationToken>())).Returns(false);

            var testSubject = new BindingWorkflow(this.host, bindingArgs, nugetMock.Object);

            ProjectMock project1 = new ProjectMock("project1") { ProjectKind = ProjectSystemHelper.CSharpProjectKind };
            testSubject.BindingProjects.Clear();
            testSubject.BindingProjects.Add(project1);

            var progressEvents = new ConfigurableProgressStepExecutionEvents();
            var cts = new CancellationTokenSource();

            testSubject.BindingOperationSucceeded = true;

            // Act
            testSubject.InstallPackages(new ConfigurableProgressController(), progressEvents, cts.Token);

            // Assert
            testSubject.BindingOperationSucceeded.Should().BeFalse();
        }

        [TestMethod]
        public void BindingWorkflow_EmitBindingCompleteMessage()
        {
            // Arrange
            var testSubject = this.CreateTestSubject();

            // Test case 1: Default state is 'true'
            testSubject.BindingOperationSucceeded.Should().BeTrue($"Initial state of {nameof(BindingWorkflow.BindingOperationSucceeded)} should be true");

            // Test case 2: All packages installed
            // Arrange
            var notificationsOk = new ConfigurableProgressStepExecutionEvents();
            testSubject.BindingOperationSucceeded = true;

            // Act
            testSubject.EmitBindingCompleteMessage(notificationsOk);

            // Assert
            notificationsOk.AssertProgressMessages(string.Format(CultureInfo.CurrentCulture, Strings.FinishedSolutionBindingWorkflowSuccessful));

            // Test case 3: Not all packages installed
            // Arrange
            var notificationsFail = new ConfigurableProgressStepExecutionEvents();
            testSubject.BindingOperationSucceeded = false;

            // Act
            testSubject.EmitBindingCompleteMessage(notificationsFail);

            // Assert
            notificationsFail.AssertProgressMessages(string.Format(CultureInfo.CurrentCulture, Strings.FinishedSolutionBindingWorkflowNotAllPackagesInstalled));
        }

        [TestMethod]
        public void BindingWorkflow_PromptSaveSolutionIfDirty()
        {
            // Arrange
            var testSubject = this.CreateTestSubject();
            var solution = new SolutionMock();
            serviceProvider.RegisterService(typeof(SVsSolution), solution);
            var controller = new ConfigurableProgressController();

            // Case 1: Users saves the changes
            solution.SaveSolutionElementAction = (options, hierarchy, docCookie) => VSConstants.S_OK;
            // Act
            testSubject.PromptSaveSolutionIfDirty(controller, CancellationToken.None);
            // Assert
            this.outputWindowPane.AssertOutputStrings(0);
            controller.NumberOfAbortRequests.Should().Be(0);

            // Case 2: Users cancels the save
            solution.SaveSolutionElementAction = (options, hierarchy, docCookie) => VSConstants.S_FALSE;
            // Act
            testSubject.PromptSaveSolutionIfDirty(controller, CancellationToken.None);
            // Assert
            this.outputWindowPane.AssertOutputStrings(Strings.SolutionSaveCancelledBindAborted);
            controller.NumberOfAbortRequests.Should().Be(1);
        }

        [TestMethod]
        public void BindingWorkflow_SilentSaveSolutionIfDirty()
        {
            // Arrange
            var testSubject = this.CreateTestSubject();
            var solution = new SolutionMock();
            serviceProvider.RegisterService(typeof(SVsSolution), solution);
            solution.SaveSolutionElementAction = (options, hierarchy, docCookie) => VSConstants.S_OK;

            // Act
            testSubject.SilentSaveSolutionIfDirty();

            // Assert
            this.outputWindowPane.AssertOutputStrings(0);
        }

        [TestMethod]
        public void BindingWorkflow_GetBindingLanguages_ReturnsDistinctLanguagesForProjects()
        {
            // Arrange
            var testSubject = this.CreateTestSubject();

            var csProject1 = new ProjectMock("cs1.csproj");
            var csProject2 = new ProjectMock("cs2.csproj");
            var csProject3 = new ProjectMock("cs3.csproj");
            csProject1.SetCSProjectKind();
            csProject2.SetCSProjectKind();
            csProject3.SetCSProjectKind();
            var vbNetProject1 = new ProjectMock("vb1.vbproj");
            var vbNetProject2 = new ProjectMock("vb2.vbproj");
            vbNetProject1.SetVBProjectKind();
            vbNetProject2.SetVBProjectKind();
            var projects = new[]
            {
                csProject1,
                csProject2,
                vbNetProject1,
                csProject3,
                vbNetProject2
            };
            testSubject.BindingProjects.AddRange(projects);

            var expectedLanguages = new[] { Language.CSharp, Language.VBNET };
            this.host.SupportedPluginLanguages.UnionWith(expectedLanguages);

            // Act
            var actualLanguages = testSubject.GetBindingLanguages();

            // Assert
            CollectionAssert.AreEquivalent(expectedLanguages, actualLanguages.ToArray(), "Unexpected languages for binding projects");
        }

        [TestMethod]
        public void BindingWorkflow_GetBindingLanguages_FiltersProjectsWithUnsupportedPluginLanguage()
        {
            // Arrange
            var testSubject = this.CreateTestSubject();

            var csProject1 = new ProjectMock("cs1.csproj");
            var csProject2 = new ProjectMock("cs2.csproj");
            var csProject3 = new ProjectMock("cs3.csproj");
            csProject1.SetCSProjectKind();
            csProject2.SetCSProjectKind();
            csProject3.SetCSProjectKind();
            var vbNetProject1 = new ProjectMock("vb1.vbproj");
            var vbNetProject2 = new ProjectMock("vb2.vbproj");
            vbNetProject1.SetVBProjectKind();
            vbNetProject2.SetVBProjectKind();
            var projects = new[]
            {
                csProject1,
                csProject2,
                vbNetProject1,
                csProject3,
                vbNetProject2
            };
            testSubject.BindingProjects.AddRange(projects);

            var expectedLanguages = new[] { Language.VBNET };
            this.host.SupportedPluginLanguages.UnionWith(expectedLanguages);

            // Act
            var actualLanguages = testSubject.GetBindingLanguages();

            // Assert
            CollectionAssert.AreEquivalent(expectedLanguages, actualLanguages.ToArray(), "Unexpected languages for binding projects");
        }

        [TestMethod]
        public void BindingWorkflow_DiscoverProjects_AddsMatchingProjectsToBinding()
        {
            // Arrange
            ThreadHelper.SetCurrentThreadAsUIThread();
            var controller = new ConfigurableProgressController();
            var progressEvents = new ConfigurableProgressStepExecutionEvents();

            var csProject1 = new ProjectMock("cs1.csproj");
            var csProject2 = new ProjectMock("cs2.csproj");
            csProject1.SetCSProjectKind();
            csProject2.SetCSProjectKind();

            var matchingProjects = new[] { csProject1, csProject2 };
            this.projectSystemHelper.FilteredProjects = matchingProjects;

            var testSubject = this.CreateTestSubject();
            this.host.SupportedPluginLanguages.UnionWith(new[] { Language.CSharp });

            // Act
            testSubject.DiscoverProjects(controller, progressEvents);

            // Assert
            CollectionAssert.AreEqual(matchingProjects, testSubject.BindingProjects.ToArray(), "Unexpected projects selected for binding");
            progressEvents.AssertProgressMessages(Strings.DiscoveringSolutionProjectsProgressMessage);
        }

        private void BindingWorkflow_DiscoverProjects_GenericPart(ConfigurableProgressController controller, ConfigurableProgressStepExecutionEvents progressEvents, int numberOfProjectsToCreate, int numberOfProjectsToInclude)
        {
            // Arrange
            List<Project> projects = new List<Project>();
            for (int i = 0; i < numberOfProjectsToCreate; i++)
            {
                var project = new ProjectMock($"cs{i}.csproj");
                project.SetCSProjectKind();
                projects.Add(project);
            }

            this.projectSystemHelper.FilteredProjects = projects.Take(numberOfProjectsToInclude);
            this.projectSystemHelper.Projects = projects;

            var testSubject = this.CreateTestSubject();
            this.host.SupportedPluginLanguages.UnionWith(new[] { Language.CSharp });

            // Act
            testSubject.DiscoverProjects(controller, progressEvents);

            // Assert
            testSubject.BindingProjects.Should().HaveCount(numberOfProjectsToInclude, "Expected " + numberOfProjectsToInclude + " project(s) selected for binding");
            progressEvents.AssertProgressMessages(Strings.DiscoveringSolutionProjectsProgressMessage);
            this.outputWindowPane.AssertOutputStrings(1);

            // Returns expected output message
            var expectedOutput = new StringBuilder();
            expectedOutput.AppendFormat(Strings.SubTextPaddingFormat, Strings.DiscoveringSolutionIncludedProjectsHeader).AppendLine();
            if (numberOfProjectsToInclude > 0)
            {
                this.projectSystemHelper.FilteredProjects.ToList().ForEach(p => expectedOutput.AppendFormat("   * {0}\r\n", p.Name));
            }
            else
            {
                var msg = string.Format(Strings.DiscoveredIncludedOrExcludedProjectFormat, Strings.NoProjectsExcludedFromBinding);
                expectedOutput.AppendFormat(Strings.SubTextPaddingFormat, msg).AppendLine();
            }
            expectedOutput.AppendFormat(Strings.SubTextPaddingFormat, Strings.DiscoveringSolutionExcludedProjectsHeader).AppendLine();
            if (numberOfProjectsToCreate - numberOfProjectsToInclude > 0)
            {
                this.projectSystemHelper.Projects.Except(this.projectSystemHelper.FilteredProjects)
                                                 .ToList()
                                                 .ForEach(p => expectedOutput.AppendFormat("   * {0}\r\n", p.Name));
            }
            else
            {
                var msg = string.Format(Strings.DiscoveredIncludedOrExcludedProjectFormat, Strings.NoProjectsExcludedFromBinding);
                expectedOutput.AppendFormat(Strings.SubTextPaddingFormat, msg).AppendLine();
            }
            expectedOutput.AppendFormat(Strings.SubTextPaddingFormat, Strings.FilteredOutProjectFromBindingEnding);

            this.outputWindowPane.AssertOutputStrings(expectedOutput.ToString());
        }

        [TestMethod]
        public void BindingWorkflow_DiscoverProjects_OutputsIncludedProjects()
        {
            // Arrange
            ThreadHelper.SetCurrentThreadAsUIThread();
            var controller = new ConfigurableProgressController();
            var progressEvents = new ConfigurableProgressStepExecutionEvents();

            // Act & Common Assert
            BindingWorkflow_DiscoverProjects_GenericPart(controller, progressEvents, 2, 2);
        }

        [TestMethod]
        public void BindingWorkflow_DiscoverProjects_OutputsExcludedProjects()
        {
            // Arrange
            ThreadHelper.SetCurrentThreadAsUIThread();
            var controller = new ConfigurableProgressController();
            var progressEvents = new ConfigurableProgressStepExecutionEvents();

            // Act & Common Assert
            BindingWorkflow_DiscoverProjects_GenericPart(controller, progressEvents, 2, 0);
        }

        [TestMethod]
        public void BindingWorkflow_DiscoverProjects_OutputsIncludedAndExcludedProjects()
        {
            // Arrange
            ThreadHelper.SetCurrentThreadAsUIThread();
            var controller = new ConfigurableProgressController();
            var progressEvents = new ConfigurableProgressStepExecutionEvents();

            // Act & Common Assert
            BindingWorkflow_DiscoverProjects_GenericPart(controller, progressEvents, 4, 2);
        }

        [TestMethod]
        public void BindingWorkflow_DiscoverProjects_NoMatchingProjects_AbortsWorkflow()
        {
            // Arrange
            ThreadHelper.SetCurrentThreadAsUIThread();
            var controller = new ConfigurableProgressController();
            var progressEvents = new ConfigurableProgressStepExecutionEvents();

            // Act & Common Assert
            BindingWorkflow_DiscoverProjects_GenericPart(controller, progressEvents, 0, 0);

            // Assert
            controller.NumberOfAbortRequests.Should().Be(1);
        }

        #endregion Tests

        #region Helpers

        private BindingWorkflow CreateTestSubject(string projectKey = "anykey", string projectName = "anyname", 
            INuGetBindingOperation nuGetBindingOperation = null)
        {
            this.host.SonarQubeService = this.sonarQubeServiceMock.Object;

            var bindingArgs = new BindCommandArgs(projectKey, projectName, new ConnectionInformation(new Uri("http://connected")));

            if (nuGetBindingOperation == null)
            {
                return new BindingWorkflow(this.host, bindingArgs);
            }
            return new BindingWorkflow(this.host, bindingArgs, nuGetBindingOperation);
        }

        private SonarQubeQualityProfile ConfigureProfileExport(RoslynExportProfileResponse export, Language language, string profileName)
        {
            var profile = new SonarQubeQualityProfile("", profileName, "", false, DateTime.Now);
            this.sonarQubeServiceMock
                .Setup(x => x.GetQualityProfileAsync(It.IsAny<string>(), It.IsAny<string>(), language.ToServerLanguage(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);
            this.sonarQubeServiceMock
                .Setup(x => x.GetRoslynExportProfileAsync(profileName, It.IsAny<string>(), It.IsAny<SonarQubeLanguage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(export);

            return profile;
        }

        private static void VerifyNuGetPackgesDownloaded(IEnumerable<PackageName> expectedPackages,
            Language language,
            IList<Tuple<Language, RoslynExportProfileResponse>> actualProfiles)
        {
            actualProfiles.All(p => p.Item1 == language).Should().BeTrue();

            var actualPackages = actualProfiles.SelectMany(p => p.Item2?.Deployment?.NuGetPackages.Select(
                ngp => new PackageName(ngp.Id, new SemanticVersion(ngp.Version))));

            actualPackages.Should().BeEquivalentTo(expectedPackages);
            actualPackages.Should().HaveSameCount(expectedPackages, "Different number of packages.");
            actualPackages.Select(x => x.ToString()).Should().Equal(expectedPackages.Select(x => x.ToString()));
        }

        #endregion Helpers
    }
}
