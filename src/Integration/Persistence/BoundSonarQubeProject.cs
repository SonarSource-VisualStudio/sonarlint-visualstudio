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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace SonarLint.VisualStudio.Integration.Persistence
{
    internal class BoundSonarQubeProject
    {
        public BoundSonarQubeProject()
        {
        }

        public BoundSonarQubeProject(Uri serverUri, string projectKey, ICredentials credentials = null)
            : this()
        {
            if (serverUri == null)
            {
                throw new ArgumentNullException(nameof(serverUri));
            }

            if (string.IsNullOrWhiteSpace(projectKey))
            {
                throw new ArgumentNullException(nameof(projectKey));
            }

            this.ServerUri = serverUri;
            this.ProjectKey = projectKey;
            this.Credentials = credentials;
        }

        public Uri ServerUri { get; set; }

        public string ProjectKey { get; set; }

        public Dictionary<Language, ApplicableQualityProfile> Profiles { get; set; }

        [JsonIgnore]
        public ICredentials Credentials { get; set; }
    }
}
