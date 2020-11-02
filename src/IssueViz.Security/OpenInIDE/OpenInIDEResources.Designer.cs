﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SonarLint.VisualStudio.IssueVisualization.Security.OpenInIDE {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class OpenInIDEResources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal OpenInIDEResources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("SonarLint.VisualStudio.IssueVisualization.Security.OpenInIDE.OpenInIDEResources", typeof(OpenInIDEResources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [Open in IDE] Checking availability of port {0}.
        /// </summary>
        internal static string Factory_CheckingPort {
            get {
                return ResourceManager.GetString("Factory_CheckingPort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [Open in IDE] Creating request listener....
        /// </summary>
        internal static string Factory_CreatingListener {
            get {
                return ResourceManager.GetString("Factory_CreatingListener", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [Open in IDE] Failed to create a request listener - no available ports. The Open In IDE server feature will not be able to connect to this instance of Visual Studio..
        /// </summary>
        internal static string Factory_Failed_NoAvailablePorts {
            get {
                return ResourceManager.GetString("Factory_Failed_NoAvailablePorts", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [Open in IDE] Port {0} is unavailable.
        /// </summary>
        internal static string Factory_PortIsUnavailable {
            get {
                return ResourceManager.GetString("Factory_PortIsUnavailable", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [Open in IDE] Request listener created successfully. Listening on port {0}..
        /// </summary>
        internal static string Factory_Succeeded {
            get {
                return ResourceManager.GetString("Factory_Succeeded", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [Open in IDE] Invalid server parameter: {0}.
        /// </summary>
        internal static string OpenHotspot_InvalidServerParameter {
            get {
                return ResourceManager.GetString("OpenHotspot_InvalidServerParameter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to you must be in Connected Mode.
        /// </summary>
        internal static string Infobar_InvalidStateReason_NotInConnectedMode {
            get {
                return ResourceManager.GetString("Infobar_InvalidStateReason_NotInConnectedMode", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to you must connect to the right organization. Currently connected to: {0}.
        /// </summary>
        internal static string Infobar_InvalidStateReason_WrongOrganization {
            get {
                return ResourceManager.GetString("Infobar_InvalidStateReason_WrongOrganization", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to you must bind your solution to the right project. Currently bound to: {0}.
        /// </summary>
        internal static string Infobar_InvalidStateReason_WrongProject {
            get {
                return ResourceManager.GetString("Infobar_InvalidStateReason_WrongProject", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to you must connect to the right server. Currently connected to: {0}.
        /// </summary>
        internal static string Infobar_InvalidStateReason_WrongServer {
            get {
                return ResourceManager.GetString("Infobar_InvalidStateReason_WrongServer", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Please connect to {0}, organization {1}, and bind to project &apos;{2}&apos;..
        /// </summary>
        internal static string Inforbar_Instructions_SonarCloud {
            get {
                return ResourceManager.GetString("Inforbar_Instructions_SonarCloud", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Please connect to {0} and bind to project &apos;{1}&apos;..
        /// </summary>
        internal static string Inforbar_Instructions_SonarQube {
            get {
                return ResourceManager.GetString("Inforbar_Instructions_SonarQube", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Could not handle Open in IDE request: {0}. {1}.
        /// </summary>
        internal static string Inforbar_InvalidState {
            get {
                return ResourceManager.GetString("Inforbar_InvalidState", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [Open in IDE] Missing request parameter: {0}.
        /// </summary>
        internal static string OpenHotspot_MissingParameter {
            get {
                return ResourceManager.GetString("OpenHotspot_MissingParameter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [Open in IDE] Handing request: {0}.
        /// </summary>
        internal static string Pipeline_HandlingRequest {
            get {
                return ResourceManager.GetString("Pipeline_HandlingRequest", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [Open in IDE] Error handling request: {0}.
        /// </summary>
        internal static string Pipeline_UnhandledError {
            get {
                return ResourceManager.GetString("Pipeline_UnhandledError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [Open in IDE] Detailed error: {0}.
        /// </summary>
        internal static string Pipeline_UnhandledError_Detailed {
            get {
                return ResourceManager.GetString("Pipeline_UnhandledError_Detailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [Open in IDE] Unrecognized request received: {0}.
        /// </summary>
        internal static string Pipeline_UnrecognizedRequest {
            get {
                return ResourceManager.GetString("Pipeline_UnrecognizedRequest", resourceCulture);
            }
        }
    }
}
