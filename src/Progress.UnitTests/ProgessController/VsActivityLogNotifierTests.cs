/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using FluentAssertions;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Progress.Controller.ErrorNotification;

namespace SonarLint.VisualStudio.Progress.UnitTests
{
    /// <summary>
    /// Tests for <see cref="VsActivityLogNotifier"/>
    /// </summary>
    [TestClass]
    public class VsActivityLogNotifierTests
    {
        private ConfigurableServiceProvider serviceProvider;
        private StubVsActivityLog activityLog;
        private VsActivityLogNotifier testSubject;

        #region Test plumbing

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
        {
            this.serviceProvider = new ConfigurableServiceProvider();
            this.activityLog = new StubVsActivityLog();
            this.serviceProvider.RegisterService(typeof(SVsActivityLog), this.activityLog);
            this.serviceProvider.RegisterService(typeof(SVsTaskSchedulerService), new SingleThreadedTaskSchedulerService());
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.activityLog = null;
            this.serviceProvider = null;
            this.testSubject = null;
        }

        #endregion Test plumbing

        #region Tests

        [TestMethod]
        [Description("Arg check tests")]
        public void VsActivityLogNotifier_Args()
        {
            // 1st Argument invalid
            Exceptions.Expect<ArgumentNullException>(() => new VsActivityLogNotifier(null, "source", "{0}", false));

            // 2nd Argument invalid
            Exceptions.Expect<ArgumentNullException>(() => new VsActivityLogNotifier(this.serviceProvider, null, "{0}", false));
            Exceptions.Expect<ArgumentNullException>(() => new VsActivityLogNotifier(this.serviceProvider, string.Empty, "{0}", false));
            Exceptions.Expect<ArgumentNullException>(() => new VsActivityLogNotifier(this.serviceProvider, " \t", "{0}", false));

            // 3rd Argument invalid
            Exceptions.Expect<ArgumentNullException>(() => new VsActivityLogNotifier(this.serviceProvider, "source", null, false));
            Exceptions.Expect<ArgumentNullException>(() => new VsActivityLogNotifier(this.serviceProvider, "source", string.Empty, false));
            Exceptions.Expect<ArgumentNullException>(() => new VsActivityLogNotifier(this.serviceProvider, "source", " \t", false));

            // Valid
            new VsActivityLogNotifier(this.serviceProvider, "source", "{0}", false);
            new VsActivityLogNotifier(this.serviceProvider, "source", "{0}", true);
        }

        [TestMethod]
        [Description("Verifies logging of an exception message in activity log")]
        public void VsActivityLogNotifier_MessageOnly()
        {
            // Arrange
            bool logWholeMessage = true;
            Exception ex = this.Setup(logWholeMessage);

            // Act
            ((IProgressErrorNotifier)this.testSubject).Notify(ex);

            // Assert
            this.activityLog.IsEntryLogged.Should().BeTrue();
        }

        [TestMethod]
        [Description("Verifies logging of a full exception in activity log")]
        public void VsActivityLogNotifier_FullException()
        {
            // Arrange
            bool logWholeMessage = true;
            Exception ex = this.Setup(logWholeMessage);

            // Act
            ((IProgressErrorNotifier)this.testSubject).Notify(ex);

            // Assert
            this.activityLog.IsEntryLogged.Should().BeTrue();
        }

        #endregion Tests

        #region Helpers

        private Exception Setup(bool logWholeMessage)
        {
            string format = this.TestContext.TestName + "{0}";
            string source = this.TestContext.TestName;
            this.testSubject = new VsActivityLogNotifier(this.serviceProvider, source, format, logWholeMessage);
            Exception ex = this.GenerateException();
            this.activityLog.LogEntryAction = (entryType, actualSource, actualMessage) =>
            {
                entryType.Should().Be((uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR, "Unexpected entry type");
                actualSource.Should().Be(source, "Unexpected entry source");
                MessageVerificationHelper.VerifyNotificationMessage(actualMessage, format, ex, logWholeMessage);
            };
            return ex;
        }

        private Exception GenerateException()
        {
            return new Exception(this.TestContext.TestName, new Exception(Environment.TickCount.ToString()));
        }

        #endregion Helpers
    }
}