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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using SonarLint.VisualStudio.IssueVisualization.Security.Commands;
using SonarLint.VisualStudio.IssueVisualization.Security.UI.HotspotsList;
using SonarLint.VisualStudio.IssueVisualization.Security.UI.TaintList;
using Task = System.Threading.Tasks.Task;

namespace SonarLint.VisualStudio.IssueVisualization.Security
{
    [ExcludeFromCodeCoverage]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid("D7D54E08-45E1-49A6-AA53-AF1CFAA6EBDC")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(HotspotsToolWindow), MultiInstances = false, Transient = true, Style = VsDockStyle.Tabbed, Window = VsWindowKindErrorList, Width = 700, Height = 250)]
    [ProvideToolWindow(typeof(TaintToolWindow), MultiInstances = false, Transient = true, Style = VsDockStyle.Tabbed, Window = VsWindowKindErrorList, Width = 700, Height = 250)]
    public sealed class IssueVizSecurityPackage : AsyncPackage
    {
        /// <summary>
        /// https://docs.microsoft.com/en-us/dotnet/api/envdte80.windowkinds.vswindowkinderrorlist?view=visualstudiosdk-2019
        /// </summary>
        public const string VsWindowKindErrorList = "{D78612C7-9962-4B83-95D9-268046DAD23A}";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await HotspotsToolWindowCommand.InitializeAsync(this);
            await TaintToolWindowCommand.InitializeAsync(this);
        }

        protected override WindowPane InstantiateToolWindow(Type toolWindowType)
        {
            if (toolWindowType == typeof(HotspotsToolWindow))
            {
                return new HotspotsToolWindow(this);
            }

            if (toolWindowType == typeof(TaintToolWindow))
            {
                return new TaintToolWindow(this);
            }

            return base.InstantiateToolWindow(toolWindowType);
        }
    }
}
