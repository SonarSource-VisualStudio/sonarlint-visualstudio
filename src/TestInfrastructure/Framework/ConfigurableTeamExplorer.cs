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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using FluentAssertions;
using Microsoft.TeamFoundation.Controls;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    /// <summary>
    /// Test implementation of <see cref="ITeamExplorer"/>.
    /// </summary>
    public class ConfigurableTeamExplorer : ITeamExplorer
    {
        internal Guid CurrentPageId { get; private set; }

        public IDictionary<Guid, ITeamExplorerPage> AvailablePages { get; } = new Dictionary<Guid, ITeamExplorerPage>();

        public ConfigurableTeamExplorer()
            : this(new Guid(TeamExplorerPageIds.Home))
        {
        }

        public ConfigurableTeamExplorer(Guid startPage)
        {
            this.CurrentPageId = startPage;
            this.AddStandardPages();
        }

        private void AddStandardPages()
        {
            const BindingFlags constantsBindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            IEnumerable<string> standardPageIdStrings = typeof(TeamExplorerPageIds)
                                                            .GetFields(constantsBindingFlags)
                                                            .Where(x => x.IsLiteral && !x.IsInitOnly)
                                                            .Select(x => x.GetRawConstantValue())
                                                            .OfType<string>();
            foreach (var idStr in standardPageIdStrings)
            {
                var guid = new Guid(idStr);
                var pageInstance = new ConfigurableTeamExplorerPage(guid);
                this.AvailablePages.Add(guid, pageInstance);
            }
        }

        #region ITeamExplorer

        ITeamExplorerPage ITeamExplorer.CurrentPage
        {
            get
            {
                return this.AvailablePages[this.CurrentPageId];
            }
        }

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        void ITeamExplorer.ClearNotifications()
        {
            throw new NotImplementedException();
        }

        void ITeamExplorer.ClosePage(ITeamExplorerPage page)
        {
            throw new NotImplementedException();
        }

        object IServiceProvider.GetService(Type serviceType)
        {
            throw new NotImplementedException();
        }

        bool ITeamExplorer.HideNotification(Guid id)
        {
            throw new NotImplementedException();
        }

        bool ITeamExplorer.IsNotificationVisible(Guid id)
        {
            throw new NotImplementedException();
        }

        ITeamExplorerPage ITeamExplorer.NavigateToPage(Guid pageId, object context)
        {
            this.CurrentPageId = pageId;
            return ((ITeamExplorer)this).CurrentPage;
        }

        void ITeamExplorer.ShowNotification(string message, NotificationType type, NotificationFlags flags, ICommand command, Guid id)
        {
            throw new NotImplementedException();
        }

        #endregion ITeamExplorer
    }
}