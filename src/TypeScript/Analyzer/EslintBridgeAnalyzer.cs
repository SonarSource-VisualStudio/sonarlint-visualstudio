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
using System.Threading;
using System.Threading.Tasks;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Core.Analysis;
using SonarLint.VisualStudio.Integration;
using SonarLint.VisualStudio.TypeScript.EslintBridgeClient;
using SonarLint.VisualStudio.TypeScript.EslintBridgeClient.Contract;
using SonarLint.VisualStudio.TypeScript.Rules;

namespace SonarLint.VisualStudio.TypeScript.Analyzer
{
    internal interface IEslintBridgeAnalyzer : IDisposable
    {
        Task<IReadOnlyCollection<IAnalysisIssue>> Analyze(string filePath, string tsConfig, CancellationToken cancellationToken);
    }

    internal sealed class EslintBridgeAnalyzer : IEslintBridgeAnalyzer
    {
        private readonly EventWaitHandle serverInitLocker = new EventWaitHandle(true, EventResetMode.AutoReset);

        private readonly IRulesProvider rulesProvider;
        private readonly IEslintBridgeClient eslintBridgeClient;
        private readonly IActiveSolutionTracker activeSolutionTracker;
        private readonly IAnalysisConfigMonitor analysisConfigMonitor;
        private readonly IEslintBridgeIssueConverter issueConverter;
        private readonly ILogger logger;

        private bool shouldInitLinter = true;

        public EslintBridgeAnalyzer(
            IRulesProvider rulesProvider,
            IEslintBridgeClient eslintBridgeClient,
            IActiveSolutionTracker activeSolutionTracker,
            IAnalysisConfigMonitor analysisConfigMonitor,
            IEslintBridgeIssueConverter issueConverter,
            ILogger logger)
        {
            this.rulesProvider = rulesProvider;
            this.eslintBridgeClient = eslintBridgeClient;
            this.activeSolutionTracker = activeSolutionTracker;
            this.analysisConfigMonitor = analysisConfigMonitor;
            this.issueConverter = issueConverter;
            this.logger = logger;

            activeSolutionTracker.ActiveSolutionChanged += ActiveSolutionTracker_ActiveSolutionChanged;
            analysisConfigMonitor.ConfigChanged += AnalysisConfigMonitor_ConfigChanged;
        }

        public async Task<IReadOnlyCollection<IAnalysisIssue>> Analyze(string filePath, string tsConfig, CancellationToken cancellationToken)
        {
            try
            {
                return await GetFileIssues(filePath, tsConfig, cancellationToken);
            }
            catch (EslintBridgeClientNotInitializedException)
            {
                RequireLinterUpdate();
                return await GetFileIssues(filePath, tsConfig, cancellationToken);
            }
        }

        private async Task<IReadOnlyCollection<IAnalysisIssue>> GetFileIssues(string filePath, string tsConfig, CancellationToken cancellationToken)
        {
            await EnsureEslintBridgeClientIsInitialized(rulesProvider.GetActiveRulesConfiguration(), cancellationToken);

            var analysisResponse = await eslintBridgeClient.Analyze(filePath, tsConfig, cancellationToken);

            if (analysisResponse.ParsingError != null)
            {
                LogParsingError(filePath, analysisResponse.ParsingError);
                return Array.Empty<IAnalysisIssue>();
            }

            if (analysisResponse.Issues == null)
            {
                return Array.Empty<IAnalysisIssue>();
            }

            var issues = ConvertIssues(filePath, analysisResponse.Issues);

            return issues;
        }

        private async Task EnsureEslintBridgeClientIsInitialized(IEnumerable<Rule> activeRules, CancellationToken cancellationToken)
        {
            try
            {
                serverInitLocker.WaitOne();

                if (shouldInitLinter)
                {
                    await eslintBridgeClient.InitLinter(activeRules, cancellationToken);
                    shouldInitLinter = false;
                }
            }
            finally
            {
                serverInitLocker.Set();
            }
        }

        public void Dispose()
        {
            activeSolutionTracker.ActiveSolutionChanged -= ActiveSolutionTracker_ActiveSolutionChanged;
            analysisConfigMonitor.ConfigChanged -= AnalysisConfigMonitor_ConfigChanged;

            serverInitLocker?.Dispose();
        }

        private async void ActiveSolutionTracker_ActiveSolutionChanged(object sender, ActiveSolutionChangedEventArgs e)
        {
            await StopServer();
        }

        private void AnalysisConfigMonitor_ConfigChanged(object sender, EventArgs e)
        {
            RequireLinterUpdate();
        }

        private async Task StopServer()
        {
            RequireLinterUpdate();
            await eslintBridgeClient.Close();
        }

        private void RequireLinterUpdate()
        {
            serverInitLocker.WaitOne();
            shouldInitLinter = true;
            serverInitLocker.Set();
        }

        /// <summary>
        /// Java version: https://github.com/SonarSource/SonarJS/blob/1916267988093cb5eb1d0b3d74bb5db5c0dbedec/sonar-javascript-plugin/src/main/java/org/sonar/plugins/javascript/eslint/AbstractEslintSensor.java#L134
        /// </summary>
        private void LogParsingError(string path, ParsingError parsingError)
        {
            if (parsingError.Code == ParsingErrorCode.MISSING_TYPESCRIPT)
            {
                logger.WriteLine(Resources.ERR_ParsingError_MissingTypescript);
            }
            else if (parsingError.Code == ParsingErrorCode.UNSUPPORTED_TYPESCRIPT)
            {
                logger.WriteLine(parsingError.Message);
                logger.WriteLine(Resources.ERR_ParsingError_UnsupportedTypescript);
            }
            else
            {
                logger.WriteLine(Resources.ERR_ParsingError_General, path, parsingError.Line, parsingError.Code, parsingError.Message);
            }
        }

        private IReadOnlyCollection<IAnalysisIssue> ConvertIssues(string filePath, IEnumerable<Issue> analysisResponseIssues) =>
            analysisResponseIssues.Select(x => issueConverter.Convert(filePath, x)).ToList();
    }
}
