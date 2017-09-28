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

using System;
using FluentAssertions;
using SonarLint.VisualStudio.Integration.Persistence;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    internal class ConfigurableSolutionBindingSerializer : ISolutionBindingSerializer
    {
        internal int WrittenFilesCount { get; private set; }

        #region ISolutionBindingSerializer

        BoundSonarQubeProject ISolutionBindingSerializer.ReadSolutionBinding()
        {
            this.ReadSolutionBindingAction?.Invoke();
            return this.CurrentBinding;
        }

        string ISolutionBindingSerializer.WriteSolutionBinding(BoundSonarQubeProject binding)
        {
            binding.Should().NotBeNull("Required argument");

            string filePath = this.WriteSolutionBindingAction?.Invoke(binding) ?? binding.ProjectKey;
            this.WrittenFilesCount++;

            return filePath;
        }

        #endregion ISolutionBindingSerializer

        #region Test helpers

        public BoundSonarQubeProject CurrentBinding { get; set; }

        public Func<BoundSonarQubeProject, string> WriteSolutionBindingAction { get; set; }

        public Action ReadSolutionBindingAction { get; set; }

        #endregion Test helpers
    }
}