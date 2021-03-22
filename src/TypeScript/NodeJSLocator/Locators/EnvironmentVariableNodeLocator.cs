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
using SonarLint.VisualStudio.Integration;

namespace SonarLint.VisualStudio.TypeScript.NodeJSLocator.Locators
{
    internal class EnvironmentVariableNodeLocator : INodeLocator
    {
        internal const string NodeJsPathEnvVar = "SONAR_NODEJS_PATH";

        private readonly ILogger logger;

        public EnvironmentVariableNodeLocator(ILogger logger)
        {
            this.logger = logger;
        }

        public string Locate()
        {
            logger.WriteLine(Resources.INFO_CheckingEnvVar, NodeJsPathEnvVar);
            var nodeExePath = Environment.GetEnvironmentVariable(NodeJsPathEnvVar);

            if (string.IsNullOrEmpty(nodeExePath))
            {
                logger.WriteLine(Resources.INFO_NoEnvVar);
                return null;
            }

            logger.WriteLine(Resources.INFO_EnvVarValue, nodeExePath);
            return nodeExePath;
        }
    }
}
