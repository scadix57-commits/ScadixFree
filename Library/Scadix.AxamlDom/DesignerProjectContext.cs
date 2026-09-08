using System;
using System.Collections.Generic;
using System.Text;

namespace Scadix.AxamlDom
{
    public static class DesignerProjectContext
    {
        #region Properties

        /// <summary>
        /// Gets the project's root path on disk.
        /// </summary>
        public static string ProjectRootPath { get; set; }

        /// <summary>المسار الكامل لمجلد المشروع الحالي</summary>
        public static string? CurrentProjectPath { get; set; }

        /// <summary>اسم المشروع (assembly name) لبناء مسارات avares://</summary>
        public static string? CurrentProjectName { get; set; }

        /// <summary>الاسم البرمجي للتجمع (Assembly Name) للمشروع</summary>
        public static string? CurrentProjectAssemblyName { get; set; }

        /// <summary>المسار الكامل لملف XAML المفتوح حالياً في المصمم</summary>
        public static string? CurrentXamlFilePath { get; set; }

        /// <summary>Global error reporter for libraries to signal errors to the main project UI</summary>
        public static Action<Exception, string>? ErrorReporter { get; set; }

        /// <summary>
        /// Reports an error from a library component to the global error sink.
        /// </summary>
        public static void ReportError(Exception ex, string context, string? code = null)
        {
            ErrorReporter?.Invoke(ex, context);
        }

        #endregion
    }
}
