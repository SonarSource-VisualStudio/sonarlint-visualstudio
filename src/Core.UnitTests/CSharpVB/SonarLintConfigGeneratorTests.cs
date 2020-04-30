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
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Core.CSharpVB;
using SonarQube.Client.Models;

namespace SonarLint.VisualStudio.Core.UnitTests.CSharpVB
{
    [TestClass]
    public class SonarLintConfigGeneratorTests
    {
        private static readonly IEnumerable<SonarQubeRule> EmptyRules = Array.Empty<SonarQubeRule>();
        private static readonly IDictionary<string, string> EmptyProperties = new Dictionary<string, string>();
        private const string ValidLanguage = "cs";
        private static readonly IDictionary<string, string> ValidParams = new Dictionary<string, string> { { "any", "any value" } };

        [TestMethod]
        public void Generate_NullArguments_Throws()
        {
            Action act = () => SonarLintConfigGenerator.Generate(null, EmptyProperties, ValidLanguage);
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("rules");

            act = () => SonarLintConfigGenerator.Generate(EmptyRules, null, ValidLanguage);
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("sonarProperties");

            act = () => SonarLintConfigGenerator.Generate(EmptyRules, EmptyProperties, null);
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("language");
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("xxx")]
        [DataRow("CS")] // should be case-sensitive
        [DataRow("vb")] // VB language key is "vbnet"
        public void Generate_UnrecognisedLanguage_Throws(string language)
        {
            Action act = () => SonarLintConfigGenerator.Generate(EmptyRules, EmptyProperties, language);
            act.Should().ThrowExactly<ArgumentOutOfRangeException>().And.ParamName.Should().Be("language");
        }

        [TestMethod]
        [DataRow("cs")]
        [DataRow("vbnet")]
        public void Generate_NoActiveRulesOrSettings_ValidLanguage_ReturnsValidConfig(string validLanguage)
        {
            var actual = SonarLintConfigGenerator.Generate(EmptyRules, EmptyProperties, validLanguage);

            actual.Should().NotBeNull();
            actual.Rules.Should().BeEmpty();
            actual.Settings.Should().BeEmpty();
        }

