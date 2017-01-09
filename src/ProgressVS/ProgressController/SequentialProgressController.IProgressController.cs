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

using SonarLint.VisualStudio.Progress.Controller.ErrorNotification;
using SonarLint.VisualStudio.Progress.Threading;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TPL = System.Threading.Tasks;

namespace SonarLint.VisualStudio.Progress.Controller
{
    /// <summary>
    /// Partial class implementation of <see cref="IProgressController"/>
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability",
   "S2931:Classes with \"IDisposable\" members should implement \"IDisposable\"",
       Justification = "cancellationTokenSource is being disposed OnFinish whish is guaranteed (tested) to be called in the end",
       Scope = "type",
       Target = "~T:SonarLint.VisualStudio.Progress.Controller.SequentialProgressController")]
    public partial class SequentialProgressController : IProgressController
    {
        private readonly object locker = new object();
        private IEnumerable<IProgressStepOperation> progressStepOperations;
        private IProgressStepFactory stepFactory;
        private ErrorNotificationManager notificationManager;
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// <see cref="IProgressController.ErrorNotificationManager"/>
        /// </summary>
        public IErrorNotificationManager ErrorNotificationManager
        {
            get { return this.notificationManager; }
        }

        /// <summary>
        /// <see cref="IProgressController.Events"/>
        /// </summary>
        public IProgressEvents Events
        {
            get { return this; }
        }

        /// <summary>
        /// Initializes the controller with <see cref="IProgressStepFactory"/>
        /// which is responsible to convert the specified <see cref="IProgressStepDefinition"/>
        /// into executable <see cref="IProgressStepOperation"/>
        /// </summary>
        /// <param name="factory">An instance of <see cref="IProgressStepFactory"/>. Required.</param>
        /// <param name="stepsDefinition">set of step definitions. Required.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1500:VariableNamesShouldNotMatchFieldNames", MessageId = "stepFactory", Justification = "Using this to distinguish between them")]
        public void Initialize(IProgressStepFactory factory, IEnumerable<IProgressStepDefinition> stepsDefinition)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (stepsDefinition == null)
            {
                throw new ArgumentNullException(nameof(stepsDefinition));
            }

            if (this.stepFactory != null || this.progressStepOperations != null)
            {
                throw new InvalidOperationException(ProgressResources.AlreadyInitializedException);
            }

            this.notificationManager = new ErrorNotificationManager();
            this.stepFactory = factory;
            this.progressStepOperations = this.CreateStepOperations(stepsDefinition);
        }

        /// <summary>
        /// Starts executing the initialized steps.
        /// The method is not thread safe but can be called from any thread.
        /// </summary>
        /// <returns>An await-able</returns>
        public async TPL.Task<ProgressControllerResult> Start()
        {
            if (this.IsStarted)
            {
                throw new InvalidOperationException(ProgressResources.AlreadyStartedException);
            }

            this.OnStarted();

            ProgressControllerResult controllerResult = await VsThreadingHelper.RunTask<ProgressControllerResult>(this, VsTaskRunContext.BackgroundThread,
                () =>
                {
                    ThreadHelper.ThrowIfOnUIThread();

                    // By default can abort, the individual step may changed that
                    this.CanAbort = true;

                    ProgressControllerResult result = ProgressControllerResult.Cancelled;
                    foreach (IProgressStepOperation operation in this.progressStepOperations)
                    {
                        // Try to cancel (in case the step itself will not cancel itself)
                        if (this.cancellationTokenSource.IsCancellationRequested)
                        {
                            result = ProgressControllerResult.Cancelled;
                            break;
                        }

                        this.CanAbort = operation.Step.Cancellable;

                        IProgressStepExecutionEvents notifier = this.stepFactory.GetExecutionCallback(operation);

                        // Give another try before running the operation (there's a test that covers cancellation
                        // before running the operation which requires this check after CanAbort is set)
                        if (this.cancellationTokenSource.IsCancellationRequested)
                        {
                            result = ProgressControllerResult.Cancelled;
                            break;
                        }

                        StepExecutionState stepResult = operation.Run(this.cancellationTokenSource.Token, notifier).Result;

                        /* Not trying to cancel here intentionally. The reason being
                        in case the step was the last one, there's nothing to cancel really,
                        otherwise there will be an attempt to cancel just before the next
                        step execution*/

                        if (stepResult == StepExecutionState.Succeeded)
                        {
                            result = ProgressControllerResult.Succeeded;
                        }
                        else if (stepResult == StepExecutionState.Failed)
                        {
                            result = ProgressControllerResult.Failed;
                            break;
                        }
                        else if (stepResult == StepExecutionState.Cancelled)
                        {
                            result = ProgressControllerResult.Cancelled;
                            break;
                        }
                        else
                        {
                            Debug.Fail("Unexpected step execution result:" + stepResult);
                        }
                    }

                    return result;
                },

                this.cancellationTokenSource.Token);

            this.OnFinished(controllerResult);

            return controllerResult;
        }

        /// <summary>
        /// Attempts to abort the current operation. In case not started or the operation is not cancellable the abort request will be ignored.
        /// <seealso cref="CanAbort"/>
        /// <seealso cref="IsStarted"/>
        /// </summary>
        /// <returns>Whether aborted or not</returns>
        public bool TryAbort()
        {
            bool aborted = this.ThreadSafeCancelCancellationTokenSource();
            if (aborted)
            {
                this.CanAbort = false;
            }

            return aborted;
        }

        private void ThreadSafeCreateCancellationTokenSource()
        {
            lock (this.locker)
            {
                this.cancellationTokenSource = new CancellationTokenSource();
            }
        }

        private void ThreadSafeDisposeCancellationTokenSource()
        {
            if (this.cancellationTokenSource != null)
            {
                lock (this.locker)
                {
                    if (this.cancellationTokenSource != null)
                    {
                        this.cancellationTokenSource.Dispose();
                        this.cancellationTokenSource = null;
                    }
                }
            }
        }

        private bool ThreadSafeCancelCancellationTokenSource()
        {
            if (this.CanAbort && this.cancellationTokenSource != null && !this.cancellationTokenSource.IsCancellationRequested)
            {
                lock (this.locker)
                {
                    if (this.CanAbort && this.cancellationTokenSource != null && !this.cancellationTokenSource.IsCancellationRequested)
                    {
                        this.cancellationTokenSource.Cancel();
                        return true;
                    }
                }
            }

            return false;
        }

        private void ConfigureStepEventListeners(bool start)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (IProgressStepOperation stepOperation in this.progressStepOperations)
            {
                if (start)
                {
                    stepOperation.Step.StateChanged += this.OnStepStateChanged;
                }
                else
                {
                    stepOperation.Step.StateChanged -= this.OnStepStateChanged;
                }
            }
        }

        private IEnumerable<IProgressStepOperation> CreateStepOperations(IEnumerable<IProgressStepDefinition> definitions)
        {
            List<IProgressStepOperation> steps = new List<IProgressStepOperation>();
            foreach (IProgressStepDefinition def in definitions)
            {
                steps.Add(this.stepFactory.CreateStepOperation(this, def));
            }

            return steps;
        }
    }
}
