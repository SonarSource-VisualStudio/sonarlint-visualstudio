﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto: contact AT sonarsource DOT com
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

using SonarLint.VisualStudio.Integration.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace SonarLint.VisualStudio.Integration
{
    /// <summary>
    /// Represents a programming language. Implements <seealso cref="IEquatable{T}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <seealso cref="Language"/>s are equal if they have the same <see cref="Id"/> and <see cref="ProjectType"/>.
    /// </para>
    /// <para>
    /// This class is safe for use as a key in collection classes. E.g., <seealso cref="IDictionary{TKey, TValue}"/>.
    /// </para>
    /// </remarks>
    [DebuggerDisplay("{Name} (ID: {Id}, ProjectType: {ProjectType}, IsSupported: {IsSupported})")]
    [TypeConverter(typeof(LanguageConverter))]
    public sealed class Language : IEquatable<Language>
    {
        public readonly static Language Unknown = new Language();
        public readonly static Language CSharp = new Language("CSharp", Strings.CSharpLanguageName, ProjectSystemHelper.CSharpProjectKind);
        public readonly static Language VBNET = new Language("VB", Strings.VBNetLanguageName, ProjectSystemHelper.VbProjectKind);

        /// <summary>
        /// A stable identifier for this language.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The language display name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The VS project GUID for this language.
        /// </summary>
        public Guid ProjectType { get; }

        /// <summary>
        /// Returns whether or not this language is a supported project language.
        /// </summary>
        public bool IsSupported => SupportedLanguages.Contains(this);

        /// <summary>
        /// All languages which are supported for project binding.
        /// </summary>
        public static IEnumerable<Language> SupportedLanguages
        {
            get
            {
                return new[] { CSharp, VBNET };
            }
        }

        /// <summary>
        /// All known languages.
        /// </summary>
        public static IEnumerable<Language> KnownLanguages
        {
            get
            {
                return new[] { CSharp, VBNET };
            }
        }

        /// <summary>
        /// Private constructor reserved for the <seealso cref="Unknown"/>.
        /// </summary>
        private Language()
        {
            this.Id = string.Empty;
            this.Name = Strings.UnknownLanguageName;
            this.ProjectType = Guid.Empty;
        }

        public Language(string id, string name, string projectTypeGuid)
            : this(id, name, new Guid(projectTypeGuid))
        {
        }

        public Language(string id, string name, Guid projectType)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            this.Id = id;
            this.Name = name;
            this.ProjectType = projectType;
        }

        public static Language ForProject(EnvDTE.Project dteProject)
        {
            if (dteProject == null)
            {
                throw new ArgumentNullException(nameof(dteProject));
            }

            Guid projectKind;
            if (!Guid.TryParse(dteProject.Kind, out projectKind))
            {
                return Unknown;
            }

            return KnownLanguages.FirstOrDefault(x => x.ProjectType == projectKind) ?? Unknown;
        }

        #region IEquatable<Language> and Equals

        public bool Equals(Language other)
        {
            if (ReferenceEquals(other, this))
            {
                return true;
            }

            return other != null
                && other.Id == this.Id
                && other.ProjectType == this.ProjectType;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as Language);
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }

        #endregion
    }
}
