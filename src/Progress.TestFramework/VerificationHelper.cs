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

using SonarLint.VisualStudio.Progress.Controller;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarLint.VisualStudio.Progress.UnitTests
{
    /// <summary>
    /// Verification helper class
    /// </summary>
    public static class VerificationHelper
    {
        /// <summary>
        /// Verifies that the <see cref="ProgressControllerStep"/> was initialized correctly
        /// </summary>
        /// <param name="testSubject">The step to verify</param>
        /// <param name="attributes">Step attributes</param>
        /// <param name="displayText">Step display text</param>
        public static void VerifyInitialized(ProgressControllerStep testSubject, StepAttributes attributes, string displayText = null)
        {
            StepExecution expectedExecution = (attributes & StepAttributes.BackgroundThread) != 0 ? StepExecution.BackgroundThread : StepExecution.ForegroundThread;
            bool expectedHidden = (attributes & StepAttributes.Hidden) != 0 ? true : false;
            bool expectedCancellable = (attributes & StepAttributes.NonCancellable) != 0 ? false : true;
            bool expectedImpactingProgress = (attributes & StepAttributes.NoProgressImpact) != 0 ? false : true;
            bool expectedIndeterminate = (attributes & StepAttributes.Indeterminate) != 0 ? true : false;

            CheckState(testSubject, StepExecutionState.NotStarted);
            Assert.AreEqual(displayText, testSubject.DisplayText, "Unexpected display text");
            Assert.AreEqual(expectedCancellable, testSubject.Cancellable, "Cancellable: Unexpected post initialization value");
            Assert.AreEqual(expectedIndeterminate, testSubject.Indeterminate, "Indeterminate: Unexpected post initialization value");
            Assert.AreEqual(expectedExecution, testSubject.Execution, "Execution: Unexpected post initialization value");
            Assert.AreEqual(expectedHidden, testSubject.Hidden, "Hidden: Unexpected post initialization value");
            Assert.AreEqual(expectedImpactingProgress, testSubject.ImpactsProgress, "ImpactingProgress: Unexpected post initialization value");

            if (expectedIndeterminate)
            {
                Assert.IsTrue(ProgressControllerHelper.IsIndeterminate(testSubject.Progress), "Progess: Should be Indeterminate");
            }
            else
            {
                Assert.AreEqual(0, testSubject.Progress, "Progress: Unexpected post initialization value");
            }
        }

        /// <summary>
        /// Checks the current state of a <see cref="IProgressStep"/>
        /// </summary>
        /// <param name="testSubject">The step to check</param>
        /// <param name="expectedState">The expected state of the step</param>
        public static void CheckState(IProgressStep testSubject, StepExecutionState expectedState)
        {
            Assert.AreEqual(expectedState, testSubject.ExecutionState, "Unexpected state");
        }
    }
}
