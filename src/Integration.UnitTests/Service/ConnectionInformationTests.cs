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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Service;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    [TestClass]
    public class ConnectionInformationTests
    {
        [TestMethod]
        public void ConnectionInformation_WithLoginInformation()
        {
            // Arrange
            var userName = "admin";
            var passwordUnsecure = "admin";
            var password = passwordUnsecure.ToSecureString();
            var serverUri = new Uri("http://localhost/");
            var testSubject = new ConnectionInformation(serverUri, userName, password);

            // Act
            password.Dispose(); // Connection information should maintain it's own copy of the password

            // Assert
            testSubject.Password.ToUnsecureString().Should().Be(passwordUnsecure, "Password doesn't match");
            testSubject.UserName.Should().Be(userName, "UserName doesn't match");
            testSubject.ServerUri.Should().Be(serverUri, "ServerUri doesn't match");

            // Act clone
            var testSubject2 = (ConnectionInformation)((ICloneable)testSubject).Clone();

            // Now dispose the test subject
            testSubject.Dispose();

            // Assert testSubject
            Exceptions.Expect<ObjectDisposedException>(() => testSubject.Password.ToUnsecureString());

            // Assert testSubject2
            testSubject2.Password.ToUnsecureString().Should().Be(passwordUnsecure, "Password doesn't match");
            testSubject2.UserName.Should().Be(userName, "UserName doesn't match");
            testSubject2.ServerUri.Should().Be(serverUri, "ServerUri doesn't match");
        }

        [TestMethod]
        public void ConnectionInformation_WithoutLoginInformation()
        {
            // Arrange
            var serverUri = new Uri("http://localhost/");

            // Act
            var testSubject = new ConnectionInformation(serverUri);

            // Assert
            testSubject.Password.Should().BeNull("Password wasn't provided");
            testSubject.UserName.Should().BeNull("UserName wasn't provided");
            testSubject.ServerUri.Should().Be(serverUri, "ServerUri doesn't match");

            // Act clone
            var testSubject2 = (ConnectionInformation)((ICloneable)testSubject).Clone();

            // Assert testSubject2
            testSubject2.Password.Should().BeNull("Password wasn't provided");
            testSubject2.UserName.Should().BeNull("UserName wasn't provided");
            testSubject2.ServerUri.Should().Be(serverUri, "ServerUri doesn't match");
        }

        [TestMethod]
        public void ConnectionInformation_Ctor_NormalizesServerUri()
        {
            // Act
            var noSlashResult = new ConnectionInformation(new Uri("http://localhost/NoSlash"));

            // Assert
            noSlashResult.ServerUri.ToString().Should().Be("http://localhost/NoSlash/", "Unexpected normalization of URI without trailing slash");
        }

        [TestMethod]
        public void ConnectionInformation_Ctor_ArgChecks()
        {
            Exceptions.Expect<ArgumentNullException>(() => new ConnectionInformation(null));
            Exceptions.Expect<ArgumentNullException>(() => new ConnectionInformation(null, "user", "pwd".ToSecureString()));
        }
    }
}