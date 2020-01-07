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
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.CodeAnalysis.RuleSets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Binding;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    [TestClass]
    public class DotNetRulesConfigurationFileTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Ctor_InvalidArgs()
        {
            Action act = () => new DotNetRulesConfigurationFile(null);

            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("ruleSet");
        }

        [TestMethod]
        public void Save_InvalidArgs()
        {
            var testSubject = new DotNetRulesConfigurationFile(new RuleSet("dummy"));

            // 1. Null -> throw
            Action act = () => testSubject.Save(null);
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("fullFilePath");

            // 2. Empty -> throw
            act = () => testSubject.Save("");
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("fullFilePath");
        }

        [TestMethod]
        public void Save_ValidFilePath_SaveCalled()
        {
            // We can't mock the RuleSet class so we're testing Save by actually
            // writing to disk.
            // Arrange
            var testDir = Path.Combine(TestContext.DeploymentDirectory, TestContext.TestName);
            Directory.CreateDirectory(testDir);
            var fullPath = Path.Combine(testDir, "savedRuleSet.txt");

            var testSubject = new DotNetRulesConfigurationFile(new RuleSet("dummy"));

            // Act
            testSubject.Save(fullPath);

            // Assert
            File.Exists(fullPath).Should().BeTrue();
        }
    }
}
