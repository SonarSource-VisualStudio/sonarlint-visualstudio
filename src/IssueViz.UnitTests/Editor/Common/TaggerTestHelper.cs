﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2020 SonarSource SA
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


using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Moq;

namespace SonarLint.VisualStudio.IssueVisualization.UnitTests.Editor.Common
{
    internal static class TaggerTestHelper
    {
        public static readonly ITextBuffer ValidBuffer = CreateBufferWithSnapshot();

        public static ITextBuffer CreateBufferWithSnapshot(int length = 999) =>
            CreateBufferMockWithSnapshot(length).Object;

        public static Mock<ITextBuffer> CreateBufferMockWithSnapshot(int length = 999)
        {
            var snapshotMock = new Mock<ITextSnapshot>();
            snapshotMock.Setup(x => x.Length).Returns(length);

            var bufferMock = new Mock<ITextBuffer>();
            bufferMock.Setup(x => x.CurrentSnapshot).Returns(snapshotMock.Object);

            return bufferMock;
        }

        public static ITagAggregator<T> CreateAggregator<T>(params IMappingTagSpan<T>[] tagSpans) where T: ITag
        {
            var aggregatorMock = new Mock<ITagAggregator<T>>();
            aggregatorMock.Setup(x => x.GetTags(It.IsAny<NormalizedSnapshotSpanCollection>()))
                .Returns(tagSpans);
            return aggregatorMock.Object;
        }

        public static IMappingTagSpan<T> CreateMappingTagSpan<T>(ITextSnapshot snapshot, T tag, params Span[] spans) where T: ITag
        {
            var mappingSpanMock = new Mock<IMappingSpan>();
            var normalizedSpanCollection = new NormalizedSnapshotSpanCollection(snapshot, spans);
            mappingSpanMock.Setup(x => x.GetSpans(snapshot)).Returns(normalizedSpanCollection);

            var tagSpanMock = new Mock<IMappingTagSpan<T>>();
            tagSpanMock.Setup(x => x.Tag).Returns(tag);
            tagSpanMock.Setup(x => x.Span).Returns(mappingSpanMock.Object);

            return tagSpanMock.Object;
        }
    }
}
