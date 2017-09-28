﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2017 SonarSource SA
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
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.CodeAnalysis.RuleSets;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Persistence;
using SonarLint.VisualStudio.Integration.ProfileConflicts;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    [TestClass]
    public class ConflictsManagerTests
    {
        private ConfigurableServiceProvider serviceProvider;
        private ConfigurableVsProjectSystemHelper projectHelper;
        private ConfigurableSolutionRuleSetsInformationProvider ruleSetInfoProvider;
        private ConfigurableFileSystem fileSystem;
        private ConfigurableSolutionBindingSerializer solutionBinding;
        private ConfigurableRuleSetInspector inspector;
        private ConfigurableVsOutputWindowPane outputWindowPane;
        private DTEMock dte;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInit()
        {
            this.serviceProvider = new ConfigurableServiceProvider();

            this.projectHelper = new ConfigurableVsProjectSystemHelper(this.serviceProvider);
            this.serviceProvider.RegisterService(typeof(IProjectSystemHelper), this.projectHelper);

            this.ruleSetInfoProvider = new ConfigurableSolutionRuleSetsInformationProvider();
            this.ruleSetInfoProvider.SolutionRootFolder = this.TestContext.TestRunDirectory;
            this.serviceProvider.RegisterService(typeof(ISolutionRuleSetsInformationProvider), this.ruleSetInfoProvider);

            this.fileSystem = new ConfigurableFileSystem();
            this.serviceProvider.RegisterService(typeof(IFileSystem), this.fileSystem);

            this.solutionBinding = new ConfigurableSolutionBindingSerializer();
            this.serviceProvider.RegisterService(typeof(ISolutionBindingSerializer), this.solutionBinding);

            this.inspector = new ConfigurableRuleSetInspector();
            this.serviceProvider.RegisterService(typeof(IRuleSetInspector), this.inspector);

            var outputWindow = new ConfigurableVsOutputWindow();
            this.outputWindowPane = outputWindow.GetOrCreateSonarLintPane();
            this.serviceProvider.RegisterService(typeof(SVsOutputWindow), outputWindow);

            this.dte = new DTEMock();
            this.projectHelper.CurrentActiveSolution = new SolutionMock(dte);
        }

        [TestMethod]
        public void ConflictsManager_Ctor()
        {
            Exceptions.Expect<ArgumentNullException>(() => new ConflictsManager(null));

            var testSubject = new ConflictsManager(this.serviceProvider);
            testSubject.Should().NotBeNull("Avoid code analysis warning when testSubject is unused");
        }

        [TestMethod]
        public void ConflictsManager_GetCurrentConflicts_NotBound()
        {
            // Arrange
            var testSubject = new ConflictsManager(this.serviceProvider);
            this.SetValidProjects();
            this.solutionBinding.CurrentBinding = null;

            // Act + Assert
            testSubject.GetCurrentConflicts().Should().BeEmpty("Not expecting any conflicts since solution is not bound");
            this.outputWindowPane.AssertOutputStrings(0);
        }

        [TestMethod]
        public void ConflictsManager_GetCurrentConflicts_NoValidProjects()
        {
            // Arrange
            var testSubject = new ConflictsManager(this.serviceProvider);
            this.SetValidSolutionBinding();

            // Act + Assert
            testSubject.GetCurrentConflicts().Should().BeEmpty("Not expecting any conflicts since there are no projects");
            this.outputWindowPane.AssertOutputStrings(0);
        }

        [TestMethod]
        public void ConflictsManager_GetCurrentConflicts_MissingBaselineFile()
        {
            // Arrange
            var testSubject = new ConflictsManager(this.serviceProvider);
            this.SetValidSolutionBinding();
            this.SetValidProjects();

            // Act + Assert
            testSubject.GetCurrentConflicts().Should().BeEmpty("Not expecting any conflicts since the solution baseline is missing");
            this.outputWindowPane.AssertOutputStrings(1);
            this.outputWindowPane.AssertMessageContainsAllWordsCaseSensitive(0,
                words: new[] { Constants.SonarQubeManagedFolderName },
                splitter: new[] { Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar });
        }

        [TestMethod]
        public void ConflictsManager_GetCurrentConflicts_NoRuleSetDeclaration()
        {
            // Arrange
            var testSubject = new ConflictsManager(this.serviceProvider);
            this.SetValidSolutionBinding();
            this.SetValidProjects();
            this.SetValidSolutionRuleSetPerProjectKind();

            // Act + Assert
            testSubject.GetCurrentConflicts().Should().BeEmpty("Not expecting any conflicts since there are no project rulesets specified");
            this.outputWindowPane.AssertOutputStrings(0);
        }

        [TestMethod]
        public void ConflictsManager_GetCurrentConflicts_NoConflicts()
        {
            // Arrange
            var testSubject = new ConflictsManager(this.serviceProvider);
            this.SetValidSolutionBinding();
            this.SetValidProjects();
            this.SetValidSolutionRuleSetPerProjectKind();
            IEnumerable<RuleSetDeclaration> knownDeclarations = this.SetSingleValidRuleSetDeclarationPerProject();

            bool findConflictsWasCalled = false;
            this.ConfigureInspectorWithConflicts(knownDeclarations, () =>
            {
                findConflictsWasCalled = true;
                return new RuleConflictInfo();
            });

            // Act + Assert
            testSubject.GetCurrentConflicts().Should().BeEmpty("Not expecting any conflicts since there are no conflicts");
            this.outputWindowPane.AssertOutputStrings(0);
            findConflictsWasCalled.Should().BeTrue();
        }

        [TestMethod]
        public void ConflictsManager_GetCurrentConflicts_ExceptionDuringFindConflicts()
        {
            // Arrange
            var testSubject = new ConflictsManager(this.serviceProvider);
            this.SetValidSolutionBinding();
            this.SetValidProjects(2);
            this.SetValidSolutionRuleSetPerProjectKind();
            IEnumerable<RuleSetDeclaration> knownDeclarations = this.SetSingleValidRuleSetDeclarationPerProject();
            int findConflictCalls = 0;
            this.ConfigureInspectorWithConflicts(knownDeclarations, () =>
            {
                findConflictCalls++;
                throw new Exception($"Hello world{findConflictCalls} ");
            });

            // Act + Assert
            int conflicts = 0;
            using (new AssertIgnoreScope()) // Ignore asserts on exception
            {
                conflicts = testSubject.GetCurrentConflicts().Count;
            }

            conflicts.Should().Be(0, "Expect 0 conflicts since failed each time");
            this.outputWindowPane.AssertOutputStrings(2);
            this.outputWindowPane.AssertMessageContainsAllWordsCaseSensitive(0, new[] { "Hello", "world1" });
            this.outputWindowPane.AssertMessageContainsAllWordsCaseSensitive(1, new[] { "Hello", "world2" });
            findConflictCalls.Should().Be(2);
        }

        [TestMethod]
        public void ConflictsManager_GetCurrentConflicts_HasConflicts()
        {
            // Arrange
            var testSubject = new ConflictsManager(this.serviceProvider);
            this.SetValidSolutionBinding();
            this.SetValidProjects(4);
            this.SetValidSolutionRuleSetPerProjectKind();
            IEnumerable<RuleSetDeclaration> knownDeclarations = this.SetSingleValidRuleSetDeclarationPerProject();
            int findConflictCalls = 0;
            this.ConfigureInspectorWithConflicts(knownDeclarations, () =>
            {
                findConflictCalls++;
                return CreateConflictsBasedOnNumberOfCalls(findConflictCalls);
            });

            // Act + Assert
            testSubject.GetCurrentConflicts().Should().HaveCount(3, "Expect 3 conflicts (1st call is not a conflict)");
            this.outputWindowPane.AssertOutputStrings(0);
            findConflictCalls.Should().Be(4);
        }

        [TestMethod]
        public void ConflictsManager_GetCurrentConflicts_HasConflicts_WithoutAggregationApplied()
        {
            // Arrange
            var testSubject = new ConflictsManager(this.serviceProvider);
            this.SetValidSolutionBinding();
            this.SetValidProjects(3);
            this.SetValidSolutionRuleSetPerProjectKind();
            IEnumerable<RuleSetDeclaration> knownDeclarations = this.SetValidRuleSetDeclarationPerProjectAndConfiguration(
                useUniqueProjectRuleSets: true,
                configurationNames: new[] { "Debug", "Release" });

            this.ConfigureInspectorWithConflicts(knownDeclarations, () =>
            {
                RuleSet temp = TestRuleSetHelper.CreateTestRuleSet(1);
                return new RuleConflictInfo(temp.Rules); // Missing rules
            });

            // Act + Assert
            testSubject.GetCurrentConflicts().Should().HaveCount(6, "Expecting 6 conflicts (3 projects x 2 configuration)");
            this.outputWindowPane.AssertOutputStrings(0);
        }

        [TestMethod]
        public void ConflictsManager_GetCurrentConflicts_HasConflicts_WithAggregationApplied()
        {
            // Arrange
            var testSubject = new ConflictsManager(this.serviceProvider);
            this.SetValidSolutionBinding();
            this.SetValidProjects(3);
            this.SetValidSolutionRuleSetPerProjectKind();
            IEnumerable<RuleSetDeclaration> knownDeclarations = this.SetValidRuleSetDeclarationPerProjectAndConfiguration(
              useUniqueProjectRuleSets: false,
              configurationNames: new[] { "Debug", "Release" });

            this.ConfigureInspectorWithConflicts(knownDeclarations, () =>
            {
                RuleSet temp = TestRuleSetHelper.CreateTestRuleSet(1);
                return new RuleConflictInfo(temp.Rules); // Missing rules
            });

            // Act + Assert
            testSubject.GetCurrentConflicts().Should().HaveCount(3, "Expecting 3 conflicts (1 per project) since the ruleset declaration for project configuration are the same");
            this.outputWindowPane.AssertOutputStrings(0);
        }

        #region Helpers

        private void SetValidSolutionBinding()
        {
            this.solutionBinding.CurrentBinding = new BoundSonarQubeProject { ProjectKey = "ProjectKey" };
        }

        private void SetValidProjects(int numberOfProjects = 1)
        {
            List<ProjectMock> projects = new List<ProjectMock>();
            for(int i=0;i< numberOfProjects;i++)
            {
                ProjectMock project = this.dte.Solution.AddOrGetProject($@"X:\Solution\Project\Project{i}.csproj");
                project.SetCSProjectKind();
                projects.Add(project);
            }

            this.projectHelper.FilteredProjects = projects;
        }

        private void SetValidSolutionRuleSetPerProjectKind()
        {
            ISolutionRuleSetsInformationProvider rsInfoProvider = this.ruleSetInfoProvider;
            foreach (var project in this.projectHelper.FilteredProjects)
            {
                string solutionRuleSet = rsInfoProvider.CalculateSolutionSonarQubeRuleSetFilePath(
                    this.solutionBinding.CurrentBinding.ProjectKey,
                    Language.ForProject(project));
                this.fileSystem.RegisterFile(solutionRuleSet);
            }
        }

        private IEnumerable<RuleSetDeclaration> SetSingleValidRuleSetDeclarationPerProject(string configurationName = "Configuration", bool useUniqueProjectRuleSets = false)
        {
            List<RuleSetDeclaration> declarations = new List<RuleSetDeclaration>();

            foreach (ProjectMock project in this.projectHelper.FilteredProjects.OfType<ProjectMock>())
            {
                var configuration = new ConfigurationMock(configurationName);
                project.ConfigurationManager.Configurations.Add(configuration);

                PropertyMock ruleSetProperty = configuration.Properties.RegisterKnownProperty(Constants.CodeAnalysisRuleSetPropertyKey);
                ruleSetProperty.Value = project.FilePath.ToUpperInvariant(); // Catch cases where file paths are compared without OrdinalIgnoreCase
                if (useUniqueProjectRuleSets)
                {
                    ruleSetProperty.Value = Path.ChangeExtension(project.FilePath, configurationName + ".ruleSet");
                }

                PropertyMock ruleSetDirectories = configuration.Properties.RegisterKnownProperty(Constants.CodeAnalysisRuleSetDirectoriesPropertyKey);
                ruleSetDirectories.Value = ConflictsManager.CombineDirectories(new[] { this.TestContext.TestRunDirectory, "." });

                RuleSetDeclaration declaration = new RuleSetDeclaration(
                    project,
                    ruleSetProperty,
                    ruleSetProperty.Value.ToString().ToLowerInvariant(), // Catch cases where file paths are compared without OrdinalIgnoreCase
                    configuration.ConfigurationName,
                    ruleSetDirectories.Value.ToString().Split(';'));

                declarations.Add(declaration);

                this.ruleSetInfoProvider.RegisterProjectInfo(project, declaration);
            }

            return declarations;
        }

        private IEnumerable<RuleSetDeclaration> SetValidRuleSetDeclarationPerProjectAndConfiguration(bool useUniqueProjectRuleSets, params string[] configurationNames)
        {
            List<RuleSetDeclaration> declarations = new List<RuleSetDeclaration>();

            foreach (string configurationName in configurationNames)
            {
                declarations.AddRange(this.SetSingleValidRuleSetDeclarationPerProject(configurationName, useUniqueProjectRuleSets));
            }

            return declarations;
        }

        private void ConfigureInspectorWithConflicts(IEnumerable<RuleSetDeclaration> knownDeclarations, Func<RuleConflictInfo> conflictFactory)
        {
            this.inspector.FindConflictingRulesAction = (baseline, ruleset, directories) =>
            {
                baseline.Should().NotBeNull();
                ruleset.Should().NotBeNull();
                this.fileSystem.files.Should().ContainKey(baseline);

                knownDeclarations.Any(dec =>
                {
                    if (dec.RuleSetPath == ruleset)
                    {
                        return new HashSet<string>(dec.RuleSetDirectories).SetEquals(directories);
                    }

                    return false;
                }).Should().BeTrue();

                return conflictFactory.Invoke();
            };
        }

        private static RuleConflictInfo CreateConflictsBasedOnNumberOfCalls(int findConflictCalls)
        {
            RuleSet temp = TestRuleSetHelper.CreateTestRuleSet(2);
            Dictionary<RuleReference, RuleAction> map = new Dictionary<RuleReference, RuleAction>();

            switch (findConflictCalls)
            {
                case 1:
                    return new RuleConflictInfo(); // No conflicts

                case 2:
                    return new RuleConflictInfo(temp.Rules); // Missing rules

                case 3:
                    map[temp.Rules[0]] = RuleAction.Error;
                    return new RuleConflictInfo(map); // Weakened rules

                case 4:
                    map[temp.Rules[0]] = RuleAction.Error;
                    return new RuleConflictInfo(temp.Rules.Skip(1), map); // One of each

                default:
                    FluentAssertions.Execution.Execute.Assertion.FailWith("Called unexpected number of times");
                    return null;
            }
        }

        #endregion Helpers
    }
}