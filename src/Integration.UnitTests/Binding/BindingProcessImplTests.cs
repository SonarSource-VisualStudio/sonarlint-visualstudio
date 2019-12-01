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
using SonarLint.VisualStudio.Integration.NewConnectedMode;
using SonarLint.VisualStudio.Integration.Resources;
using SonarLint.VisualStudio.Progress.Controller;
using SonarQube.Client;
using SonarQube.Client.Messages;
using SonarQube.Client.Models;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    [TestClass]
    public class BindingProcessImplTests
    {
        private ConfigurableServiceProvider serviceProvider;
        private Mock<ISonarQubeService> sonarQubeServiceMock;
        private ConfigurableVsOutputWindowPane outputWindowPane;
        private ConfigurableVsProjectSystemHelper projectSystemHelper;
        private ConfigurableSolutionRuleSetsInformationProvider ruleSetsInformationProvider;
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
            this.ruleSetsInformationProvider = new ConfigurableSolutionRuleSetsInformationProvider();

            this.serviceProvider.RegisterService(typeof(ISourceControlledFileSystem), sccFileSystem);
            this.serviceProvider.RegisterService(typeof(IRuleSetSerializer), ruleSerializer);
            this.serviceProvider.RegisterService(typeof(IProjectSystemHelper), this.projectSystemHelper);
            this.serviceProvider.RegisterService(typeof(ISolutionRuleSetsInformationProvider), this.ruleSetsInformationProvider);

            this.host = new ConfigurableHost(this.serviceProvider, Dispatcher.CurrentDispatcher);
        }

        #region Tests

        [TestMethod]
        public void Ctor_ArgChecks()
        {
            var validHost = new ConfigurableHost();
            var bindingArgs = new BindCommandArgs("key", "name", new ConnectionInformation(new Uri("http://server")));
            var slnBindOp = new Mock<ISolutionBindingOperation>().Object;
            var nuGetOp = new Mock<INuGetBindingOperation>().Object;
            var bindingInfoProvider = new ConfigurableSolutionBindingInformationProvider();

            // 1. Null host
            Action act = () => new BindingProcessImpl(null, bindingArgs, slnBindOp, nuGetOp, bindingInfoProvider);
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("host");

            // 2. Null binding args
            act = () => new BindingProcessImpl(validHost, null, slnBindOp, nuGetOp, bindingInfoProvider);
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("bindingArgs");

            // 3. Null solution binding operation
            act = () => new BindingProcessImpl(validHost, bindingArgs, null, nuGetOp, bindingInfoProvider);
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("solutionBindingOperation");

            // 4. Null NuGet operation
            act = () => new BindingProcessImpl(validHost, bindingArgs, slnBindOp, null, bindingInfoProvider);
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("nugetBindingOperation");

            // 5. Null binding info provider
            act = () => new BindingProcessImpl(validHost, bindingArgs, slnBindOp, nuGetOp, null);
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("bindingInformationProvider");
        }

        [TestMethod]
        public async Task DownloadQualityProfile_Success()
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

            var testSubject = this.CreateTestSubject("key", ProjectName, nuGetOpMock.Object);

            var notifications = new ConfigurableProgressStepExecutionEvents();
            var progressAdapter = new FixedStepsProgressAdapter(notifications);

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
            var result = await testSubject.DownloadQualityProfileAsync(progressAdapter, new[] { language }, CancellationToken.None);

            // Assert
            result.Should().BeTrue();
            RuleSetAssert.AreEqual(expectedRuleSet, testSubject.InternalState.Rulesets[language], "Unexpected rule set");
            testSubject.InternalState.QualityProfiles[language].Should().Be(profile);
            VerifyNuGetPackgesDownloaded(nugetPackages, language, actualProfiles);

            notifications.AssertProgress(0.0, 1.0);
            notifications.AssertProgressMessages(Strings.DownloadingQualityProfileProgressMessage, string.Empty);

            this.outputWindowPane.AssertOutputStrings(1);
            var expectedOutput = string.Format(Strings.SubTextPaddingFormat,
                string.Format(Strings.QualityProfileDownloadSuccessfulMessageFormat, QualityProfileName, string.Empty, language.Name));
            this.outputWindowPane.AssertOutputStrings(expectedOutput);
        }

        [TestMethod]
        public async Task DownloadQualityProfile_WhenQualityProfileIsNotAvailable_Fails()
        {
            // Arrange
            var testSubject = this.CreateTestSubject();
            var notifications = new ConfigurableProgressStepExecutionEvents();
            var progressAdapter = new FixedStepsProgressAdapter(notifications);

            var language = Language.CSharp;
            this.ConfigureProfileExport(null, Language.VBNET, "");

            // Act
            var result = await testSubject.DownloadQualityProfileAsync(progressAdapter, new[] { language }, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
            testSubject.InternalState.Rulesets.Should().NotContainKey(Language.VBNET, "Not expecting any rules for this language");
            testSubject.InternalState.Rulesets.Should().NotContainKey(language, "Not expecting any rules");

            notifications.AssertProgressMessages(Strings.DownloadingQualityProfileProgressMessage);

            this.outputWindowPane.AssertOutputStrings(1);
            var expectedOutput = string.Format(Strings.SubTextPaddingFormat,
                string.Format(Strings.CannotDownloadQualityProfileForLanguage, language.Name));
            this.outputWindowPane.AssertOutputStrings(expectedOutput);
        }

        [TestMethod]
        public async Task DownloadQualityProfile_WhenProfileExportIsNotAvailable_Fails()
        {
            // Arrange
            var testSubject = this.CreateTestSubject();
            var notifications = new ConfigurableProgressStepExecutionEvents();
            var progressAdapter = new FixedStepsProgressAdapter(notifications);

            var language = Language.CSharp;
            this.ConfigureProfileExport(null, language, "");

            // Act
            var result = await testSubject.DownloadQualityProfileAsync(progressAdapter, new[] { language }, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
            testSubject.InternalState.Rulesets.Should().NotContainKey(Language.VBNET, "Not expecting any rules for this language");
            testSubject.InternalState.Rulesets.Should().NotContainKey(language, "Not expecting any rules");

            notifications.AssertProgressMessages(Strings.DownloadingQualityProfileProgressMessage);

            this.outputWindowPane.AssertOutputStrings(1);
            var expectedOutput = string.Format(Strings.SubTextPaddingFormat,
                string.Format(Strings.QualityProfileDownloadFailedMessageFormat, string.Empty, string.Empty, language.Name));
            this.outputWindowPane.AssertOutputStrings(expectedOutput);
        }

        [TestMethod]
        public async Task DownloadQualityProfile_WithNoRules_Fails()
        {
            // Arrange
            const string QualityProfileName = "SQQualityProfileName";
            const string ProjectName = "SQProjectName";
            var testSubject = this.CreateTestSubject("key", ProjectName);
            var notifications = new ConfigurableProgressStepExecutionEvents();
            var progressAdapter = new FixedStepsProgressAdapter(notifications);

            RuleSet ruleSet = TestRuleSetHelper.CreateTestRuleSetWithRuleIds(Enumerable.Empty<string>());
            var nugetPackages = new[] { new PackageName("myPackageId", new SemanticVersion("1.0.0")) };
            var additionalFiles = new[] { new AdditionalFileResponse { FileName = "abc.xml", Content = new byte[] { 1, 2, 3 } } };
            RoslynExportProfileResponse export = RoslynExportProfileHelper.CreateExport(ruleSet, nugetPackages, additionalFiles);

            var language = Language.VBNET;
            this.ConfigureProfileExport(export, language, QualityProfileName);

            // Act
            var result = await testSubject.DownloadQualityProfileAsync(progressAdapter, new[] { language }, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
            testSubject.InternalState.Rulesets.Should().NotContainKey(Language.VBNET, "Not expecting any rules for this language");
            testSubject.InternalState.Rulesets.Should().NotContainKey(language, "Not expecting any rules");
            
            notifications.AssertProgressMessages(Strings.DownloadingQualityProfileProgressMessage);

            this.outputWindowPane.AssertOutputStrings(1);
            var expectedOutput = string.Format(Strings.SubTextPaddingFormat,
                string.Format(Strings.NoSonarAnalyzerActiveRulesForQualityProfile, QualityProfileName, language.Name));
            this.outputWindowPane.AssertOutputStrings(expectedOutput);
        }

        [TestMethod]
        public async Task DownloadQualityProfile_WithNoActiveRules_Fails()
        {
            // Arrange
            const string QualityProfileName = "SQQualityProfileName";
            const string ProjectName = "SQProjectName";
            var testSubject = this.CreateTestSubject("key", ProjectName);
            var notifications = new ConfigurableProgressStepExecutionEvents();
            var progressAdapter = new FixedStepsProgressAdapter(notifications);

            RuleSet ruleSet = TestRuleSetHelper.CreateTestRuleSetWithRuleIds(new[] { "Key1", "Key2" });
            foreach (var rule in ruleSet.Rules)
            {
                rule.Action = RuleAction.None;
            }
            var nugetPackages = new[] { new PackageName("myPackageId", new SemanticVersion("1.0.0")) };
            var additionalFiles = new[] { new AdditionalFileResponse { FileName = "abc.xml", Content = new byte[] { 1, 2, 3 } } };
            RoslynExportProfileResponse export = RoslynExportProfileHelper.CreateExport(ruleSet, nugetPackages, additionalFiles);

            var language = Language.VBNET;
            this.ConfigureProfileExport(export, language, QualityProfileName);

            // Act
            var result = await testSubject.DownloadQualityProfileAsync(progressAdapter, new[] { language }, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
            testSubject.InternalState.Rulesets.Should().NotContainKey(Language.VBNET, "Not expecting any rules for this language");
            testSubject.InternalState.Rulesets.Should().NotContainKey(language, "Not expecting any rules");
            testSubject.InternalState.Rulesets.Count.Should().Be(0);

            notifications.AssertProgressMessages(Strings.DownloadingQualityProfileProgressMessage);

            this.outputWindowPane.AssertOutputStrings(1);
            var expectedOutput = string.Format(Strings.SubTextPaddingFormat,
                string.Format(Strings.NoSonarAnalyzerActiveRulesForQualityProfile, QualityProfileName, language.Name));
            this.outputWindowPane.AssertOutputStrings(expectedOutput);
        }

        [TestMethod]
        public async Task DownloadQualityProfile_LegacyMode_WithNoNugetPackage_Fails()
        {
            // Arrange
            const string QualityProfileName = "SQQualityProfileName";
            const string ProjectName = "SQProjectName";
            var legacyNuGetBinding = new NuGetBindingOperation(this.host, this.host.Logger);
            var testSubject = this.CreateTestSubject("key", ProjectName, legacyNuGetBinding);
            var notifications = new ConfigurableProgressStepExecutionEvents();
            var progressAdapter = new FixedStepsProgressAdapter(notifications);

            RuleSet ruleSet = TestRuleSetHelper.CreateTestRuleSetWithRuleIds(new[] { "Key1", "Key2" });
            var additionalFiles = new[] { new AdditionalFileResponse { FileName = "abc.xml", Content = new byte[] { 1, 2, 3 } } };
            RoslynExportProfileResponse export = RoslynExportProfileHelper.CreateExport(ruleSet, Enumerable.Empty<PackageName>(), additionalFiles);

            var language = Language.VBNET;
            this.ConfigureProfileExport(export, language, QualityProfileName);

            // Act
            var result = await testSubject.DownloadQualityProfileAsync(progressAdapter, new[] { language }, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
            testSubject.InternalState.Rulesets.Should().NotContainKey(Language.VBNET, "Not expecting any rules for this language");
            testSubject.InternalState.Rulesets.Should().NotContainKey(language, "Not expecting any rules");
            testSubject.InternalState.Rulesets.Count.Should().Be(0);

            notifications.AssertProgressMessages(Strings.DownloadingQualityProfileProgressMessage);

            this.outputWindowPane.AssertOutputStrings(1);
            var expectedOutput = string.Format(Strings.SubTextPaddingFormat,
                string.Format(Strings.NoNuGetPackageForQualityProfile, language.Name));
            this.outputWindowPane.AssertOutputStrings(expectedOutput);
        }

        [TestMethod]
        public void GetBindingLanguages_ReturnsDistinctLanguagesForProjects()
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
            testSubject.InternalState.BindingProjects.AddRange(projects);

            var expectedLanguages = new[] { Language.CSharp, Language.VBNET };
            this.host.SupportedPluginLanguages.UnionWith(expectedLanguages);

            // Act
            var actualLanguages = testSubject.GetBindingLanguages();

            // Assert
            CollectionAssert.AreEquivalent(expectedLanguages, actualLanguages.ToArray(), "Unexpected languages for binding projects");
        }

        [TestMethod]
        public void GetBindingLanguages_FiltersProjectsWithUnsupportedPluginLanguage()
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
            testSubject.InternalState.BindingProjects.AddRange(projects);

            var expectedLanguages = new[] { Language.VBNET };
            this.host.SupportedPluginLanguages.UnionWith(expectedLanguages);

            // Act
            var actualLanguages = testSubject.GetBindingLanguages();

            // Assert
            CollectionAssert.AreEquivalent(expectedLanguages, actualLanguages.ToArray(), "Unexpected languages for binding projects");
        }

        [TestMethod]
        public void InstallPackages_Succeeds_SuccessPropertyIsTrue()
        {
            // Arrange
            var bindingArgs = new BindCommandArgs("projectKey", "projectName", new ConnectionInformation(new Uri("http://connected")));

            var slnBindOpMock = new Mock<ISolutionBindingOperation>();
            var nugetMock = new Mock<INuGetBindingOperation>();
            nugetMock.Setup(x => x.InstallPackages(It.IsAny<ISet<Project>>(),
                It.IsAny<IProgress<FixedStepsProgress>>(),
                It.IsAny<CancellationToken>())).Returns(true);
            var bindingInfoProvider = new ConfigurableSolutionBindingInformationProvider();

            var testSubject = new BindingProcessImpl(this.host, bindingArgs, slnBindOpMock.Object, nugetMock.Object, bindingInfoProvider);

            ProjectMock project1 = new ProjectMock("project1") { ProjectKind = ProjectSystemHelper.CSharpProjectKind };
            testSubject.InternalState.BindingProjects.Clear();
            testSubject.InternalState.BindingProjects.Add(project1);

            var progressEvents = new ConfigurableProgressStepExecutionEvents();
            var progressAdapter = new FixedStepsProgressAdapter(progressEvents);
            var cts = new CancellationTokenSource();

            testSubject.InternalState.BindingOperationSucceeded = false;

            // Act
            testSubject.InstallPackages(progressAdapter, cts.Token);

            // Assert
            testSubject.InternalState.BindingOperationSucceeded.Should().BeTrue();
        }

        [TestMethod]
        public void InstallPackages_Succeeds_SuccessPropertyIsFalse()
        {
            // Arrange
            var bindingArgs = new BindCommandArgs("projectKey", "projectName", new ConnectionInformation(new Uri("http://connected")));

            var slnBindOpMock = new Mock<ISolutionBindingOperation>();
            var nugetMock = new Mock<INuGetBindingOperation>();
            nugetMock.Setup(x => x.InstallPackages(It.IsAny<ISet<Project>>(),
                It.IsAny<IProgress<FixedStepsProgress>>(),
                It.IsAny<CancellationToken>())).Returns(false);
            var bindingInfoProvider = new ConfigurableSolutionBindingInformationProvider();

            var testSubject = new BindingProcessImpl(this.host, bindingArgs, slnBindOpMock.Object, nugetMock.Object, bindingInfoProvider);

            ProjectMock project1 = new ProjectMock("project1") { ProjectKind = ProjectSystemHelper.CSharpProjectKind };
            testSubject.InternalState.BindingProjects.Clear();
            testSubject.InternalState.BindingProjects.Add(project1);

            var progressEvents = new ConfigurableProgressStepExecutionEvents();
            var progressAdapter = new FixedStepsProgressAdapter(progressEvents);
            var cts = new CancellationTokenSource();

            testSubject.InternalState.BindingOperationSucceeded = true;

            // Act
            testSubject.InstallPackages(progressAdapter, cts.Token);

            // Assert
            testSubject.InternalState.BindingOperationSucceeded.Should().BeFalse();
        }

        [TestMethod]
        public void EmitBindingCompleteMessage()
        {
            // Arrange
            var testSubject = this.CreateTestSubject();

            // Test case 1: Default state is 'true'
            testSubject.InternalState.BindingOperationSucceeded.Should().BeTrue($"Initial state of {nameof(BindingProcessImpl.InternalState.BindingOperationSucceeded)} should be true");
        }

        [TestMethod]
        public void PromptSaveSolutionIfDirty()
        {
            // Arrange
            var testSubject = this.CreateTestSubject();
            var solution = new SolutionMock();
            serviceProvider.RegisterService(typeof(SVsSolution), solution);

            // Case 1: Users saves the changes
            solution.SaveSolutionElementAction = (options, hierarchy, docCookie) => VSConstants.S_OK;
            // Act
            var result = testSubject.PromptSaveSolutionIfDirty();
            // Assert
            result.Should().BeTrue();
            this.outputWindowPane.AssertOutputStrings(0);

            // Case 2: Users cancels the save
            solution.SaveSolutionElementAction = (options, hierarchy, docCookie) => VSConstants.S_FALSE;
            // Act
            result = testSubject.PromptSaveSolutionIfDirty();
            // Assert
            result.Should().BeFalse();
            this.outputWindowPane.AssertOutputStrings(Strings.SolutionSaveCancelledBindAborted);
        }

        [TestMethod]
        public void SilentSaveSolutionIfDirty()
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
        public void DiscoverProjects_AddsMatchingProjectsToBinding()
        {
            // Arrange
            ThreadHelper.SetCurrentThreadAsUIThread();

            var csProject1 = new ProjectMock("cs1.csproj");
            var csProject2 = new ProjectMock("cs2.csproj");
            csProject1.SetCSProjectKind();
            csProject2.SetCSProjectKind();

            var matchingProjects = new[] { csProject1, csProject2 };
            this.projectSystemHelper.FilteredProjects = matchingProjects;

            var testSubject = this.CreateTestSubject();
            this.host.SupportedPluginLanguages.UnionWith(new[] { Language.CSharp });

            // Act
            var result = testSubject.DiscoverProjects();

            // Assert
            result.Should().BeTrue();
            CollectionAssert.AreEqual(matchingProjects, testSubject.InternalState.BindingProjects.ToArray(), "Unexpected projects selected for binding");
        }

        private void DiscoverProjects_GenericPart(int numberOfProjectsToCreate, int numberOfProjectsToInclude, bool expectedResult)
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
            var result = testSubject.DiscoverProjects();

            // Assert
            result.Should().Be(expectedResult);
            testSubject.InternalState.BindingProjects.Should().HaveCount(numberOfProjectsToInclude, "Expected " + numberOfProjectsToInclude + " project(s) selected for binding");
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
        public void DiscoverProjects_OutputsIncludedProjects()
        {
            // Arrange
            ThreadHelper.SetCurrentThreadAsUIThread();

            // Act & Common Assert
            DiscoverProjects_GenericPart(2, 2, true);
        }

        [TestMethod]
        public void DiscoverProjects_OutputsExcludedProjects()
        {
            // Arrange
            ThreadHelper.SetCurrentThreadAsUIThread();

            // Act & Common Assert
            DiscoverProjects_GenericPart(2, 0, false);
        }

        [TestMethod]
        public void DiscoverProjects_OutputsIncludedAndExcludedProjects()
        {
            // Arrange
            ThreadHelper.SetCurrentThreadAsUIThread();

            // Act & Common Assert
            DiscoverProjects_GenericPart(4, 2, true);
        }

        [TestMethod]
        public void DiscoverProjects_NoMatchingProjects_AbortsWorkflow()
        {
            // Arrange
            ThreadHelper.SetCurrentThreadAsUIThread();

            // Act & Common Assert
            DiscoverProjects_GenericPart(0, 0, false);
        }

        [TestMethod]
        public void GetProjectsForRulesetBinding_FirstBinding_AllProjectsBound()
        {
            // Arrange
            var allProjects = new Project[]
            {
                new ProjectMock("cs1.csproj"),
                new ProjectMock("cs2.csproj"),
                new ProjectMock("cs3.csproj")
            };

            var logger = new TestLogger();
            var bindingInfoProvider = new ConfigurableSolutionBindingInformationProvider
            {
                UnboundProjects = allProjects
            };

            // Act
            var result = BindingProcessImpl.GetProjectsForRulesetBinding(true, allProjects, bindingInfoProvider, logger);

            // Assert
            result.Should().BeEquivalentTo(allProjects);
            logger.AssertOutputStringExists(Strings.Bind_Ruleset_InitialBinding);
        }

        [TestMethod]
        public void GetProjectsForRulesetBinding_NoProjectsUpToDate()
        {
            // Arrange
            var allProjects = new Project[]
            {
                new ProjectMock("cs1.csproj"),
                new ProjectMock("cs2.csproj"),
                new ProjectMock("cs3.csproj")
            };

            var logger = new TestLogger();
            var bindingInfoProvider = new ConfigurableSolutionBindingInformationProvider
            {
                UnboundProjects = allProjects
            };

            // Act
            var result = BindingProcessImpl.GetProjectsForRulesetBinding(false, allProjects, bindingInfoProvider, logger);

            // Assert
            result.Should().BeEquivalentTo(allProjects);
            logger.AssertOutputStringExists(Strings.Bind_Ruleset_AllProjectsNeedToBeUpdated);
        }

        [TestMethod]
        public void InitializeSolutionBinding_Update_NotAllUpToDate_SomeProjectsUpdated()
        {
            // Arrange
            var allProjects = new Project[]
            {
                new ProjectMock("csA.csproj"),
                new ProjectMock("csB.csproj"),
                new ProjectMock("csC.csproj"),
                new ProjectMock("csD.csproj")
            };

            var unboundProjects = new Project[]
            {
                new ProjectMock("XXX.csproj"),
                allProjects[1],
                allProjects[3]
            };

            var logger = new TestLogger();
            var bindingInfoProvider = new ConfigurableSolutionBindingInformationProvider
            {
                UnboundProjects = unboundProjects
            };

            // Act
            var result = BindingProcessImpl.GetProjectsForRulesetBinding(false, allProjects, bindingInfoProvider, logger);

            // Assert
            result.Should().BeEquivalentTo(allProjects[1], allProjects[3]);
            logger.AssertOutputStringExists(Strings.Bind_Ruleset_SomeProjectsDoNotNeedToBeUpdated);
            logger.AssertPartialOutputStringExists("csA.csproj", "csC.csproj");
        }

        [TestMethod]
        public void InitializeSolutionBinding_Update_AllUpToDate_NoProjectsUpdated()
        {
            // Arrange
            var allProjects = new Project[]
            {
                new ProjectMock("cs1.csproj"),
                new ProjectMock("cs2.csproj"),
                new ProjectMock("cs3.csproj"),
                new ProjectMock("cs4.csproj")
            };

            var logger = new TestLogger();
            var bindingInfoProvider = new ConfigurableSolutionBindingInformationProvider
            {
                UnboundProjects = null
            };

            // Act
            var result = BindingProcessImpl.GetProjectsForRulesetBinding(false, allProjects, bindingInfoProvider, logger);

            // Assert
            result.Should().BeEmpty();
            logger.AssertOutputStringExists(Strings.Bind_Ruleset_SomeProjectsDoNotNeedToBeUpdated);
            logger.AssertPartialOutputStringExists("cs1.csproj", "cs2.csproj", "cs3.csproj", "cs4.csproj");
        }

        #endregion Tests

        #region Helpers

        private BindingProcessImpl CreateTestSubject(string projectKey = "anykey", string projectName = "anyname",
            INuGetBindingOperation nuGetBindingOperation = null)
        {
            this.host.SonarQubeService = this.sonarQubeServiceMock.Object;

            var bindingArgs = new BindCommandArgs(projectKey, projectName, new ConnectionInformation(new Uri("http://connected")));

            var slnBindOperation = new SolutionBindingOperation(this.host, bindingArgs.Connection, projectKey, "projectName", SonarLintMode.LegacyConnected, this.host.Logger);
            var bindingInfoProvider = new ConfigurableSolutionBindingInformationProvider();

            if (nuGetBindingOperation == null)
            {
                return new BindingProcessImpl(this.host, bindingArgs, slnBindOperation, new NoOpNuGetBindingOperation(this.host.Logger), bindingInfoProvider);
            }
            return new BindingProcessImpl(this.host, bindingArgs, slnBindOperation, nuGetBindingOperation, bindingInfoProvider);
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

        private static ProjectMock CreateCsProject(string projectName)
        {
            var project = new ProjectMock(projectName);
            project.SetCSProjectKind();
            return project;
        }

        #endregion Helpers
    }
}
