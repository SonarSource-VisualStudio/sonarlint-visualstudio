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

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    [TestClass]
    public class PathHelperTests
    {
        [TestMethod]
        public void PathHelper_EscapeFileName()
        {
            // Arrange
            const string unescapedString = "A string | with / special : and / invalid \\ characters * all < over > the \" place ?";
            const string expectedEscaped = "A string _ with _ special _ and _ invalid _ characters _ all _ over _ the _ place _";

            // Act
            string actualEscaped = PathHelper.EscapeFileName(unescapedString);

            // Assert
            actualEscaped.Should().Be(expectedEscaped);
        }

        [TestMethod]
        public void PathHelper_ForceDirectoryEnding()
        {
            // Arrange
            const string withSlash = @"X:\directories\all\the\way\";
            const string withoutSlash = @"X:\directories\all\the\way";

            // Test case: without trailing slash
            PathHelper.ForceDirectoryEnding(withoutSlash).Should().Be(withSlash, "Expected to append trailing slash '\'");

            // Test case: with trailing slash
            PathHelper.ForceDirectoryEnding(withSlash).Should().Be(withSlash, "Expected to return input string without modification");
        }

        [TestMethod]
        public void PathHelper_CalculateRelativePath()
        {
            // Up one level
            VerifyCalculateRelativePath
            (
                expected: @"..\file2.ext",
                fromPath: @"X:\dirA\file1.ext",
                toPath: @"X:\file2.ext"
            );

            // Up multiple levels
            VerifyCalculateRelativePath
            (
                expected: @"..\..\..\file2.ext",
                fromPath: @"X:\dirA\dirB\dirC\file1.ext",
                toPath: @"X:\file2.ext"
            );

            // Down one level
            VerifyCalculateRelativePath
            (
                expected: @"dirA\file2.ext",
                fromPath: @"X:\file1.txt",
                toPath: @"X:\dirA\file2.ext"
            );

            // Down multiple levels
            VerifyCalculateRelativePath
            (
                expected: @"dirA\dirB\dirC\file2.ext",
                fromPath: @"X:\file1.ext",
                toPath: @"X:\dirA\dirB\dirC\file2.ext"
            );

            // Same level
            VerifyCalculateRelativePath
            (
                expected: @"file2.ext",
                fromPath: @"X:\file1.ext",
                toPath: @"X:\file2.ext"
            );

            // Different roots
            VerifyCalculateRelativePath
            (
                expected: @"Y:\file2.ext",
                fromPath: @"X:\file1.ext",
                toPath: @"Y:\file2.ext"
            );

            // Complicated file names
            VerifyCalculateRelativePath
            (
                expected: @"file with spaces (2).ext",
                fromPath: @"X:\file with spaces (1).ext",
                toPath: @"X:\file with spaces (2).ext"
            );

            // Non canonical paths (contains . and ..)
            VerifyCalculateRelativePath
            (
                expected: @"..\..\file1.ext",
                fromPath: @"X:\dirA\..\dirA\dirB\dirC\dirD\",
                toPath: @"X:\dirA\dirB\..\dirB\file1.ext"
            );
        }

        [TestMethod]
        public void PathHelper_CalculateRelativePath_NullArgumentChecks()
        {
            // 'absolute' param
            Exceptions.Expect<ArgumentNullException>(() =>
            {
                PathHelper.CalculateRelativePath(@"X:\a\file.proj", null);
            });

            // 'relativeTo' param
            Exceptions.Expect<ArgumentNullException>(() =>
            {
                PathHelper.CalculateRelativePath(null, @"X:\a\file.sln");
            });
        }

        [TestMethod]
        public void PathHelper_CalculateRelativePath_InputPathsMustBeAbsolute()
        {
            // 'absolute' param
            Exceptions.Expect<ArgumentException>(() =>
            {
                PathHelper.CalculateRelativePath(@"X:\a\file.proj", @"not\absolute\file.sln");
            });

            // 'relativeTo' param
            Exceptions.Expect<ArgumentException>(() =>
            {
                PathHelper.CalculateRelativePath(@"not\absolute\file.proj", @"X:\a\file.sln");
            });
        }

        [TestMethod]
        public void PathHelper_ResolveRelativePath()
        {
            // Up one level
            VerifyResolveRelativePath
            (
                expected: @"X:\file1.ext",
                basePath: @"X:\dirA\",
                relativePath: @"..\file1.ext"
            );

            // Up multiple levels
            VerifyResolveRelativePath
            (
                expected: @"X:\file1.ext",
                basePath: @"X:\dirA\dirB\dirC\",
                relativePath: @"..\..\..\file1.ext"
            );

            // Down one level
            VerifyResolveRelativePath
            (
                expected: @"X:\dirA\file1.ext",
                basePath: @"X:\",
                relativePath: @"dirA\file1.ext"
            );

            // Down multiple levels
            VerifyResolveRelativePath
            (
                expected: @"X:\dirA\dirB\dirC\file1.ext",
                basePath: @"X:\",
                relativePath: @"dirA\dirB\dirC\file1.ext"
            );

            // Same level
            VerifyResolveRelativePath
            (
                expected: @"X:\file1.ext",
                basePath: @"X:\",
                relativePath: @"file1.ext"
            );

            // Complicated file names
            VerifyResolveRelativePath
            (
                expected: @"X:\file with spaces.ext",
                basePath: @"X:\",
                relativePath: @"file with spaces.ext"
            );
        }

        [TestMethod]
        public void PathHelper_ExpandRelativePath_NullArgumentChecks()
        {
            // 'baseDirectoryPath' param
            Exceptions.Expect<ArgumentNullException>(() =>
            {
                PathHelper.ResolveRelativePath(null, @"..\file.proj");
            });

            // 'relativePath' param
            Exceptions.Expect<ArgumentNullException>(() =>
            {
                PathHelper.ResolveRelativePath(@"X:\a", null);
            });
        }

        [TestMethod]
        public void PathHelper_ExpandRelativePath_BasePathMustBeAbsolute()
        {
            Exceptions.Expect<ArgumentException>(() =>
            {
                PathHelper.ResolveRelativePath(@"not\absolute\file.sln", @"..\a\relative.path");
            });
        }

        [TestMethod]
        public void PathHelper_IsPathRootedUnder_RootedPath_IsTrue()
        {
            // Arrange
            const string root = @"X:\All\Files\Live\Here";
            const string rootedFile = @"X:\All\Files\Live\Here\likeme.ext";

            // Act
            bool rootedIsRooted = PathHelper.IsPathRootedUnderRoot(rootedFile, root);

            // Assert
            rootedIsRooted.Should().BeTrue($"Path '{rootedFile}' should be rooted under '{root}'");
        }

        [TestMethod]
        public void PathHelper_IsPathRootedUnder_UnrootedPath_IsFalse()
        {
            // Arrange
            const string root = @"X:\All\Files\Live\Here";

            const string unrootedFile = @"X:\im_not_under_the_root.ext";

            // Act
            bool unrootedIsRooted = PathHelper.IsPathRootedUnderRoot(unrootedFile, root);

            // Assert
            unrootedIsRooted.Should().BeFalse($"Path '{unrootedFile}' should not be rooted under '{root}'");
        }

        [TestMethod]
        public void PathHelper_IsPathRootedUnder_PathsContainRelativeComponents()
        {
            // Arrange
            const string root = @"X:\All\Files\..\Files\Live\Here";
            const string rootedFile = @"X:\All\..\All\Files\Live\Here\likeme.ext";

            // Act
            bool rootedIsRooted = PathHelper.IsPathRootedUnderRoot(rootedFile, root);

            // Assert
            rootedIsRooted.Should().BeTrue($"Path '{rootedFile}' should be rooted under '{root}'");
        }

        #region Helpers

        private static void VerifyCalculateRelativePath(string expected, string fromPath, string toPath)
        {
            string actual = PathHelper.CalculateRelativePath(fromPath, toPath);

            actual.Should().Be(expected);
        }

        private static void VerifyResolveRelativePath(string expected, string basePath, string relativePath)
        {
            string actual = PathHelper.ResolveRelativePath(relativePath, basePath);

            actual.Should().Be(expected);
        }

        #endregion Helpers
    }
}