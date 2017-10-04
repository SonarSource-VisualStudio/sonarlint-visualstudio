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

using Microsoft.CodeAnalysis;
using SonarLint.VisualStudio.Integration.Suppression;

namespace SonarLint.VisualStudio.Integration.Vsix.Suppression
{
    /// <summary>
    ///     Information about a single Roslyn issue, decorated with the extra information required to map it to a
    ///     SonarQube issue.
    /// </summary>
    internal class LiveIssue
    {
        public LiveIssue(Diagnostic diagnostic, string issueFilePath, string projectGuid, int startLine,
            string wholeLineText)
        {
            Diagnostic = diagnostic;
            IssueFilePath = issueFilePath;
            ProjectGuid = projectGuid;
            StartLine = startLine;
            WholeLineText = wholeLineText;
            LineHash = ChecksumCalculator.Calculate(WholeLineText);
        }

        public Diagnostic Diagnostic { get; }
        public string IssueFilePath { get; }
        public string LineHash { get; }
        public string ProjectGuid { get; }
        public int StartLine { get; }
        public string WholeLineText { get; }
    }
}
