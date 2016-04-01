﻿using SonarLint.VisualStudio.Integration.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SonarLint.VisualStudio.Integration
{
    [DebuggerDisplay("{Name} (ServerKey: {ServerKey}, ProjectType: {ProjectType}, IsSupported: {IsSupported})")]
    internal sealed class Language : IEquatable<Language>
    {
        public readonly static Language CSharp = new Language("cs", Strings.CSharpLanguageName, ProjectSystemHelper.CSharpProjectKind);
        public readonly static Language VBNET = new Language("vbnet", Strings.VBNetLanguageName, ProjectSystemHelper.VbProjectKind);

        /// <summary>
        /// The SonarQube server key.
        /// </summary>
        public string ServerKey { get; }

        /// <summary>
        /// The language display name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The VS project GUID for this language.
        /// </summary>
        public Guid ProjectType { get; set; }

        /// <summary>
        /// Returns whether or not this language is a supported project language for binding.
        /// </summary>
        public bool IsSupported => SupportedLanguages.Contains(this);

        /// <summary>
        /// All languages which are supported for project binding.
        /// </summary>
        public static IEnumerable<Language> SupportedLanguages
        {
            get
            {
                return new[] { CSharp };
                // We don't support VB.NET as the corresponding VB SonarQube server plugin has been
                // updated to support the connected experience.
                //return new[] { CSharp, VBNET };
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

        public Language(string serverKey, string name, string projectTypeGuid)
        {
            if (string.IsNullOrWhiteSpace(serverKey))
            {
                throw new ArgumentNullException(nameof(serverKey));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(projectTypeGuid))
            {
                throw new ArgumentNullException(nameof(projectTypeGuid));
            }

            this.ServerKey = serverKey;
            this.Name = name;
            this.ProjectType = new Guid(projectTypeGuid);
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
                return null;
            }

            return KnownLanguages.FirstOrDefault(x => x.ProjectType == projectKind);
        }

        #region IEquatable<Language> and Equals

        public bool Equals(Language other)
        {
            if (ReferenceEquals(other, this))
            {
                return true;
            }

            return other != null
                && other.ServerKey == this.ServerKey
                && other.ProjectType == this.ProjectType;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as Language);
        }

        public override int GetHashCode()
        {
            return this.ServerKey.GetHashCode();
        }

        #endregion
    }
}
