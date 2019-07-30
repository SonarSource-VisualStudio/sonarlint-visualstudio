﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2018 SonarSource SA
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
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio;
using SonarLint.VisualStudio.Integration.Vsix.Resources;

namespace SonarLint.VisualStudio.Integration.Vsix
{
    /// <summary>
    /// Interaction logic for GeneralOptionsDialogControl.xaml
    /// </summary>
    public partial class GeneralOptionsDialogControl : UserControl
    {
        private readonly ISonarLintSettings settings;
        private readonly ISonarLintDaemon daemon;
        private readonly ILogger logger;

        public GeneralOptionsDialogControl(ISonarLintSettings settings, ISonarLintDaemon daemon, ILogger logger)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            if (daemon == null)
            {
                throw new ArgumentNullException(nameof(daemon));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            this.settings = settings;
            this.daemon = daemon;
            this.logger = logger;

            InitializeComponent();
        }

        protected override void OnInitialized(EventArgs e)
        {
            try
            {
                base.OnInitialized(e);

                if (!daemon.IsInstalled)
                {
                    settings.IsActivateMoreEnabled = false;
                }

                UpdateActiveMoreControls();
            }
            catch (Exception ex) when (!ErrorHandler.IsCriticalException(ex))
            {
                logger.WriteLine(Strings.ERROR_ConfiguringDaemon, ex);
            }
        }

        private void UpdateActiveMoreControls()
        {
            if (settings.IsActivateMoreEnabled)
            {
                ActivateButton.Visibility = Visibility.Collapsed;
                ActivateText.Visibility = Visibility.Collapsed;
                DeactivateButton.Visibility = Visibility.Visible;
                DeactivateText.Visibility = Visibility.Visible;
                VerbosityPanel.Visibility = Visibility.Visible;
                
            }
            else
            {
                ActivateButton.Visibility = Visibility.Visible;
                ActivateText.Visibility = Visibility.Visible;
                DeactivateButton.Visibility = Visibility.Collapsed;
                DeactivateText.Visibility = Visibility.Collapsed;
                VerbosityPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void OnActivateMoreClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!daemon.IsInstalled)
                {
                    new SonarLintDaemonInstaller(settings, daemon, logger).Show(UpdateActiveMoreControls);
                    return;
                }
                
                if (!daemon.IsRunning)
                {
                    daemon.Start();
                }
                settings.IsActivateMoreEnabled = true;

                UpdateActiveMoreControls();
            }
            catch (Exception ex) when (!ErrorHandler.IsCriticalException(ex))
            {
                logger.WriteLine(Strings.ERROR_ConfiguringDaemon, ex);
            }
        }

        private void OnDeactivateClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (daemon.IsRunning)
                {
                    daemon.Stop();
                }
                settings.IsActivateMoreEnabled = false;

                UpdateActiveMoreControls();
            }
            catch (Exception ex) when (!ErrorHandler.IsCriticalException(ex))
            {
                logger.WriteLine(Strings.ERROR_ConfiguringDaemon, ex);
            }
        }
    }
}
