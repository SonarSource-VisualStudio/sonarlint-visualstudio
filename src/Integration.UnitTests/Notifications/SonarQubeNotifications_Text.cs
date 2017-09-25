﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarLint.VisualStudio.Integration.UnitTests;

namespace SonarLint.VisualStudio.Integration.Notifications.UnitTests
{
    [TestClass]
    public class SonarQubeNotifications_Text
    {
        private SonarQubeNotifications notifications;

        [TestInitialize]
        public void TestInitialize()
        {
            notifications = new SonarQubeNotifications(new ConfigurableSonarQubeServiceWrapper(),
                new ConfigurableStateManager(), new Mock<ITimer>().Object, new Mock<ITimer>().Object);
        }

        [TestMethod]
        public void Text_Raises_PropertyChanged()
        {
            // Arrange
            notifications.MonitorEvents();

            // Act
            notifications.Text = "some random text";

            // Assert
            notifications.ShouldRaisePropertyChangeFor(x => x.Text);
        }
    }
}
