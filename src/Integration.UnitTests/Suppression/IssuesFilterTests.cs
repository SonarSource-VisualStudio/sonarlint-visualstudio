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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Integration.Suppression;

namespace SonarLint.VisualStudio.Integration.UnitTests.Suppression
{
    [TestClass]
    public class IssuesFilterTests
    {
        private Mock<ISonarQubeIssuesProvider> sonarQubeIssuesProvider;
        private IssuesFilter testSubject;

        [TestInitialize]
        public void TestInitialize()
        {
            sonarQubeIssuesProvider = new Mock<ISonarQubeIssuesProvider>();
            testSubject = new IssuesFilter(sonarQubeIssuesProvider.Object);
        }

        [TestMethod]
        public void Ctor_NullSonarQubeIssuesProvider_ThrowsArgumentNullException()
        {
            Action act = () => new IssuesFilter(null);

            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("sonarQubeIssuesProvider");
        }

        [TestMethod]
        public void Filter_NullIssues_ThrowsArgumentNullException()
        {

            Action act = () => testSubject.Filter("c:\\path\\file1.txt", null);
            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("issues");
        }

        [TestMethod]
        public void Filter_NullFile_DoesNotThrow()
        {
            var inputIssues = new List<IFilterableIssue>();

            var result = testSubject.Filter(null, inputIssues);
            result.Should().NotBeNull();
        }
    }
}