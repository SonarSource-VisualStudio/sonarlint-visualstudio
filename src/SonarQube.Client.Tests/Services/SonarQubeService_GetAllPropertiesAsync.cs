﻿/*
 * SonarQube Client
 * Copyright (C) 2016-2018 SonarSource SA
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Client.Models;

namespace SonarQube.Client.Tests.Services
{
    [TestClass]
    public class SonarQubeService_GetAllPropertiesAsync : SonarQubeService_TestBase
    {
        [TestMethod]
        public async Task GetProperties_Old_ExampleFromSonarQube()
        {
            await ConnectToSonarQube();

            SetupRequest("api/properties", @"[
  {
    ""key"": ""sonar.test.jira"",
    ""value"": ""abc""
  },
  {
    ""key"": ""sonar.autogenerated"",
    ""value"": ""val1,val2,val3"",
    ""values"": [
      ""val1"",
      ""val2"",
      ""val3""
    ]
  },
  {
    ""key"": ""sonar.demo"",
    ""value"": ""1,2"",
    ""values"": [
      ""1"",
      ""2""
    ]
  },
  {
    ""key"": ""sonar.demo.1.text"",
    ""value"": ""foo""
  },
  {
    ""key"": ""sonar.demo.1.boolean"",
    ""value"": ""true""
  },
  {
    ""key"": ""sonar.demo.2.text"",
    ""value"": ""bar""
  },
  {
    ""key"": ""sonar.demo.2.boolean"",
    ""value"": ""false""
  }
]");

            var result = await service.GetAllPropertiesAsync(CancellationToken.None);

            messageHandler.VerifyAll();

            result.Should().HaveCount(7);
            result.Select(x => x.Key).Should().BeEquivalentTo(
                new[]
                {
                    "sonar.test.jira",
                    "sonar.autogenerated",
                    "sonar.demo",
                    "sonar.demo.1.text",
                    "sonar.demo.1.boolean",
                    "sonar.demo.2.text",
                    "sonar.demo.2.boolean"
                });
            result.Select(x => x.Value).Should().BeEquivalentTo(
                new[]
                {
                    "abc",
                    "val1,val2,val3",
                    "1,2",
                    "foo",
                    "true",
                    "bar",
                    "false"
                });
        }

        [TestMethod]
        public async Task GetProperties_New_ExampleFromSonarQube()
        {
            await ConnectToSonarQube("6.3.0.0");

            SetupRequest("api/settings/values", @"{
  ""settings"": [
    {
      ""key"": ""sonar.test.jira"",
      ""value"": ""abc"",
      ""inherited"": true
    },
    {
      ""key"": ""sonar.autogenerated"",
      ""values"": [
        ""val1"",
        ""val2"",
        ""val3""
      ],
      ""inherited"": false
    },
    {
      ""key"": ""sonar.demo"",
      ""fieldValues"": [
        {
          ""boolean"": ""true"",
          ""text"": ""foo""
        },
        {
          ""boolean"": ""false"",
          ""text"": ""bar""
        }
      ],
      ""inherited"": false
    }
  ]
}");

            var result = await service.GetAllPropertiesAsync(CancellationToken.None);

            messageHandler.VerifyAll();

            result.Should().HaveCount(3);
            result.Select(x => x.Key).Should().BeEquivalentTo(
                new[]
                {
                    "sonar.test.jira",
                    "sonar.autogenerated",
                    "sonar.demo",
                });
            result.Select(x => x.Value).Should().BeEquivalentTo(
                new[]
                {
                    "abc",
                    null,
                    null,
                });
        }

        [TestMethod]
        public async Task GetProperties_Old_NotFound()
        {
            await ConnectToSonarQube();

            SetupRequest("api/properties", "", HttpStatusCode.NotFound);

            Func<Task<IList<SonarQubeProperty>>> func = async () =>
                await service.GetAllPropertiesAsync(CancellationToken.None);

            func.Should().ThrowExactly<HttpRequestException>().And
                .Message.Should().Be("Response status code does not indicate success: 404 (Not Found).");

            messageHandler.VerifyAll();
        }
    }
}
