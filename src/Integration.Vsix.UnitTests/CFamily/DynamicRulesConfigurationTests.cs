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
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Integration.Vsix;
using SonarLint.VisualStudio.Integration.Vsix.CFamily;

namespace SonarLint.VisualStudio.Integration.UnitTests.CFamily
{
    [TestClass]
    public class DynamicRulesConfigurationTests
    {
        [TestMethod]
        public void ActiveRulesMergedCorrectly()
        {
            // Arrange
            var defaultConfig = new DummyRulesConfiguration
            {
                LanguageKey = "c",
                RuleKeyToActiveMap = new Dictionary<string, bool>
                {
                    // List of known rules
                    { "rule1", false /* off by default */ },
                    { "rule2", true },
                    { "rule3", true }
                }
            };

            var userSettings = new UserSettings
            {
                Rules = new Dictionary<string, RuleConfig>
                {
                    // Unknown rules should be ignored
                    { "x:unknown1", new RuleConfig { Level = RuleLevel.On } },

                    // Turn on a rule that was off (case-insensitive comparison on keys)
                    { "c:rule1", new RuleConfig { Level = RuleLevel.On } },

                    // Turn off a rule that was on
                    { "c:rule2", new RuleConfig { Level = RuleLevel.Off} },

                    // Rule key comparison is case-sensitive
                    { "c:RULE3", new RuleConfig { Level = RuleLevel.Off} },

                    // Settings for other languages should be ignored
                    { "cpp:rule3", new RuleConfig { Level = RuleLevel.Off } }
                }
            };

            // Act
            var dynamicConfig = DynamicRulesConfiguration.CalculateActiveRules(defaultConfig, userSettings);

            // Assert
            dynamicConfig.Should().BeEquivalentTo("rule1", "rule3");
        }

        [TestMethod]
        public void ActiveRules_EmptyUserSettings_ReturnsDefaultActive()
        {
            // Arrange
            var defaultConfig = new DummyRulesConfiguration
            {
                RuleKeyToActiveMap = new Dictionary<string, bool>
                {
                    // List of known rules
                    { "rule1", true },
                    { "rule2", true }
                }
            };

            // Act
            var dynamicConfig = DynamicRulesConfiguration.CalculateActiveRules(defaultConfig, new UserSettings());
            dynamicConfig.Should().BeEquivalentTo("rule1", "rule2");
        }

        [TestMethod]
        public void Ctor_NullArguments()
        {
            var userSettings = new UserSettings();

            Action act = () => new DynamicRulesConfiguration(null, userSettings);
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("defaultRulesConfig");

            // Null settings should be ok
            act = () => new DynamicRulesConfiguration(new DummyRulesConfiguration(), null);
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("userSettings");
        }

        [TestMethod]
        public void Ctor_NoSettings_DefaultsUsed()
        {
            // Arrange
            var defaultConfig = new DummyRulesConfiguration
            {
                LanguageKey = "123",
                RuleKeyToActiveMap = new Dictionary<string, bool>
                {
                    { "rule1", true }
                },
                RulesParameters = new Dictionary<string, IDictionary<string, string>>(),
                RulesMetadata = new Dictionary<string, RulesLoader.RuleMetadata>()
            };

            // Act
            var dynamicConfig = new DynamicRulesConfiguration(defaultConfig, new UserSettings());

            // Assert
            dynamicConfig.ActivePartialRuleKeys.Should().BeEquivalentTo("rule1");

            dynamicConfig.LanguageKey.Should().Be("123");

            // Other properties should be pass-throughs
            dynamicConfig.AllPartialRuleKeys.Should().BeSameAs(defaultConfig.AllPartialRuleKeys);
            dynamicConfig.RulesParameters.Should().BeSameAs(defaultConfig.RulesParameters);
            dynamicConfig.RulesMetadata.Should().BeSameAs(defaultConfig.RulesMetadata);
        }

        [TestMethod]
        public void Ctor_SettingsExist_SettingsApplied()
        {
            // Arrange
            var defaultConfig = new DummyRulesConfiguration
            {
                LanguageKey = "cpp",
                RuleKeyToActiveMap = new Dictionary<string, bool>
                {
                    { "rule1", true }, { "rule2", true }, { "rule3", false }, { "rule4", false }
                },
                RulesParameters = new Dictionary<string, IDictionary<string, string>>(),
                RulesMetadata = new Dictionary<string, RulesLoader.RuleMetadata>()
            };

            var userSettingsData = @"{
    'sonarlint.rules': {
        'cpp:rule2': {
            'level': 'off'
        },
        'cpp:rule4': {
            'level': 'on'
        }
    }
}
";
            var userSettings = (UserSettings)JsonConvert.DeserializeObject(userSettingsData, typeof(UserSettings));

            // Act
            var dynamicConfig = new DynamicRulesConfiguration(defaultConfig, userSettings);

            // Assert
            dynamicConfig.ActivePartialRuleKeys.Should().BeEquivalentTo("rule1", "rule4");

            dynamicConfig.LanguageKey.Should().Be("cpp");

            // Other properties should be pass-throughs
            dynamicConfig.AllPartialRuleKeys.Should().BeSameAs(defaultConfig.AllPartialRuleKeys);
            dynamicConfig.RulesParameters.Should().BeSameAs(defaultConfig.RulesParameters);
            dynamicConfig.RulesMetadata.Should().BeSameAs(defaultConfig.RulesMetadata);
        }
    }
}
