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
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SonarLint.VisualStudio.Integration.UnitTests.SonarAnalyzer
{
    internal class ConfigurableAnalyzerReference : AnalyzerReference
    {
        private readonly string displayName;
        private readonly object id;

        public ConfigurableAnalyzerReference(object id, string displayName)
        {
            this.id = id;
            this.displayName = displayName;
        }

        public override string Display
        {
            get
            {
                return displayName;
            }
        }

        public override string FullPath
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override object Id
        {
            get
            {
                return id;
            }
        }

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language)
        {
            throw new NotImplementedException();
        }

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages()
        {
            throw new NotImplementedException();
        }
    }
}