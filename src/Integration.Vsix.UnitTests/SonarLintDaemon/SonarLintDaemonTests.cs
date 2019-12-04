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
using System.ComponentModel;
using System.IO;
using System.Threading;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Vsix;
using SonarLint.VisualStudio.Integration.Vsix.Resources;
using VSIX = SonarLint.VisualStudio.Integration.Vsix;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    [TestClass]
    public class SonarLintDaemonTests
    {
        private TestableSonarLintDaemon testableDaemon;
        private TestLogger logger;
        private ConfigurableSonarLintSettings settings;
        private DummyDaemonInstaller dummyInstaller;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void SetUp()
        {
            settings = new ConfigurableSonarLintSettings
            {
                DaemonLogLevel = DaemonLogLevel.Verbose,
                IsActivateMoreEnabled = true // default to assuming the user has turned on support for additional languages
            };
            logger = new TestLogger(logToConsole: true);

            dummyInstaller = new DummyDaemonInstaller
            {
                IsInstalledReturnValue = true, // installed by default
                InstallationPath = Path.Combine(TestContext.DeploymentDirectory, TestContext.TestName, Path.GetRandomFileName())
            };
            Directory.CreateDirectory(dummyInstaller.InstallationPath);


            testableDaemon = new TestableSonarLintDaemon(settings, logger, dummyInstaller);

            logger.Reset(); // clear any messages logged during construction
        }

        [TestCleanup]
        public void Cleanup()
        {
            CleanupProcess();

            try
            {
                testableDaemon.Dispose();
                ForceDeleteDirectory(dummyInstaller.InstallationPath);
            }
            catch(Exception ex)
            {
                TestContext.WriteLine($"Error during test cleanup: {ex.ToString()}");
            }
        }

        private void CleanupProcess()
        {
            try
            {
                if (testableDaemon.process != null && !testableDaemon.process.HasExited)
                {
                    testableDaemon.process.Kill();
                }
            }
            catch(InvalidOperationException)
            {
                // Expected if the process hasn't been started, which it won't have for
                // most of the tests
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Error during process cleanup: {ex.ToString()}");
            }
        }

        [TestMethod]
        public void IsRunning_NotRunningOnCreation()
        {
            testableDaemon.IsRunning.Should().BeFalse();
        }

        [TestMethod]
        public void IsInstalled_ActivateAdditionalLanguagesIsFalse_Start_NotStarted()
        {
            dummyInstaller.IsInstalledReturnValue = true;
            settings.IsActivateMoreEnabled = false;

            // Act
            testableDaemon.Start();

            // Assert
            testableDaemon.IsRunning.Should().BeFalse();
            testableDaemon.CreateChannelCallCount.Should().Be(0);
            logger.AssertOutputStringExists(Strings.Daemon_NotStarting_NotEnabled);
        }

        [TestMethod]
        public void Run_Without_Install()
        {
            dummyInstaller.IsInstalledReturnValue = false;
            testableDaemon.Start();

            // Should not throw as this could crash VS: https://github.com/SonarSource/sonarlint-visualstudio/issues/999
            // ...but daemon should not start either.
            testableDaemon.IsRunning.Should().BeFalse();
        }

        [TestMethod]
        public void Run_With_Install_Start_Succeeds()
        {
            testableDaemon.Port.Should().Be(0);

            // Fake the installation i.e. create the directory and file to execute.
            testableDaemon.SetUpDummyInstallation("testStartSucceeds.bat",
@"@echo Hello world
@echo write to error stream... 1>&2
");

            // Act
            testableDaemon.Start();
            bool processFinished = testableDaemon.process.WaitForExit(5000); // Give any asynchronous events the chance to complete
            processFinished.Should().BeTrue("Test execution error: timed out waiting for the dummy process to exit");
            TestContext.WriteLine($"Test: process.HasExited={testableDaemon.process.HasExited}");

            // Assert
            testableDaemon.Port.Should().NotBe(0);

            logger.AssertOutputStringExists(VSIX.Resources.Strings.Daemon_Starting);
            logger.AssertPartialOutputStringExists(testableDaemon.ExePath);
            logger.AssertOutputStringExists(VSIX.Resources.Strings.Daemon_Started);

            // TODO: output streams are not being captured on CI builds (cix or Azure DevOps)
            //logger.AssertPartialOutputStringExists("Hello world"); // standard output should have been captured
            //logger.AssertPartialOutputStringExists("write to error stream..."); // error output should have been captured

            testableDaemon.WasSafeInternalStopCalled.Should().BeFalse();
        }

        [TestMethod]
        public void Run_With_Install_StartFails_ErrorsLogged()
        {
            // Fake the installation i.e. create the directory and file to execute.
            testableDaemon.SetUpDummyInstallation("testStartFails.bat", "echo hello world");

            // Act
            // Lock the file so the daemon can't start the process -> should fail immediately
            using (var lockingStream = File.Open(testableDaemon.ExePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                testableDaemon.Start();
            }

            // Assert
            logger.AssertOutputStringExists(VSIX.Resources.Strings.Daemon_Starting);
            logger.AssertPartialOutputStringExists("Unable to start SonarLint daemon");

            testableDaemon.WasSafeInternalStopCalled.Should().BeTrue();
        }

        [TestMethod]
        public void Stop_Without_Start_Has_No_Effect()
        {
            testableDaemon.IsRunning.Should().BeFalse(); // Sanity test
            testableDaemon.Stop();
            testableDaemon.IsRunning.Should().BeFalse();
            testableDaemon.WasSafeInternalStopCalled.Should().BeFalse();
        }

        [TestMethod]
        public void SystemInteractiveAsync_EnsureCorrectVersion()
        {
            // Regression test for https://github.com/SonarSource/sonarlint-visualstudio/issues/850

            // We're just checking for the expected hard-coded assembly versions. This test will need
            // to be updated if the version of Grpc.Core being used changes.

            var grpcCoreAsm = AssemblyHelper.GetVersionOfReferencedAssembly(typeof(VSIX.SonarLintDaemon), "Grpc.Core");
            grpcCoreAsm.Should().NotBeNull("Cannot locate the Grpc.Core assembly referenced by SonarLint");
            grpcCoreAsm.Should().Be(new Version(1, 0, 0, 0),
                "SonarLint not referencing the expected version of Grpc.Core. Does this test need to be updated?");

            var siaAsm = AssemblyHelper.GetVersionOfReferencedAssembly(typeof(VSIX.SonarLintDaemon), "System.Interactive.Async");
            siaAsm.Should().Be(new Version("3.0.1000.0"),
                "SonarLint is not using the version of System.Interactive.Async expected by Grpc.Core. This will cause a runtime error.");
        }

        [TestMethod]
        public void SafeOperation_NonCriticalException()
        {
            // Act
            testableDaemon.SafeOperation(() => { throw new InvalidCastException("YYY"); } );

            // Assert
            logger.AssertPartialOutputStringExists("System.InvalidCastException", "YYY");
        }

        [TestMethod]
        public void SafeOperation_CriticalExceptionsAreNotCaught()
        {
            // Arrange
            Action op = () =>
            {
                testableDaemon.SafeOperation(() => { throw new StackOverflowException(); });
            };

            // Act and assert
            op.Should().ThrowExactly<StackOverflowException>();
            logger.AssertNoOutputMessages();
        }

        [TestMethod]
        public void HandleOutputDataReceived_NoData_NoError()
        {
            // Act
            testableDaemon.HandleOutputDataReceived(null);

            // Assert
            logger.AssertNoOutputMessages();
            testableDaemon.CreateChannelCallCount.Should().Be(0);
        }

        [TestMethod]
        public void HandleOutputDataReceived_Data_NotServerStarted_Verbose_LoggedButCreateChannelIsNotCalled()
        {
            // Act
            settings.DaemonLogLevel = DaemonLogLevel.Verbose;
            testableDaemon.HandleOutputDataReceived("Something happened...");

            // Assert
            logger.AssertOutputStringExists("Something happened...");
            testableDaemon.CreateChannelCallCount.Should().Be(0);
            testableDaemon.WasReadyEventInvoked.Should().BeFalse();
        }

        [TestMethod]
        public void HandleOutputDataReceived_Data_NotServerStarted_NotVerbose_NotLogged()
        {
            // Act
            settings.DaemonLogLevel = DaemonLogLevel.Info;
            testableDaemon.HandleOutputDataReceived("Data should only be logger for logging level is verbose");

            // Assert
            logger.AssertNoOutputMessages();
        }

        [TestMethod]
        public void HandleOutputDataReceived_Data_ServerStarted_CreateChannelIsCalled()
        {
            // Act
            testableDaemon.HandleOutputDataReceived("XXXServer startedyyyy");

            // Assert
            logger.AssertOutputStringExists("XXXServer startedyyyy");
            testableDaemon.CreateChannelCallCount.Should().Be(1);
            testableDaemon.WasReadyEventInvoked.Should().BeTrue();
            testableDaemon.WasSafeInternalStopCalled.Should().BeFalse();
        }

        [TestMethod]
        public void HandleOutputDataReceived_Data_ServerStarted_NonCriticalException_StopIsCalled()
        {            
            // Throw a non-critical exception when 
            testableDaemon.CreateChannelAndStreamLogsOp = () => { throw new InvalidCastException(); };

            // Act
            testableDaemon.HandleOutputDataReceived("Server started");

            // Assert
            logger.AssertOutputStringExists("Server started");
            testableDaemon.CreateChannelCallCount.Should().Be(1);
            testableDaemon.WasReadyEventInvoked.Should().BeFalse();
            logger.AssertPartialOutputStringExists("System.InvalidCastException");
            testableDaemon.WasSafeInternalStopCalled.Should().BeTrue();
        }

        [TestMethod]
        public void HandleOutputDataReceived_Data_ServerStarted_CriticalException_StopIsNotCalled()
        {
            // Throw a critical exception when 
            testableDaemon.CreateChannelAndStreamLogsOp = () => { throw new StackOverflowException(); };

            Action op = () => testableDaemon.HandleOutputDataReceived("Server started");

            // Act
            op.Should().ThrowExactly<StackOverflowException>();

            // Assert
            logger.AssertOutputStringExists("Server started");
            testableDaemon.CreateChannelCallCount.Should().Be(1);
            testableDaemon.WasReadyEventInvoked.Should().BeFalse();

            // We should not do any further processing for a critical exception
            // -> not expecting the daemon to have been stopped
            testableDaemon.WasSafeInternalStopCalled.Should().BeFalse();
        }

        [TestMethod]
        public void HandleErrorDataReceived_NoData_NoError()
        {
            // Act
            testableDaemon.HandleErrorDataReceived(null);

            // Assert
            logger.AssertNoOutputMessages();
        }

        [TestMethod]
        public void HandleErrorDataReceived_HasData_IsLogged()
        {
            // 1. Verbose logging
            settings.DaemonLogLevel = DaemonLogLevel.Verbose;
            testableDaemon.HandleErrorDataReceived("error - verbose");

            logger.AssertOutputStringExists("error - verbose");


            // 2. Minimal logging - errors still logged
            settings.DaemonLogLevel = DaemonLogLevel.Minimal;
            testableDaemon.HandleErrorDataReceived("error - minimal");

            logger.AssertOutputStringExists("error - minimal");
        }

        [TestMethod]
        public void Dispose_WorkingDirectoryDeleted()
        {
            // Sanity check
            testableDaemon.WorkingDirectory.Should().NotBeNull();
            Directory.Exists(testableDaemon.WorkingDirectory).Should().BeTrue();

            // 1. Dispose -> directory cleared
            testableDaemon.Dispose();
            Directory.Exists(testableDaemon.WorkingDirectory).Should().BeFalse();

            // 2. Multiple dispose should not error
            testableDaemon.Dispose();
        }

        [TestMethod]
        public void IsAnalyisSupported_AdditionalSupportNotActive()
        {
            // Arrange
            settings.IsActivateMoreEnabled = false;

            // Should never return true if activate more is not enabled
            testableDaemon.IsAnalysisSupported(new[] { AnalysisLanguage.Javascript }).Should().BeFalse();
            testableDaemon.IsAnalysisSupported(new[] { AnalysisLanguage.CFamily }).Should().BeFalse();
        }

        [TestMethod]
        public void IsAnalyisSupported_AdditionalSupportIsActive()
        {
            // Arrange
            settings.IsActivateMoreEnabled = true;

            // Should never return true if activate more is not enabled
            testableDaemon.IsAnalysisSupported(new[] { AnalysisLanguage.Javascript }).Should().BeTrue();
            testableDaemon.IsAnalysisSupported(new[] { AnalysisLanguage.CFamily }).Should().BeFalse();
            testableDaemon.IsAnalysisSupported(new[] { AnalysisLanguage.CFamily, AnalysisLanguage.Javascript }).Should().BeTrue();
        }

        private static void ForceDeleteDirectory(string path)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Thread.Sleep(1);
                Directory.Delete(path, true);
            }
        }

        private class TestableSonarLintDaemon : VSIX.SonarLintDaemon
        {
            private string nameOfSubstituteExeFile;

            public TestableSonarLintDaemon(ISonarLintSettings settings, ILogger logger, IDaemonInstaller installer)
                : base(settings, installer, logger)
            {
                this.Ready += (s, a) => WasReadyEventInvoked = true;
            }

            public bool WasReadyEventInvoked { get; private set; }

            public bool WasSafeInternalStopCalled { get; private set; }

            public Action CreateChannelAndStreamLogsOp { get; set; }

            public int CreateChannelCallCount { get; private set; }


            public void SetUpDummyInstallation(string batFileName, string batFileContents)
            {
                // The real daemon launches a Java exe. To make things simple, we'll launch
                // a batch file instead.

                // This method creates the necessary directories and batch file to be executed
                // that will be called by the daemon and overrides the behaviour of ExePath
                // to return the dummy batch file
                Directory.CreateDirectory(Path.GetDirectoryName(base.ExePath));
                nameOfSubstituteExeFile = batFileName;

                File.WriteAllText(this.ExePath, batFileContents);
            }

            #region Overrides

            protected override void CreateChannelAndStreamLogs()
            {
                CreateChannelCallCount++;
                CreateChannelAndStreamLogsOp?.Invoke();
            }

            internal override string ExePath
            {
                get
                {
                    if (nameOfSubstituteExeFile == null)
                    {
                        return base.ExePath;
                    }

                    var baseDir = Path.GetDirectoryName(base.ExePath);
                    return Path.Combine(baseDir, nameOfSubstituteExeFile);
                }
            }

            protected override void SafeInternalStop()
            {
                WasSafeInternalStopCalled = true;
                base.SafeInternalStop();
            }

            #endregion Overrides
        }

        private class DummyDaemonInstaller : IDaemonInstaller
        {
            public bool IsInstalledReturnValue { get;set;}

            #region IDaemonInstaller methods

            public bool InstallInProgress { get; set; }

            public string InstallationPath { get; set; }

            public string DaemonVersion { get; set; }

            public event InstallationProgressChangedEventHandler InstallationProgressChanged;
            public event AsyncCompletedEventHandler InstallationCompleted;

            public void Install()
            {
                throw new NotImplementedException();
            }

            public bool IsInstalled()
            {
                return IsInstalledReturnValue;
            }

            #endregion
        }
    }
}
