﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SonarLint.VisualStudio.Integration.Vsix.Analysis {
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
    internal class AnalysisStrings {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal AnalysisStrings() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("SonarLint.VisualStudio.Integration.Vsix.Analysis.AnalysisStrings", typeof(AnalysisStrings).Assembly);
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
        ///   Looks up a localized string similar to User settings have changed but the solution is in connected mode. Open documents will not be re-analysed..
        /// </summary>
        internal static string ConfigMonitor_IgnoringUserSettingsChanged {
            get {
                return ResourceManager.GetString("ConfigMonitor_IgnoringUserSettingsChanged", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Suppressions have been updated. Open documents will be re-analysed..
        /// </summary>
        internal static string ConfigMonitor_SuppressionsUpdated {
            get {
                return ResourceManager.GetString("ConfigMonitor_SuppressionsUpdated", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to User settings have changed. Open documents will be re-analysed..
        /// </summary>
        internal static string ConfigMonitor_UserSettingsChanged {
            get {
                return ResourceManager.GetString("ConfigMonitor_UserSettingsChanged", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Disabled rule &quot;{0}&quot;.
        /// </summary>
        internal static string DisableRule_DisabledRule {
            get {
                return ResourceManager.GetString("DisableRule_DisabledRule", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Error in DisableCommandRule.QueryStatus:
        ///  Error: {0}.
        /// </summary>
        internal static string DisableRule_ErrorCheckingCommandStatus {
            get {
                return ResourceManager.GetString("DisableRule_ErrorCheckingCommandStatus", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Error disabling rule &quot;{0}&quot;:
        ///  Error: {1}.
        /// </summary>
        internal static string DisableRule_ErrorDisablingRule {
            get {
                return ResourceManager.GetString("DisableRule_ErrorDisablingRule", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {unknown}.
        /// </summary>
        internal static string DisableRule_UnknownErrorCode {
            get {
                return ResourceManager.GetString("DisableRule_UnknownErrorCode", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Error handling file change notification: {0}.
        /// </summary>
        internal static string FileMonitor_ErrorHandlingFileChange {
            get {
                return ResourceManager.GetString("FileMonitor_ErrorHandlingFileChange", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to File changed: {0}. Change type: {1}.
        /// </summary>
        internal static string FileMonitor_FileChanged {
            get {
                return ResourceManager.GetString("FileMonitor_FileChanged", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Created directory: {0}.
        /// </summary>
        internal static string FileMonitory_CreatedDirectory {
            get {
                return ResourceManager.GetString("FileMonitory_CreatedDirectory", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Aborted analysis of {0}, analysis has been re-triggered or has timed-out..
        /// </summary>
        internal static string MSG_AnalysisAborted {
            get {
                return ResourceManager.GetString("MSG_AnalysisAborted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Finished analyzing {0}, analysis time: {1}s.
        /// </summary>
        internal static string MSG_AnalysisComplete {
            get {
                return ResourceManager.GetString("MSG_AnalysisComplete", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to analyze {0}, exception details: {1}.
        /// </summary>
        internal static string MSG_AnalysisFailed {
            get {
                return ResourceManager.GetString("MSG_AnalysisFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Analyzing {0}.
        /// </summary>
        internal static string MSG_AnalysisStarted {
            get {
                return ResourceManager.GetString("MSG_AnalysisStarted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Found {0} issue(s) for {1}.
        /// </summary>
        internal static string MSG_FoundIssues {
            get {
                return ResourceManager.GetString("MSG_FoundIssues", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SonarLint: Failed to analyze {0}. See the Output Window for more information..
        /// </summary>
        internal static string Notifier_AnalysisFailed {
            get {
                return ResourceManager.GetString("Notifier_AnalysisFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SonarLint: Finished analyzing {0}.
        /// </summary>
        internal static string Notifier_AnalysisFinished {
            get {
                return ResourceManager.GetString("Notifier_AnalysisFinished", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SonarLint: Analyzing {0}.
        /// </summary>
        internal static string Notifier_AnalysisStarted {
            get {
                return ResourceManager.GetString("Notifier_AnalysisStarted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Error re-analysing open documents: {0}.
        /// </summary>
        internal static string ReanalysisStatusBar_Error {
            get {
                return ResourceManager.GetString("ReanalysisStatusBar_Error", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SonarLint: re-analysing open documents. Completed {0} of {1}.
        /// </summary>
        internal static string ReanalysisStatusBar_InProgress {
            get {
                return ResourceManager.GetString("ReanalysisStatusBar_InProgress", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Error re-analysing open documents: {0}.
        /// </summary>
        internal static string Requester_Error {
            get {
                return ResourceManager.GetString("Requester_Error", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Default settings will be used.
        /// </summary>
        internal static string Settings_UsingDefaultSettings {
            get {
                return ResourceManager.GetString("Settings_UsingDefaultSettings", resourceCulture);
            }
        }
    }
}
