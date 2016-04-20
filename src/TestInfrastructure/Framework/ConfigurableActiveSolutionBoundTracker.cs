//-----------------------------------------------------------------------
// <copyright file="ConfigurableActiveSolutionBoundTracker.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    public class ConfigurableActiveSolutionBoundTracker : IActiveSolutionBoundTracker
    {
        public bool IsActiveSolutionBound { get; set; }

        public void Dispose()
        {
            // Default interface implementation
        }
    }
}
