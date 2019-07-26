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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnvDTE;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarLint.VisualStudio.Integration.Vsix;
using SonarLint.VisualStudio.Integration.Vsix.CFamily;

namespace SonarLint.VisualStudio.Integration.UnitTests.CFamily
{
    [TestClass]
    public class CFamily_CLangAnalyzerTests
    {
        [TestMethod]
        public void CallAnalyzer_Succeeds()
        {
            // Arrange
            var dummyProcessRunner = new DummyProcessRunner(MockResponse(), true);

            // Act
            var response = CFamilyHelper.CallClangAnalyzer(new Request(), dummyProcessRunner, new TestLogger());

            // Assert;
            dummyProcessRunner.ExecuteCalled.Should().BeTrue();
            File.Exists(dummyProcessRunner.ExchangeFileName).Should().BeFalse();

            response.Should().NotBeNull();
            response.Messages.Count().Should().Be(1);
            response.Messages[0].Filename.Should().Be("file.cpp");
        }

        [TestMethod]
        public void CallAnalyzer_Fails()
        {
            // Arrange
            var dummyProcessRunner = new DummyProcessRunner(MockResponse(), false);

            // Act
            var response = CFamilyHelper.CallClangAnalyzer(new Request(), dummyProcessRunner, new TestLogger());

            // Assert;
            dummyProcessRunner.ExecuteCalled.Should().BeTrue();
            File.Exists(dummyProcessRunner.ExchangeFileName).Should().BeFalse();

            response.Should().BeNull();
        }

        private class DummyProcessRunner : IProcessRunner
        {

            private readonly byte[] responseToReturn;
            private readonly bool successCodeToReturn;

            public DummyProcessRunner(byte[] responseToReturn, bool successCodeToReturn)
            {
                this.responseToReturn = responseToReturn;
                this.successCodeToReturn = successCodeToReturn;
            }

            public bool ExecuteCalled { get; private set; }
            public string ExchangeFileName { get; private set; }

            public bool Execute(ProcessRunnerArguments runnerArgs)
            {
                ExecuteCalled = true;

                runnerArgs.Should().NotBeNull();

                // Expecting a single file name as input
                runnerArgs.CmdLineArgs.Count().Should().Be(1);
                ExchangeFileName = runnerArgs.CmdLineArgs.First();
                File.Exists(ExchangeFileName).Should().BeTrue();

                // Replace the file with the response
                File.Delete(ExchangeFileName);

                WriteResponse(ExchangeFileName, responseToReturn);

                return successCodeToReturn;
            }

            private static void WriteResponse(string fileName, byte[] data)
            {
                using (var stream = new FileStream(fileName, FileMode.CreateNew))
                {
                    stream.Write(data, 0, data.Length);
                }
            }
        }

        private byte[] MockEmptyResponse()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream);
                Protocol.WriteUTF(writer, "OUT");

                // 0 issues
                Protocol.WriteInt(writer, 0);

                // 0 measures
                Protocol.WriteInt(writer, 0);

                // 0 symbols
                Protocol.WriteInt(writer, 0);

                Protocol.WriteUTF(writer, "END");
                return stream.ToArray();
            }
        }

        private byte[] MockResponse()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream);
                Protocol.WriteUTF(writer, "OUT");

                // 1 issue
                Protocol.WriteInt(writer, 1);

                Protocol.WriteUTF(writer, "ruleKey");
                Protocol.WriteUTF(writer, "file.cpp");
                Protocol.WriteInt(writer, 10);
                Protocol.WriteInt(writer, 11);
                Protocol.WriteInt(writer, 12);
                Protocol.WriteInt(writer, 13);
                Protocol.WriteInt(writer, 100);
                Protocol.WriteUTF(writer, "Issue message");
                writer.Write(true);

                // 1 flow
                Protocol.WriteInt(writer, 1);
                Protocol.WriteUTF(writer, "another.cpp");
                Protocol.WriteInt(writer, 14);
                Protocol.WriteInt(writer, 15);
                Protocol.WriteInt(writer, 16);
                Protocol.WriteInt(writer, 17);
                Protocol.WriteUTF(writer, "Flow message");

                // 1 measure
                Protocol.WriteInt(writer, 1);
                Protocol.WriteUTF(writer, "file.cpp");
                Protocol.WriteInt(writer, 1);
                Protocol.WriteInt(writer, 1);
                Protocol.WriteInt(writer, 1);
                Protocol.WriteInt(writer, 1);
                Protocol.WriteInt(writer, 1);

                byte[] execLines = new byte[] { 1, 2, 3, 4 };
                Protocol.WriteInt(writer, execLines.Length);
                writer.Write(execLines);


                // 1 symbol
                Protocol.WriteInt(writer, 1);
                Protocol.WriteInt(writer, 1);
                Protocol.WriteInt(writer, 1);
                Protocol.WriteInt(writer, 1);
                Protocol.WriteInt(writer, 1);
                Protocol.WriteInt(writer, 1);

                Protocol.WriteUTF(writer, "END");
                return stream.ToArray();
            }
        }

    }
}
