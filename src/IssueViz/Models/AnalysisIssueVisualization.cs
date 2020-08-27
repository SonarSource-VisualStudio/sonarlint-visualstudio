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

using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using SonarLint.VisualStudio.Core.Analysis;

namespace SonarLint.VisualStudio.IssueVisualization.Models
{
    internal interface IAnalysisIssueVisualization : IAnalysisIssueLocationVisualization
    {
        IReadOnlyList<IAnalysisIssueFlowVisualization> Flows { get; }

        IAnalysisIssue Issue { get; }
    }

    internal class AnalysisIssueVisualization : IAnalysisIssueVisualization
    {
        public AnalysisIssueVisualization(IReadOnlyList<IAnalysisIssueFlowVisualization> flows, IAnalysisIssue issue)
        {
            Flows = flows;
            Issue = issue;
        }

        public IReadOnlyList<IAnalysisIssueFlowVisualization> Flows { get; }
        public IAnalysisIssue Issue { get; }
        public int StepNumber => 0;
        public bool IsNavigable { get; }
        public IAnalysisIssueLocation Location => Issue;
        public SnapshotSpan? Span { get; set; }
    }
}
