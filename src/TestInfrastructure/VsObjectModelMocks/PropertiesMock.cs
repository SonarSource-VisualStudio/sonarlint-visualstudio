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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using FluentAssertions;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    public class PropertiesMock : Properties
    {
        private readonly List<PropertyMock> properties = new List<PropertyMock>();
        private readonly object parent;

        public PropertiesMock(object parent)
        {
            this.parent = parent;
        }

        #region Properties

        object Properties.Application
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        int Properties.Count
        {
            get
            {
                return this.properties.Count;
            }
        }

        DTE Properties.DTE
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        object Properties.Parent
        {
            get
            {
                return this.parent;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.properties.GetEnumerator();
        }

        IEnumerator Properties.GetEnumerator()
        {
            return this.properties.GetEnumerator();
        }

        Property Properties.Item(object index)
        {
            return this.properties[(int)index - 1]; // Starts from 1.
        }

        #endregion Properties

        #region Test helpers

        public PropertyMock RegisterKnownProperty(string name)
        {
            if (this.properties.Any(p => p.Name == name))
            {
                FluentAssertions.Execution.Execute.Assertion.FailWith($"Already has property: {name}");
            }

            PropertyMock prop = new PropertyMock(name, this);
            this.properties.Add(prop);
            return prop;
        }

        #endregion Test helpers
    }
}