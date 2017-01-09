﻿/*
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

using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.InfoBar;
using System;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    internal class ConfigurableInfoBar : IInfoBar
    {
        private int closedCalled;

        public ConfigurableInfoBar(string message, string buttonText, ImageMoniker imageMoniker)
        {
            Assert.IsNotNull(message, "Message is null");
            Assert.IsNotNull(buttonText, "Button text is null");
            Assert.IsNotNull(imageMoniker, "image moniker is null");

            this.Message = message;
            this.ButtonText = buttonText;
            this.Image = imageMoniker;
        }

        #region IInfoBar
        public event EventHandler ButtonClick;
        public event EventHandler Closed;

        public void Close()
        {
            this.closedCalled++;
        }
        #endregion

        #region Test helpers
        public string Message { get; }
        public string ButtonText { get; }
        public ImageMoniker Image { get; }

        public void AssertClosedCalled(int expectedNumberOfTimes)
        {
            Assert.AreEqual(expectedNumberOfTimes, this.closedCalled, $"{nameof(Close)} was called unexpected number of times");
        }

        public void SimulateButtonClickEvent()
        {
            this.ButtonClick?.Invoke(this, EventArgs.Empty);
        }

        public void SimulateClosedEvent()
        {
            this.Closed?.Invoke(this, EventArgs.Empty);
        }

        public void VerifyAllEventsUnregistered()
        {
            Assert.IsNull(this.ButtonClick, $"{nameof(this.ButtonClick)} event remained registered");
            Assert.IsNull(this.Closed, $"{nameof(this.Closed)} event remained registered");
        }

        public void VerifyAllEventsRegistered()
        {
            Assert.IsNotNull(this.ButtonClick, $"{nameof(this.ButtonClick)} event is not registered");
            Assert.IsNotNull(this.Closed, $"{nameof(this.Closed)} event is not registered");
        }
        #endregion
    }
}
