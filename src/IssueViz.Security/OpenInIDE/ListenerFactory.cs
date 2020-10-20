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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNUZ
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Net;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Integration;

namespace SonarLint.VisualStudio.IssueVisualization.Security.OpenInIDE
{
    internal interface IListenerFactory
    {
        /// <summary>
        /// Attempts to create and return a new HTTP listener listening
        /// on the first available port in the specified range.
        /// Returns null if there is not a free port in the range.
        /// </summary>
        HttpListener Create(int startPort, int endPort);
    }

    internal class ListenerFactory : IListenerFactory
    {
        private readonly ILogger logger;

        public ListenerFactory(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        HttpListener IListenerFactory.Create(int startPort, int endPort)
        {
            logger.WriteLine(OpenInIDEResources.Factory_CreatingListener);
            HttpListener listener = null;

            for (int port = startPort; port <= endPort; port++)
            {
                logger.WriteLine(OpenInIDEResources.Factory_CheckingPort, port);
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                try
                {
                    listener.Start();
                    logger.WriteLine(OpenInIDEResources.Factory_Succeeded, port);
                    break;
                }
                catch(Exception ex)  when (!ErrorHandler.IsCriticalException(ex))
                {
                    logger.WriteLine(OpenInIDEResources.Factory_PortIsUnavailable, port);
                    listener = null;
                }
            }

            if (listener == null)
            {
                logger.WriteLine(OpenInIDEResources.Factory_Failed_NoAvailablePorts);
            }
            return listener;
        }
    }
}
