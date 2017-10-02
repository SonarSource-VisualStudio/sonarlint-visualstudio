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

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SonarQube.Client.Models;

namespace SonarLint.VisualStudio.Integration.Connection.UI
{
    /// <summary>
    /// Interaction logic for OrganizationSelectionWindow.xaml
    /// </summary>
    public partial class OrganizationSelectionWindow
    {
        internal OrganizationSelectionWindow(IEnumerable<SonarQubeOrganization> organizations)
        {
            InitializeComponent();

            var sortedOrganizations = organizations.OrderBy(x => x.Name).ToList();
            OrganizationComboBox.ItemsSource = sortedOrganizations;
        }

        private void OnOkButtonClick(object sender, RoutedEventArgs e)
        {
            // Close dialog in the affirmative
            this.DialogResult = true;
        }

        internal SonarQubeOrganization GetSelectedOrganization()
        {
            return OrganizationComboBox?.SelectedItem as SonarQubeOrganization;
        }
    }
}
