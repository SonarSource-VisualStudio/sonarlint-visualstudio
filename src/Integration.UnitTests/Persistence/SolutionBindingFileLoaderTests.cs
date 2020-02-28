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
using System.IO;
using System.IO.Abstractions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Persistence;
using Moq;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    [TestClass]
    public class SolutionBindingFileLoaderTests
    {
        private Mock<ILogger> logger;
        private Mock<IFileSystem> fileSystem;
        private SolutionBindingFileLoader testSubject;

        private const string MockFilePath = "c:\\test.txt";
        private const string MockDirectory = "c:\\";

        [TestInitialize]
        public void TestInitialize()
        {
            logger = new Mock<ILogger>();
            fileSystem = new Mock<IFileSystem>();

            testSubject = new SolutionBindingFileLoader(logger.Object, fileSystem.Object);

            fileSystem.Setup(x => x.Directory.Exists(MockDirectory)).Returns(true);
        }

        [TestMethod]
        public void Ctor_NullLogger_Exception()
        {
            Action act = () => new SolutionBindingFileLoader(null, null);

            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void Ctor_NullFileSystem_Exception()
        {
            Action act = () => new SolutionBindingFileLoader(logger.Object, null);

            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("fileSystem");
        }

        [TestMethod]
        public void Save_DirectoryDoesNotExist_DirectoryIsCreated()
        {
            fileSystem.Setup(x => x.Directory.Exists(MockDirectory)).Returns(false);

            testSubject.Save(MockFilePath, new BoundSonarQubeProject());

            fileSystem.Verify(x => x.Directory.CreateDirectory(MockDirectory), Times.Once);
        }

        [TestMethod]
        public void Save_DirectoryExists_DirectoryNotCreated()
        {
            fileSystem.Setup(x => x.Directory.Exists(MockDirectory)).Returns(true);

            testSubject.Save(MockFilePath, new BoundSonarQubeProject());

            fileSystem.Verify(x => x.Directory.CreateDirectory(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public void Save_ReturnsTrue()
        {
            fileSystem.Setup(x => x.File.WriteAllText(MockFilePath, It.IsAny<string>()));

            var actual = testSubject.Save(MockFilePath, new BoundSonarQubeProject());
            actual.Should().BeTrue();
        }

        [TestMethod]
        public void Save_FileSerializedAndWritten()
        {
            var boundSonarQubeProject = new BoundSonarQubeProject(
                new Uri("http://xxx.www.zzz/yyy:9000"),
                "MyProject Key",
                "projectName");

            var expectedContent = @"{
  ""ServerUri"": ""http://xxx.www.zzz/yyy:9000"",
  ""Organization"": null,
  ""ProjectKey"": ""MyProject Key"",
  ""ProjectName"": ""projectName"",
  ""Profiles"": null
}";

            fileSystem.Setup(x => x.File.WriteAllText(MockFilePath, expectedContent));

            testSubject.Save(MockFilePath, boundSonarQubeProject);

            fileSystem.Verify(x => x.File.WriteAllText(MockFilePath, expectedContent), Times.Once);
        }

        [TestMethod]
        public void Save_NonCriticalException_False()
        {
            fileSystem.Setup(x => x.File.WriteAllText(MockFilePath, It.IsAny<string>())).Throws<PathTooLongException>();

            var actual = testSubject.Save(MockFilePath, new BoundSonarQubeProject());
            actual.Should().BeFalse();
        }

        [TestMethod]
        public void Save_CriticalException_Exception()
        {
            fileSystem.Setup(x => x.File.WriteAllText(MockFilePath, It.IsAny<string>())).Throws<StackOverflowException>();

            Action act = () => testSubject.Save(MockFilePath, new BoundSonarQubeProject());

            act.Should().ThrowExactly<StackOverflowException>();
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(null)]
        public void Load_FilePathIsNull_Null(string filePath)
        {
            var actual = testSubject.Load(filePath);
            actual.Should().Be(null);
        }

        [TestMethod]
        public void Load_FileDoesNotExist_Null()
        {
            fileSystem.Setup(x => x.File.Exists(MockFilePath)).Returns(false);

            var actual = testSubject.Load(MockFilePath);
            actual.Should().Be(null);
        }

        [TestMethod]
        public void Load_InvalidJson_Null()
        {
            fileSystem.Setup(x => x.File.Exists(MockFilePath)).Returns(true);
            fileSystem.Setup(x => x.File.ReadAllText(MockFilePath)).Returns("bad json");

            var actual = testSubject.Load(MockFilePath);
            actual.Should().Be(null);
        }

        [TestMethod]
        public void Load_NonCriticalException_Null()
        {
            fileSystem.Setup(x => x.File.Exists(MockFilePath)).Returns(true);
            fileSystem.Setup(x => x.File.ReadAllText(MockFilePath)).Throws<PathTooLongException>();

            var actual = testSubject.Load(MockFilePath);
            actual.Should().Be(null);
        }

        [TestMethod]
        public void Load_CriticalException_Exception()
        {
            fileSystem.Setup(x => x.File.Exists(MockFilePath)).Returns(true);
            fileSystem.Setup(x => x.File.ReadAllText(MockFilePath)).Throws<StackOverflowException>();

            Action act = () => testSubject.Load(MockFilePath);

            act.Should().ThrowExactly<StackOverflowException>();
        }

        [TestMethod]
        public void Load_FileExists_DeserializedProject()
        {
            var fileContent = @"{
  ""ServerUri"": ""http://xxx.www.zzz/yyy:9000"",
  ""Organization"": null,
  ""ProjectKey"": ""MyProject Key"",
  ""ProjectName"": ""projectName"",
  ""Profiles"": null
}";

            var expectedProject = new BoundSonarQubeProject(
                new Uri("http://xxx.www.zzz/yyy:9000"),
                "MyProject Key",
                "projectName");


            fileSystem.Setup(x => x.File.Exists(MockFilePath)).Returns(true);
            fileSystem.Setup(x => x.File.ReadAllText(MockFilePath)).Returns(fileContent);

            var actual = testSubject.Load(MockFilePath);
            actual.Should().BeEquivalentTo(expectedProject);
        }
    }
}
