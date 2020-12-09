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
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using SonarLint.VisualStudio.Core.Helpers;
using SonarLint.VisualStudio.IssueVisualization.Editor.LocationTagging;
using SonarLint.VisualStudio.IssueVisualization.Models;

namespace SonarLint.VisualStudio.IssueVisualization.Security
{
    internal interface IObservingIssueLocationStore : IIssueLocationStore, IDisposable
    {
        void Register(ObservableCollection<IAnalysisIssueVisualization> issueVisualizations);
        void Unregister(ObservableCollection<IAnalysisIssueVisualization> issueVisualizations);
    }

    [Export(typeof(IObservingIssueLocationStore))]
    [Export(typeof(IIssueLocationStore))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class ObservingIssueLocationStore : IObservingIssueLocationStore
    {
        private IList<ObservableCollection<IAnalysisIssueVisualization>> ObservableIssueVisualizations { get; } = new List<ObservableCollection<IAnalysisIssueVisualization>>();

        public void Register(ObservableCollection<IAnalysisIssueVisualization> issueVisualizations)
        {
            if (issueVisualizations == null)
            {
                throw new ArgumentNullException(nameof(issueVisualizations));
            }

            ObservableIssueVisualizations.Add(issueVisualizations);
            issueVisualizations.CollectionChanged += IssueVisualizations_CollectionChanged;
        }

        public void Unregister(ObservableCollection<IAnalysisIssueVisualization> issueVisualizations)
        {
            if (issueVisualizations == null)
            {
                throw new ArgumentNullException(nameof(issueVisualizations));
            }

            ObservableIssueVisualizations.Remove(issueVisualizations);
            issueVisualizations.CollectionChanged -= IssueVisualizations_CollectionChanged;
        }

        public event EventHandler<IssuesChangedEventArgs> IssuesChanged;

        public IEnumerable<IAnalysisIssueLocationVisualization> GetLocations(string filePath)
        {
            var matchingLocations = ObservableIssueVisualizations
                .SelectMany(issueVizsCollection => issueVizsCollection )
                .SelectMany(issueViz => issueViz.GetAllLocations())
                .Where(locationViz => !string.IsNullOrEmpty(locationViz.CurrentFilePath) &&
                                      PathHelper.IsMatchingPath(locationViz.CurrentFilePath, filePath));

            return matchingLocations;
        }

        public void Refresh(IEnumerable<string> affectedFilePaths)
        {
        }

        private void IssueVisualizations_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var addedItems = e.NewItems ?? Array.Empty<IAnalysisIssueVisualization>();
            var removedItems = e.OldItems ?? Array.Empty<IAnalysisIssueVisualization>();
            var changedItems = addedItems.Cast<IAnalysisIssueVisualization>().Union(removedItems.Cast<IAnalysisIssueVisualization>());

            NotifyIssueChanged(changedItems);
        }

        private void NotifyIssueChanged(IEnumerable<IAnalysisIssueVisualization> changedIssueVizs)
        {
            if (IssuesChanged == null)
            {
                return;
            }

            var filePaths = changedIssueVizs
                .SelectMany(issueViz =>
                    issueViz.GetAllLocations()
                    .Select(x => x.CurrentFilePath)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase));

            if (filePaths.Any())
            {
                IssuesChanged.Invoke(this, new IssuesChangedEventArgs(filePaths));
            }
        }

        public void Dispose()
        {
            foreach (var collection in ObservableIssueVisualizations)
            {
                collection.CollectionChanged -= IssueVisualizations_CollectionChanged;
            }

            ObservableIssueVisualizations.Clear();
        }
    }
}
