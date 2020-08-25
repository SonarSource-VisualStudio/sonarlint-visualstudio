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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using SonarLint.VisualStudio.IssueVisualization.Editor;
using SonarLint.VisualStudio.IssueVisualization.Models;

namespace SonarLint.VisualStudio.IssueVisualization.Selection
{
    internal interface IAnalysisIssueNavigation
    {
        void GotoNextNavigableLocation();
        void GotoPreviousNavigableLocation();
    }

    [Export(typeof(IAnalysisIssueNavigation))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class AnalysisIssueNavigation : IAnalysisIssueNavigation
    {
        private readonly IAnalysisIssueSelectionService selectionService;
        private readonly ILocationNavigator locationNavigator;

        [ImportingConstructor]
        public AnalysisIssueNavigation(IAnalysisIssueSelectionService selectionService, ILocationNavigator locationNavigator)
        {
            this.selectionService = selectionService;
            this.locationNavigator = locationNavigator;
        }

        public void GotoNextNavigableLocation()
        {
            NavigateToMatchingLocation(
                locations => locations.OrderBy(x => x.StepNumber),
                (location, currentLocation) => location.StepNumber > currentLocation.StepNumber);
        }

        public void GotoPreviousNavigableLocation()
        {
            NavigateToMatchingLocation(
                locations => locations.OrderByDescending(x => x.StepNumber),
                (location, currentLocation) => location.StepNumber < currentLocation.StepNumber);
        }

        private void NavigateToMatchingLocation(Func<IReadOnlyList<IAnalysisIssueLocationVisualization>, IOrderedEnumerable<IAnalysisIssueLocationVisualization>> order, Func<IAnalysisIssueLocationVisualization, IAnalysisIssueLocationVisualization, bool> match)
        {
            var currentLocation = selectionService.SelectedLocation;

            if (currentLocation == null)
            {
                return;
            }

            var currentFlow = selectionService.SelectedFlow;

            if (currentFlow == null)
            {
                return;
            }

            var navigableLocations = order(currentFlow.Locations)
                .Where(x =>
                    x.IsNavigable &&
                    match(x, currentLocation));

            foreach (var locationViz in navigableLocations)
            {
                locationViz.IsNavigable = locationNavigator.TryNavigate(locationViz.Location);

                if (locationViz.IsNavigable)
                {
                    selectionService.SelectedLocation = locationViz;
                    break;
                }
            }
        }
    }
}