        [TestMethod]
        public void Generate_ValidSettings_OnlyLanguageSpecificSettingsReturned()
        {
            // Arrange
            var properties = new Dictionary<string, string>
            {
                { "sonar.cs.property1", "valid setting 1"},
                { "sonar.cs.property2", "valid setting 2"},
                { "sonar.vbnet.property1", "wrong language - not returned"},
                { "sonar.CS.property2", "wrong case - not returned"},
                { "sonar.cs.", "incorrect prefix - not returned"},
                { "xxx.cs.property1", "key does not match - not returned"},
                { ".does.not.match", "not returned"}
            };

            // Act
            var actual = SonarLintConfigGenerator.Generate(EmptyRules, properties, "cs");

            // Assert
            actual.Settings.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                { "sonar.cs.property1", "valid setting 1"},
                { "sonar.cs.property2", "valid setting 2"}
            });
        }

        [TestMethod]
        public void Generate_ValidSettings_AreSorted()
        {
            // Arrange
            var properties = new Dictionary<string, string>
            {
                { "sonar.cs.property3", "aaa"},
                { "sonar.cs.property1", "bbb"},
                { "sonar.cs.property2", "ccc"},
            };

            // Act
            var actual = SonarLintConfigGenerator.Generate(EmptyRules, properties, "cs");

            // Assert
            actual.Settings[0].Key.Should().Be("sonar.cs.property1");
            actual.Settings[0].Value.Should().Be("bbb");

            actual.Settings[1].Key.Should().Be("sonar.cs.property2");
            actual.Settings[1].Value.Should().Be("ccc");

            actual.Settings[2].Key.Should().Be("sonar.cs.property3");
            actual.Settings[2].Value.Should().Be("aaa");
        }

        [TestMethod]
        public void Generate_ValidSettings_SecuredSettingsAreNotReturned()
        {
            // Arrange
            var properties = new Dictionary<string, string>
            {
                { "sonar.cs.property1.secured", "secure - should not be returned"},
                { "sonar.cs.property2", "valid setting"},
                { "sonar.cs.property3.SECURED", "secure - should not be returned2"},
            };

            // Act
            var actual = SonarLintConfigGenerator.Generate(EmptyRules, properties, "cs");

            // Assert
            actual.Settings.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                { "sonar.cs.property2", "valid setting"}
            });
        }

        [TestMethod]
        [DataRow("cs", "csharpsquid")]
        [DataRow("vbnet", "vbnet")]
        public void Generate_ValidRules_OnlyRulesFromKnownRepositoryReturned(string knownLanguage, string knownRepoKey)
        {
            var rules = new List<SonarQubeRule>()
            {
                CreateRuleWithValidParams("valid1", knownRepoKey),
                CreateRuleWithValidParams("unknown1", "unknown.repo.key"),
                CreateRuleWithValidParams("valid2", knownRepoKey),
                CreateRuleWithValidParams("invalid2", "another.unknown.repo.key"),
                CreateRuleWithValidParams("valid3", knownRepoKey)
            };

            // Act
            var actual = SonarLintConfigGenerator.Generate(rules, EmptyProperties, knownLanguage);

            // Assert
            actual.Rules.Select(r => r.Key).Should().BeEquivalentTo(new string[] { "valid1", "valid2", "valid3" });
        }

        [TestMethod]
        [DataRow("cs")]
        [DataRow("vbnet")]
        public void Generate_SonarSecurityRules_AreNotReturned(string language)
        {
            var rules = new List<SonarQubeRule>()
            {
                CreateRuleWithValidParams("valid1", $"roslyn.sonaranalyzer.security.{language}"),
                CreateRuleWithValidParams("valid2", $"roslyn.sonaranalyzer.security.{language}")
            };

            // Act
            var actual = SonarLintConfigGenerator.Generate(rules, EmptyProperties, language);

            // Assert
            actual.Rules.Should().BeEmpty();
        }

        [TestMethod]
        public void Generate_ValidRules_OnlyRulesWithParametersReturned()
        {
            var rule1Params = new Dictionary<string, string> { { "param1", "value1" }, { "param2", "value2" } };
            var rule3Params = new Dictionary<string, string> { { "param3", "value4" } };

            var rules = new List<SonarQubeRule>()
            {
                CreateRule("s111", "csharpsquid", rule1Params ),
                CreateRule("s222", "csharpsquid" /* no params */),
                CreateRule("s333", "csharpsquid", rule3Params )
            };

            // Act
            var actual = SonarLintConfigGenerator.Generate(rules, EmptyProperties, "cs");

            // Assert
            actual.Rules.Count.Should().Be(2);

            actual.Rules[0].Key.Should().Be("s111");
            actual.Rules[0].Parameters.Should().BeEquivalentTo(rule1Params);
            actual.Rules[1].Key.Should().Be("s333");
            actual.Rules[1].Parameters.Should().BeEquivalentTo(rule3Params);
        }

        [TestMethod]
        public void Generate_ValidRules_AreSorted()
        {
            var rules = new List<SonarQubeRule>()
            {
                CreateRule("s222", "csharpsquid",
                    new Dictionary<string, string> { { "any", "any" } }),
                CreateRule("s111", "csharpsquid",
                    new Dictionary<string, string> { { "CCC", "value 1" }, { "BBB", "value 2" }, { "AAA", "value 3" } }),
                CreateRule("s333", "csharpsquid",
                    new Dictionary<string, string> { { "any", "any" } })
            };

            // Act
            var actual = SonarLintConfigGenerator.Generate(rules, EmptyProperties, "cs");

            // Assert
            actual.Rules.Count.Should().Be(3);

            actual.Rules[0].Key.Should().Be("s111");
            actual.Rules[1].Key.Should().Be("s222");
            actual.Rules[2].Key.Should().Be("s333");
            
            actual.Rules[0].Parameters[0].Key.Should().Be("AAA");
            actual.Rules[0].Parameters[0].Value.Should().Be("value 3");

            actual.Rules[0].Parameters[1].Key.Should().Be("BBB");
            actual.Rules[0].Parameters[1].Value.Should().Be("value 2");

            actual.Rules[0].Parameters[2].Key.Should().Be("CCC");
            actual.Rules[0].Parameters[2].Value.Should().Be("value 1");
        }

        [TestMethod]
        public void Generate_Serialized_ReturnsExpectedXml()
        {
            var properties = new Dictionary<string, string>()
            {
                { "sonar.cs.prop1", "value 1"},
                { "sonar.cs.prop2", "value 2"}
            };

            var rules = new List<SonarQubeRule>()
            {
                CreateRule("s555", "csharpsquid",
                    new Dictionary<string, string> { { "x", "y y" } }),
                CreateRule("s444", "csharpsquid"),
                CreateRule("s333", "csharpsquid"),
                CreateRule("s222", "csharpsquid",
                    new Dictionary<string, string> { { "ZZZ", "param value1" }, { "AAA", "param value2" } }),
                CreateRule("s111", "csharpsquid"),
            };

            // Act
            var actual = SonarLintConfigGenerator.Generate(rules, properties, "cs");
            var actualXml = Serializer.ToString(actual);

            // Assert
            actualXml.Should().Be(@"<?xml version=""1.0"" encoding=""utf-8""?>
<AnalysisInput xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <Settings>
    <Setting>
      <Key>sonar.cs.prop1</Key>
      <Value>value 1</Value>
    </Setting>
    <Setting>
      <Key>sonar.cs.prop2</Key>
      <Value>value 2</Value>
    </Setting>
  </Settings>
  <Rules>
    <Rule>
      <Key>s222</Key>
      <Parameters>
        <Parameter>
          <Key>AAA</Key>
          <Value>param value2</Value>
        </Parameter>
        <Parameter>
          <Key>ZZZ</Key>
          <Value>param value1</Value>
        </Parameter>
      </Parameters>
    </Rule>
    <Rule>
      <Key>s555</Key>
      <Parameters>
        <Parameter>
          <Key>x</Key>
          <Value>y y</Value>
        </Parameter>
      </Parameters>
    </Rule>
  </Rules>
</AnalysisInput>");
        }

        private static SonarQubeRule CreateRuleWithValidParams(string ruleKey, string repoKey) =>
            CreateRule(ruleKey, repoKey, ValidParams);

        private static SonarQubeRule CreateRule(string ruleKey, string repoKey, IDictionary<string, string> parameters = null) =>
            new SonarQubeRule(ruleKey, repoKey, isActive: false, SonarQubeIssueSeverity.Blocker, parameters);
    }
}
