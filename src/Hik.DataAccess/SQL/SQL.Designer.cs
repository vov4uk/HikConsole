﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Hik.DataAccess.SQL {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class SQL {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal SQL() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Hik.DataAccess.SQL.SQL", typeof(SQL).Assembly);
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
        ///   Looks up a localized string similar to 
        ///PRAGMA foreign_keys = off;
        ///BEGIN TRANSACTION;
        ///
        ///CREATE TABLE IF NOT EXISTS DailyStatistics (
        ///    Id            INTEGER       NOT NULL CONSTRAINT PK_DailyStatistics PRIMARY KEY AUTOINCREMENT,
        ///    JobTriggerId  INTEGER       NOT NULL,
        ///    Period        DATETIME2 (0) NOT NULL,
        ///    FilesCount    INTEGER       NOT NULL,
        ///    FilesSize     INTEGER       NOT NULL,
        ///    TotalDuration INTEGER,
        ///    CONSTRAINT FK_DailyStatistics_JobTrigger_JobTriggerId FOREIGN KEY ( JobTriggerId ) REFERENCES JobTrigger (Id) O [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string _20230331000000_init {
            get {
                return ResourceManager.GetString("_20230331000000_init", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ALTER TABLE JobTrigger ADD COLUMN ClassName text default &apos;&apos;;
        ///ALTER TABLE JobTrigger ADD COLUMN Config text default &apos;&apos;;
        ///ALTER TABLE JobTrigger ADD COLUMN Description text default &apos;&apos;;
        ///ALTER TABLE JobTrigger ADD COLUMN CronExpression text default &apos;&apos;;
        ///ALTER TABLE JobTrigger ADD COLUMN RunAsTask BOOLEAN       DEFAULT (true);
        ///ALTER TABLE JobTrigger ADD COLUMN IsEnabled BOOLEAN       DEFAULT (true);
        ///.
        /// </summary>
        internal static string _20231012000000_update_jobtriggers {
            get {
                return ResourceManager.GetString("_20231012000000_update_jobtriggers", resourceCulture);
            }
        }
    }
}
