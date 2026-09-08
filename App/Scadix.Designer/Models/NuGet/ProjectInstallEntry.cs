using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace Scadix.Designer.Models.NuGet
{
    /// <summary>
    /// Represents one project row in the "install into project" grid.
    /// </summary>
    public partial class ProjectInstallEntry : ObservableObject
    {
        /// <summary>Display name of the project (e.g. "App\MyDesigner.XamlDesigner")</summary>
        public string ProjectName { get; set; } = "";

        /// <summary>Full path to the .csproj file</summary>
        public string ProjectPath { get; set; } = "";

        /// <summary>Currently installed version, null if not installed</summary>
        [ObservableProperty]
        private string? _installedVersion;

        /// <summary>Version that will be installed (bound to the version ComboBox)</summary>
        [ObservableProperty]
        private string? _selectedVersion;

        /// <summary>Whether the package is installed in this project</summary>
        public bool IsInstalled => InstalledVersion != null;

        /// <summary>Checked = user wants to install/update into this project</summary>
        [ObservableProperty]
        private bool _isChecked;
    }

}
