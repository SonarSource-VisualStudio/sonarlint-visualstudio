﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2021 SonarSource SA
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
using SonarLint.VisualStudio.Core.Analysis;
using SonarLint.VisualStudio.TypeScript.EslintBridgeClient.Contract;
using SonarLint.VisualStudio.TypeScript.Rules;

namespace SonarLint.VisualStudio.TypeScript.Analyzer
{
    internal interface IEslintBridgeIssueConverter
    {
        IAnalysisIssue Convert(string filePath, Issue issue);
    }

    internal class EslintBridgeIssueConverter : IEslintBridgeIssueConverter
    {
        private readonly IRulesProvider rulesProvider;

        public EslintBridgeIssueConverter(IRulesProvider rulesProvider)
        {
            this.rulesProvider = rulesProvider;
        }

        public IAnalysisIssue Convert(string filePath, Issue issue)
        {
            var ruleDefinitions = rulesProvider.GetDefinitions();
            var ruleDefinition = ruleDefinitions.Single(x => x.EslintKey.Equals(issue.RuleId, StringComparison.OrdinalIgnoreCase));
            var sonarRuleKey = ruleDefinition.RuleKey;

            return new AnalysisIssue(
                sonarRuleKey,
                Convert(ruleDefinition.Severity),
                Convert(ruleDefinition.Type),
                issue.Message,
                filePath,
                issue.Line,
                issue.EndLine, // todo: do we need to handle EndLine=0?
                issue.Column,
                issue.EndColumn,
                null,
                Convert(filePath, issue.SecondaryLocations));
        }

        internal static /* for testing */ AnalysisIssueSeverity Convert(RuleSeverity ruleSeverity)
        {
            switch (ruleSeverity)
            {
                case RuleSeverity.BLOCKER:
                    return AnalysisIssueSeverity.Blocker;
                case RuleSeverity.CRITICAL:
                    return AnalysisIssueSeverity.Critical;
                case RuleSeverity.INFO:
                    return AnalysisIssueSeverity.Info;
                case RuleSeverity.MAJOR:
                    return AnalysisIssueSeverity.Major;
                case RuleSeverity.MINOR:
                    return AnalysisIssueSeverity.Minor;

                default:
                    throw new ArgumentOutOfRangeException(nameof(ruleSeverity));
            }
        }

        internal static /* for testing */ AnalysisIssueType Convert(RuleType ruleType)
        {
            switch (ruleType)
            {
                case RuleType.BUG:
                    return AnalysisIssueType.Bug;
                case RuleType.CODE_SMELL:
                    return AnalysisIssueType.CodeSmell;
                case RuleType.VULNERABILITY:
                    return AnalysisIssueType.Vulnerability;

                default:
                    throw new ArgumentOutOfRangeException(nameof(ruleType));
            }
        }

        private IReadOnlyList<IAnalysisIssueFlow> Convert(string filePath, IssueLocation[] issueLocations)
        {
            var locations = issueLocations?.Select(x => Convert(filePath, x));

            return locations == null || !locations.Any()
                ? Array.Empty<IAnalysisIssueFlow>()
                : new[] {new AnalysisIssueFlow(locations.ToArray())};
        }

        private IAnalysisIssueLocation Convert(string filePath, IssueLocation issueLocation) =>
            new AnalysisIssueLocation(
                issueLocation.Message,
                filePath,
                issueLocation.Line,
                issueLocation.EndLine,
                issueLocation.Column,
                issueLocation.EndColumn,
                null);
    }
}
