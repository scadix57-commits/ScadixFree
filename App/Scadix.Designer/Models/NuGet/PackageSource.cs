using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Scadix.Designer.Models.NuGet
{
    /// <summary>
    /// نموذج لمصدر حزم NuGet
    /// </summary>
    public partial class PackageSource : ObservableObject
    {
        [ObservableProperty]
        private string _name = "";

        [ObservableProperty]
        private string _source = "";

        [ObservableProperty]
        private string _type = "Online"; // Online, Local, Network

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private bool _isDefault = false;

        public PackageSource()
        {
        }

        public PackageSource(string name, string source, string type = "Online", bool isEnabled = true)
        {
            Name = name;
            Source = source;
            Type = type;
            IsEnabled = isEnabled;
        }
    }
}
