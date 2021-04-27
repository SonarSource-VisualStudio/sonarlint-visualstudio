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

using System.Collections.Generic;
using System.ComponentModel.Composition;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.TypeScript.EslintBridgeClient.Contract;

namespace SonarLint.VisualStudio.TypeScript.Rules
{
    interface IActiveJavaScriptRulesProvider
    {
        /// <summary>
        /// Returns the eslint configuration for the currently active rules
        /// </summary>
        IEnumerable<Rule> Get();
    }

    interface IActiveTypeScriptRulesProvider
    {
        /// <summary>
        /// Returns the eslint configuration for the currently active rules
        /// </summary>
        IEnumerable<Rule> Get();
    }

    [Export(typeof(IActiveJavaScriptRulesProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class ActiveJavaScriptRulesProvider : IActiveJavaScriptRulesProvider
    {
        private readonly ActiveRulesCalculator calc;

        [ImportingConstructor]
        public ActiveJavaScriptRulesProvider(IJavaScriptRuleDefinitionsProvider jsRuleDefinitions,
            IUserSettingsProvider userSettingsProvider)
        {
            calc = new ActiveRulesCalculator(jsRuleDefinitions.GetDefinitions(), userSettingsProvider);
        }

        public IEnumerable<Rule> Get() => calc.Get();
    }

    [Export(typeof(IActiveTypeScriptRulesProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class ActiveTypeScriptRulesProvider : IActiveTypeScriptRulesProvider
    {
        private readonly ActiveRulesCalculator calc;

        [ImportingConstructor]
        public ActiveTypeScriptRulesProvider(ITypeScriptRuleDefinitionsProvider tsRuleDefinitions,
            IUserSettingsProvider userSettingsProvider)
        {
            calc = new ActiveRulesCalculator(tsRuleDefinitions.GetDefinitions(), userSettingsProvider);
        }

        public IEnumerable<Rule> Get() => calc.Get();
    }
}
