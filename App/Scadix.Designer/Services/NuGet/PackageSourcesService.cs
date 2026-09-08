using Scadix.Designer.Models.NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Scadix.Designer.Services.NuGet
{
    /// <summary>
    /// خدمة لإدارة مصادر حزم NuGet
    /// </summary>
    public class PackageSourcesService
    {
        private readonly string _configFilePath;
        private const string ConfigFileName = "nuget-sources.json";

        public PackageSourcesService()
        {
            // حفظ الإعدادات في مجلد المشروع
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MyDesigner"
            );

            Directory.CreateDirectory(appDataPath);
            _configFilePath = Path.Combine(appDataPath, ConfigFileName);
        }

        /// <summary>
        /// تحميل مصادر الحزم من الملف
        /// </summary>
        public List<PackageSource> LoadPackageSources()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    // إرجاع المصادر الافتراضية
                    return GetDefaultSources();
                }

                var json = File.ReadAllText(_configFilePath);
                var sources = JsonSerializer.Deserialize<List<PackageSource>>(json);

                if (sources == null || sources.Count == 0)
                {
                    return GetDefaultSources();
                }


                return sources;
            }
            catch (Exception ex)
            {
                MainWindowViewModel.ReportException(new Exception(ex.Message));
                return GetDefaultSources();
            }
        }

        /// <summary>
        /// حفظ مصادر الحزم إلى الملف
        /// </summary>
        public void SavePackageSources(List<PackageSource> sources)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(sources, options);
                File.WriteAllText(_configFilePath, json);


            }
            catch (Exception ex)
            {
                MainWindowViewModel.ReportException(new Exception(ex.Message));
                throw;
            }
        }

        /// <summary>
        /// الحصول على المصادر الافتراضية
        /// </summary>
        private List<PackageSource> GetDefaultSources()
        {
            return new List<PackageSource>
        {
            new PackageSource(
                "nuget.org",
                "https://api.nuget.org/v3/index.json",
                "Online",
                true
            )
            {
                IsDefault = true
            }
        };
        }

        /// <summary>
        /// إضافة مصدر جديد
        /// </summary>
        public void AddPackageSource(PackageSource source, List<PackageSource> existingSources)
        {
            if (existingSources.Any(s => s.Name.Equals(source.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A source with the name '{source.Name}' already exists.");
            }

            existingSources.Add(source);
            SavePackageSources(existingSources);
        }

        /// <summary>
        /// تحديث مصدر موجود
        /// </summary>
        public void UpdatePackageSource(PackageSource oldSource, PackageSource newSource, List<PackageSource> sources)
        {
            var index = sources.IndexOf(oldSource);
            if (index >= 0)
            {
                sources[index] = newSource;
                SavePackageSources(sources);
            }
        }

        /// <summary>
        /// حذف مصدر
        /// </summary>
        public void RemovePackageSource(PackageSource source, List<PackageSource> sources)
        {
            if (source.IsDefault)
            {
                throw new InvalidOperationException("Cannot remove the default NuGet source.");
            }

            sources.Remove(source);
            SavePackageSources(sources);
        }

        /// <summary>
        /// التحقق من صحة المصدر
        /// </summary>
        public bool ValidateSource(string source, string type)
        {
            try
            {
                if (type == "Online")
                {
                    // التحقق من أن URL صحيح
                    return Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
                }
                else if (type == "Local")
                {
                    // التحقق من أن المجلد موجود
                    return Directory.Exists(source);
                }
                else if (type == "Network")
                {
                    // التحقق من UNC path
                    return source.StartsWith(@"\\") && Directory.Exists(source);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
