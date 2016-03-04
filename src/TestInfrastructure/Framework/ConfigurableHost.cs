﻿//-----------------------------------------------------------------------
// <copyright file="ConfigurableHost.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Integration.Service;
using SonarLint.VisualStudio.Integration.State;
using SonarLint.VisualStudio.Integration.TeamExplorer;
using System;
using System.Windows.Threading;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    internal class ConfigurableHost : IHost
    {
        private readonly ConfigurableServiceProvider serviceProvider;

        public ConfigurableHost()
            : this(new ConfigurableServiceProvider(), Dispatcher.CurrentDispatcher)
        {
        }

        public ConfigurableHost(ConfigurableServiceProvider sp, Dispatcher dispatcher)
        {
            this.serviceProvider = sp;
            this.UIDIspatcher = dispatcher;
            this.VisualStateManager = new ConfigurableStateManager { Host = this };
        }

        public IConnectSection ActiveSection
        {
            get;
            private set;
        }

        public ISonarQubeServiceWrapper SonarQubeService
        {
            get;
            set;
        }

        public Dispatcher UIDIspatcher
        {
            get;
            private set;
        }

        public IStateManager VisualStateManager
        {
            get;
            set;
        }

        public void ClearActiveSection()
        {
            this.ActiveSection = null;

            // Simulate product code
            this.VisualStateManager.SyncCommandFromActiveSection();
        }

        public object GetService(Type serviceType)
        {
            return this.serviceProvider.GetService(serviceType);
        }

        public void SetActiveSection(IConnectSection section)
        {
            Assert.IsNotNull(section);

            this.ActiveSection = section;

            // Simulate product code
            this.VisualStateManager.SyncCommandFromActiveSection();
        }
    }
}
