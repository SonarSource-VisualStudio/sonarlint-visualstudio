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
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarLint.VisualStudio.Infrastructure.VS;
using SonarLint.VisualStudio.Integration.UnitTests;
using SonarLint.VisualStudio.TypeScript.TsConfig;

namespace SonarLint.VisualStudio.TypeScript.UnitTests.TsConfig
{
    [TestClass]
    public class TsConfigsLocatorTests
    {
        private const string ValidSourceFilePath = "source-file.ts";
        private const string ProjectDirectory = "c:\\test\\solution\\project\\";

        [TestMethod]
        public void MefCtor_CheckIsExported()
        {
            MefTestHelpers.CheckTypeCanBeImported<TsConfigsLocator, ITsConfigsLocator>(null, new[]
            {
                MefTestHelpers.CreateExport<IProjectDirectoryProvider>(Mock.Of<IProjectDirectoryProvider>())
            });
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(null)]
        public void Locate_NullProjectDirectory_EmptyList(string projectDirectory)
        {
            var testSubject = CreateTestSubject(projectDirectory);

            var result = testSubject.Locate(ValidSourceFilePath);

            result.Should().BeEmpty();
        }

        [TestMethod]
        public void Locate_NoMatchingFiles_EmptyList()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory(ProjectDirectory);

            var testSubject = CreateTestSubject(ProjectDirectory, fileSystem);

            var result = testSubject.Locate(ValidSourceFilePath);

            result.Should().BeEmpty();
        }

        [TestMethod]
        public void Locate_SearchesFilesUnderTheDirectory_FindsCorrectFiles()
        {
            var fileSystem = new MockFileSystem();

            var files = new Dictionary<string, bool>
            {
                { "..\\tsconfig.json", false}, // exact match + wrong root
                { "tsconfig.JSON", true}, // case-sensitivity
                { "a\\tsconfig.json", true}, // exact match + sub folder
                { "tsconfig.json.txt", false}, // wrong file extension
                { "atsconfig.json", false}, // wrong name
                { "b\\c\\TSCONFIG.json", true}, // case-sensitivity + sub folder
            };

            foreach (var file in files)
            {
                fileSystem.AddFile(Path.Combine(ProjectDirectory, file.Key), new MockFileData("some content"));
            }

            var testSubject = CreateTestSubject(ProjectDirectory, fileSystem);

            var expectedFiles = files
                .Where(x => x.Value)
                .Select(x => Path.Combine(ProjectDirectory, x.Key));

            var result = testSubject.Locate(ValidSourceFilePath);

            result.Should().BeEquivalentTo(expectedFiles);
        }

        private static MockFileSystem SetupFileSystem(params string[] files)
        {
            var fileSystem = new MockFileSystem();

            foreach (var file in files)
            {
                fileSystem.AddFile(file, new MockFileData("some content"));
            }

            return fileSystem;
        }

        private static TsConfigsLocator CreateTestSubject(string projectDirectory, IFileSystem fileSystem = null)
        {
            var projectDirectoryProvider = new Mock<IProjectDirectoryProvider>();
            projectDirectoryProvider
                .Setup(x => x.GetProjectDirectory(ValidSourceFilePath))
                .Returns(projectDirectory);

            return new TsConfigsLocator(projectDirectoryProvider.Object, fileSystem);
        }
    }
}
