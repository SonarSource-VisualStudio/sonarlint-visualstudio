﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2019 SonarSource SA
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SonarQube.Client.Models;

namespace SonarLint.VisualStudio.Core.Binding
{
    public class CompositeRulesConfigurationProvider : IRulesConfigurationProvider
    {
        private readonly HashSet<IRulesConfigurationProvider> providers;

        public CompositeRulesConfigurationProvider(params IRulesConfigurationProvider[] providers)
        {
            // params args can't be null - will be an empty array
            if (providers.Length == 0)
            {
                throw new ArgumentNullException(nameof(providers));
            }
            if (providers.Any(p => p == null))
            {
                throw new ArgumentNullException(nameof(providers));
            }

            this.providers = new HashSet<IRulesConfigurationProvider>(providers);
        }

        internal /* for testing */ IEnumerable<IRulesConfigurationProvider> Providers { get { return this.providers; } }

        #region IRulesConfigurationProvider methods

        public async Task<IRulesConfigurationFile> GetRulesConfigurationAsync(SonarQubeQualityProfile qualityProfile, string organizationKey, Language language, CancellationToken cancellationToken)
        {
            var provider = Providers.FirstOrDefault(p => p.IsLanguageSupported(language));

            if (provider == null)
            {
                throw new ArgumentOutOfRangeException(nameof(language));
            }
            IRulesConfigurationFile config = null;
            if (provider != null)
            {
                config = await provider?.GetRulesConfigurationAsync(qualityProfile, organizationKey, language, cancellationToken);
            }

            return config;
        }

        public bool IsLanguageSupported(Language language)
        {
            return Providers.Any(p => p.IsLanguageSupported(language));
        }

        #endregion IRulesConfigurationProvider methods
    }
}
