﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2020 SonarSource SA
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
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Core.Binding;
using SonarLint.VisualStudio.Integration;
using SonarQube.Client;

namespace SonarLint.VisualStudio.IssueVisualization.Security.Taint
{
    internal interface ITaintIssuesSynchronizer
    {
        /// <summary>
        /// Fetches taint vulnerabilities from the server, converts them into visualizations and populates <see cref="ITaintStore"/>.
        /// </summary>
        Task Sync();
    }

    [Export(typeof(ITaintIssuesSynchronizer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class TaintIssuesSynchronizer : ITaintIssuesSynchronizer
    {
        private readonly ITaintStore taintStore;
        private readonly ISonarQubeService sonarQubeService;
        private readonly ITaintIssueToIssueVisualizationConverter converter;
        private readonly IConfigurationProvider configurationProvider;
        private readonly ILogger logger;

        [ImportingConstructor]
        public TaintIssuesSynchronizer(ITaintStore taintStore,
            ISonarQubeService sonarQubeService,
            ITaintIssueToIssueVisualizationConverter converter,
            IConfigurationProvider configurationProvider,
            ILogger logger)
        {
            this.taintStore = taintStore;
            this.sonarQubeService = sonarQubeService;
            this.converter = converter;
            this.configurationProvider = configurationProvider;
            this.logger = logger;
        }

        public async Task Sync()
        {
            var bindingConfiguration = configurationProvider.GetConfiguration();

            if (bindingConfiguration.Mode == SonarLintMode.Standalone)
            {
                logger.WriteLine(TaintResources.Synchronizer_NotInConnectedMode);
                return;
            }
            try
            {
                var projectKey = bindingConfiguration.Project.ProjectKey;
                var taintVulnerabilities = await sonarQubeService.GetTaintVulnerabilitiesAsync(projectKey, CancellationToken.None);

                logger.WriteLine(TaintResources.Synchronizer_NumberOfServerIssues, taintVulnerabilities.Count);

                var taintIssueVizs = taintVulnerabilities.Select(converter.Convert).ToList();
                taintStore.Set(taintIssueVizs);
            }
            catch (Exception ex) when (!ErrorHandler.IsCriticalException(ex))
            {
                logger.WriteLine(TaintResources.Synchronizer_Failure, ex);
            }
        }
    }
}
