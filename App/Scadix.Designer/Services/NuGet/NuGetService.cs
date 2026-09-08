using Scadix.Designer.Models.NuGet;
using Scadix.Designer.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Scadix.Designer.Services.NuGet
{
    public class NuGetService
    {
        private readonly HttpClient _httpClient;
        private const string NuGetApiUrl = "https://api.nuget.org/v3/index.json";
        private string? _searchQueryServiceUrl;

        private bool? _isOnline;
        public bool IsOnline
        {
            get
            {
                if (_isOnline.HasValue) return _isOnline.Value;
                try
                {
                    // Quick check: is any network adapter active?
                    if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                    {
                        _isOnline = false;
                        return false;
                    }
                    // Real check: can we reach a reliable host? (short timeout)
                    using var ping = new System.Net.NetworkInformation.Ping();
                    var reply = ping.Send("8.8.8.8", 1000); // 1s timeout
                    _isOnline = reply.Status == System.Net.NetworkInformation.IPStatus.Success;
                }
                catch { _isOnline = false; }
                return _isOnline.Value;
            }
            set => _isOnline = value;
        }

        public NuGetService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MyDesigner-NuGetManager/1.0");
        }

        /// <summary>
        /// Initialize NuGet service and get service URLs
        /// </summary>
        private async Task InitializeAsync()
        {
            if (_searchQueryServiceUrl != null)
                return;

            if (!IsOnline)
            {
                _searchQueryServiceUrl = "https://azuresearch-usnc.nuget.org/query";
                return;
            }

            try
            {
                var response = await _httpClient.GetStringAsync(NuGetApiUrl);
                var doc = JsonDocument.Parse(response);

                var resources = doc.RootElement.GetProperty("resources");
                foreach (var resource in resources.EnumerateArray())
                {
                    var type = resource.GetProperty("@type").GetString();
                    if (type == "SearchQueryService")
                    {
                        _searchQueryServiceUrl = resource.GetProperty("@id").GetString();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindowViewModel.ReportException(new Exception(ex.Message));
                // Fallback to direct URL
                _searchQueryServiceUrl = "https://azuresearch-usnc.nuget.org/query";
            }
        }

        /// <summary>
        /// Search for packages on NuGet.org
        /// </summary>
        public async Task<List<NuGetPackageInfo>> SearchPackagesAsync(string searchTerm, int skip = 0, int take = 20, bool includePrerelease = false)
        {
            return await SearchPackagesAsync(searchTerm, null, skip, take, includePrerelease);
        }

        /// <summary>
        /// Search for packages in a specific source (NuGet.org or local)
        /// </summary>
        public async Task<List<NuGetPackageInfo>> SearchPackagesAsync(string searchTerm, string? sourceUrl, int skip = 0, int take = 20, bool includePrerelease = false)
        {
            // Check if source is local (directory path)
            if (!string.IsNullOrEmpty(sourceUrl) && (Directory.Exists(sourceUrl) || sourceUrl.StartsWith("file:///")))
            {
                return await SearchLocalPackagesAsync(searchTerm, sourceUrl, skip, take, includePrerelease);
            }

            // Search in NuGet.org or online source
            await InitializeAsync();

            try
            {
                var url = $"{_searchQueryServiceUrl}?q={Uri.EscapeDataString(searchTerm)}&skip={skip}&take={take}&prerelease={includePrerelease.ToString().ToLower()}";
                var response = await _httpClient.GetStringAsync(url);
                var doc = JsonDocument.Parse(response);

                var packages = new List<NuGetPackageInfo>();
                var data = doc.RootElement.GetProperty("data");

                foreach (var item in data.EnumerateArray())
                {
                    var package = new NuGetPackageInfo
                    {
                        Id = item.GetProperty("id").GetString() ?? "",
                        Title = item.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                        Description = item.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                        Authors = item.TryGetProperty("authors", out var authors)
                            ? string.Join(", ", authors.EnumerateArray().Select(a => a.GetString()))
                            : "",
                        LatestVersion = item.GetProperty("version").GetString() ?? "",
                        DownloadCount = item.TryGetProperty("totalDownloads", out var downloads)
                            ? downloads.GetInt64()
                            : 0,
                        ProjectUrl = item.TryGetProperty("projectUrl", out var projectUrl)
                            ? projectUrl.GetString()
                            : null,
                        IconUrl = item.TryGetProperty("iconUrl", out var iconUrl)
                            ? iconUrl.GetString()
                            : null
                    };

                    // Get all versions
                    if (item.TryGetProperty("versions", out var versions))
                    {
                        package.Versions = versions.EnumerateArray()
                            .Select(v => v.GetProperty("version").GetString() ?? "")
                            .Where(v => !string.IsNullOrEmpty(v))
                            .Reverse()
                            .ToList();
                    }

                    if (package.Versions.Count == 0)
                    {
                        package.Versions.Add(package.LatestVersion);
                    }

                    packages.Add(package);
                }

                return packages;
            }
            catch (Exception ex)
            {
                MainWindowViewModel.ReportException(new Exception(ex.Message));
                return new List<NuGetPackageInfo>();
            }
        }

        /// <summary>
        /// Search for packages in a local directory
        /// </summary>
        private async Task<List<NuGetPackageInfo>> SearchLocalPackagesAsync(string searchTerm, string localPath, int skip = 0, int take = 20, bool includePrerelease = false)
        {
            var packages = new List<NuGetPackageInfo>();

            try
            {
                // Remove file:/// prefix if present
                if (localPath.StartsWith("file:///"))
                {
                    localPath = localPath.Substring(8);
                }

                if (!Directory.Exists(localPath))
                {

                    return packages;
                }



                // Get all .nupkg files
                var nupkgFiles = Directory.GetFiles(localPath, "*.nupkg", SearchOption.AllDirectories);


                foreach (var nupkgFile in nupkgFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(nupkgFile);

                        // Parse package ID and version from filename (format: PackageId.Version.nupkg)
                        // Version format: Major.Minor.Patch[-Suffix]
                        // Example: MyCompany.Package.1.2.3.nupkg or MyCompany.Package.1.2.3-beta.nupkg

                        string packageId = fileName;
                        string version = "1.0.0";

                        // Try to find version pattern (numbers with dots)
                        var versionMatch = System.Text.RegularExpressions.Regex.Match(
                            fileName,
                            @"\.(\d+\.\d+(?:\.\d+)?(?:\.\d+)?(?:-[\w\.\-]+)?)$"
                        );

                        if (versionMatch.Success)
                        {
                            version = versionMatch.Groups[1].Value;
                            packageId = fileName.Substring(0, versionMatch.Index);
                        }

                        // Apply search filter
                        if (!string.IsNullOrEmpty(searchTerm) &&
                            !packageId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Check if package already exists in list
                        var existingPackage = packages.FirstOrDefault(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));

                        if (existingPackage != null)
                        {
                            // Add version to existing package
                            if (!existingPackage.Versions.Contains(version))
                            {
                                existingPackage.Versions.Add(version);
                                existingPackage.Versions = existingPackage.Versions.OrderByDescending(v => v).ToList();
                                existingPackage.LatestVersion = existingPackage.Versions.First();
                            }
                        }
                        else
                        {
                            // Create new package entry
                            var package = new NuGetPackageInfo
                            {
                                Id = packageId,
                                Title = packageId,
                                Description = $"Local package from {Path.GetFileName(localPath)}",
                                Authors = "Local",
                                LatestVersion = version,
                                DownloadCount = 0,
                                Versions = new List<string> { version }
                            };

                            packages.Add(package);
                        }
                    }
                    catch (Exception ex)
                    {
                        MainWindowViewModel.ReportException(new Exception(ex.Message));
                    }
                }

                // Apply skip and take (but for local sources, if take is large, show all)
                if (take >= 1000)
                {
                    // Show all packages for local sources
                    packages = packages.Skip(skip).ToList();
                }
                else
                {
                    packages = packages.Skip(skip).Take(take).ToList();
                }


            }
            catch (Exception ex)
            {
                MainWindowViewModel.ReportException(new Exception(ex.Message));
            }

            return await Task.FromResult(packages);
        }

        /// <summary>
        /// Get installed packages from project file
        /// </summary>
        public async Task<List<NuGetPackageInfo>> GetInstalledPackagesAsync(string projectPath)
        {
            var packages = new List<NuGetPackageInfo>();

            try
            {
                if (!File.Exists(projectPath))
                    return packages;

                var doc = XDocument.Load(projectPath);
                var packageReferences = doc.Descendants("PackageReference");

                var centralVersions = GetCentralPackageVersions(projectPath);
                foreach (var packageRef in packageReferences)
                {
                    var id = packageRef.Attribute("Include")?.Value;
                    var version = packageRef.Attribute("Version")?.Value
                               ?? packageRef.Element("Version")?.Value;

                    if (string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(id))
                    {
                        centralVersions.TryGetValue(id, out version);
                    }

                    if (!string.IsNullOrEmpty(id))
                    {
                        // محاولة الحصول على معلومات من NuGet.org
                        try
                        {
                            var searchResults = await SearchPackagesAsync(id, 0, 1, false);
                            if (searchResults.Count > 0)
                            {
                                var packageInfo = searchResults[0];
                                packageInfo.InstalledVersion = version;
                                packageInfo.IsInstalled = true;
                                packages.Add(packageInfo);
                            }
                            else
                            {
                                packages.Add(new NuGetPackageInfo
                                {
                                    Id = id, Title = id,
                                    Description = "Local or custom package",
                                    Authors = "Unknown",
                                    InstalledVersion = version,
                                    LatestVersion = version,
                                    IsInstalled = true,
                                    Versions = new List<string> { version ?? "" }
                                });
                            }
                        }
                        catch
                        {
                            packages.Add(new NuGetPackageInfo
                            {
                                Id = id, Title = id,
                                Description = "Local or custom package",
                                Authors = "Unknown",
                                InstalledVersion = version,
                                LatestVersion = version,
                                IsInstalled = true,
                                Versions = new List<string> { version ?? "" }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindowViewModel.ReportException(new Exception(ex.Message));
            }

            return packages;
        }

        /// <summary>Fast local-only check — reads .csproj without calling NuGet.org.
        /// Supports Central Package Management (Directory.Packages.props).</summary>
        public List<NuGetPackageInfo> GetInstalledPackagesLocal(string projectPath)
        {
            var packages = new List<NuGetPackageInfo>();
            try
            {
                // إذا كان مجلداً، نبحث عن أول .csproj فيه
                if (Directory.Exists(projectPath))
                {
                    var csprojFiles = Directory.GetFiles(projectPath, "*.csproj", SearchOption.TopDirectoryOnly);
                    if (csprojFiles.Length == 0) return packages;
                    projectPath = csprojFiles[0];
                }

                if (!File.Exists(projectPath)) return packages;

                // ── Read central versions — walk up from .csproj directory ───────
                var centralVersions = GetCentralPackageVersions(projectPath);

                // ── Read PackageReference entries from .csproj ───────────────────
                var doc = XDocument.Load(projectPath);
                foreach (var packageRef in doc.Descendants()
                             .Where(e => e.Name.LocalName == "PackageReference"))
                {
                    var id = packageRef.Attribute("Include")?.Value;
                    if (string.IsNullOrEmpty(id)) continue;

                    var version = packageRef.Attribute("Version")?.Value
                               ?? packageRef.Elements()
                                            .FirstOrDefault(e => e.Name.LocalName == "Version")
                                            ?.Value;

                    if (string.IsNullOrEmpty(version))
                        centralVersions.TryGetValue(id, out version);

                    packages.Add(new NuGetPackageInfo
                    {
                        Id = id,
                        Title = id,
                        InstalledVersion = version,
                        LatestVersion = version,
                        IsInstalled = true,
                        Versions = string.IsNullOrEmpty(version)
                            ? new List<string>()
                            : new List<string> { version }
                    });
                }
            }
            catch { }
            return packages;
        }
        

        /// <summary>
        /// Get packages that have updates available
        /// </summary>
        public async Task<List<NuGetPackageInfo>> GetUpdatesAsync(string projectPath)
        {
            var installedPackages = await GetInstalledPackagesAsync(projectPath);
            var updates = new List<NuGetPackageInfo>();

            foreach (var package in installedPackages)
            {
                try
                {
                    var searchResults = await SearchPackagesAsync(package.Id, 0, 1, false);
                    if (searchResults.Count > 0)
                    {
                        var latestPackage = searchResults[0];
                        if (latestPackage.LatestVersion != package.InstalledVersion)
                        {
                            package.LatestVersion = latestPackage.LatestVersion;
                            package.HasUpdate = true;
                            package.Description = latestPackage.Description;
                            package.Authors = latestPackage.Authors;
                            package.Versions = latestPackage.Versions;
                            updates.Add(package);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MainWindowViewModel.ReportException(new Exception(ex.Message));
                }
            }

            return updates;
        }

        /// <summary>
        /// Install a package using dotnet CLI
        /// </summary>
        public async Task InstallPackageAsync(string projectPath, string packageId, string version, string? sourceUrl = null)
        {
            try
            {
                var projectDir = Path.GetDirectoryName(projectPath);

                // Build the command arguments
                var args = $"add \"{projectPath}\" package {packageId} --version {version}";

                // Add source parameter if specified
                if (!string.IsNullOrEmpty(sourceUrl))
                {
                    // Remove file:/// prefix if present
                    var source = sourceUrl;
                    if (source.StartsWith("file:///"))
                    {
                        source = source.Substring(8);
                    }

                    // Remove trailing backslash to avoid escaping the closing quote
                    source = source.TrimEnd('\\', '/');

                    // For local sources, only use the local source
                    // Dependencies will be resolved from default nuget.config sources
                    args += $" -s \"{source}\"";


                }



                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = args,
                    WorkingDirectory = projectDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    await process.WaitForExitAsync();




                    if (process.ExitCode != 0)
                    {
                        var errorMessage = !string.IsNullOrEmpty(error) ? error : output;
                        throw new Exception($"Failed to install package (exit code {process.ExitCode}):\n{errorMessage}");
                    }

                    
                }
            }
            catch (Exception ex)
            {
                MainWindowViewModel.ReportException(new Exception(ex.Message));
                throw;
            }
        }

        /// <summary>
        /// Uninstall a package using dotnet CLI, then run restore and refresh references.
        /// </summary>
        public async Task UninstallPackageAsync(string projectPath, string packageId)
        {
            try
            {
                var projectDir = Path.GetDirectoryName(projectPath);
                var args = $"remove \"{projectPath}\" package {packageId}";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = args,
                    WorkingDirectory = projectDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        throw new Exception($"Failed to uninstall package: {error}");
                    }

                    // dotnet remove package does NOT run restore automatically — do it explicitly
                    await RunRestoreAsync(projectPath);

                   
                }
            }
            catch (Exception ex)
            {
                MainWindowViewModel.ReportException(new Exception(ex.Message));
            }
        }

        /// <summary>
        /// Runs dotnet restore on the project and streams output to BuildOutputService.
        /// </summary>
        private async Task RunRestoreAsync(string projectPath)
        {
            
            try
            {
                var projectDir = Path.GetDirectoryName(projectPath);
                BuildOutputService.Instance.AppendLine($"Running dotnet restore...");

                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"restore \"{projectPath}\"",
                    WorkingDirectory = projectDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.OutputDataReceived += (_, e) => { if (e.Data != null) BuildOutputService.Instance.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) BuildOutputService.Instance.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                    BuildOutputService.Instance.AppendLine($"dotnet restore exited with code {process.ExitCode}");
            }
            catch (Exception ex)
            {
                BuildOutputService.Instance.AppendLine($"dotnet restore error: {ex.Message}");
            }
        }

        /// <summary>
        /// Update a package to a new version
        /// </summary>
        public async Task UpdatePackageAsync(string projectPath, string packageId, string newVersion)
        {
            // Update is essentially uninstall + install
            await UninstallPackageAsync(projectPath, packageId);
            await InstallPackageAsync(projectPath, packageId, newVersion);
        }
        /// <summary>
        /// Reads central versions by walking up from the project directory.
        /// </summary>
        private Dictionary<string, string> GetCentralPackageVersions(string projectPath)
        {
            var centralVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var dir = File.Exists(projectPath)
                    ? Path.GetDirectoryName(Path.GetFullPath(projectPath))
                    : Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                while (!string.IsNullOrEmpty(dir))
                {
                    var propsFile = Path.Combine(dir, "Directory.Packages.props");
                    if (File.Exists(propsFile))
                    {
                        var propsDoc = XDocument.Load(propsFile);
                        foreach (var pv in propsDoc.Descendants().Where(e => e.Name.LocalName == "PackageVersion"))
                        {
                            var pvId = pv.Attribute("Include")?.Value;
                            var pvVer = pv.Attribute("Version")?.Value
                                     ?? pv.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value;

                            if (!string.IsNullOrEmpty(pvId) && !string.IsNullOrEmpty(pvVer))
                                centralVersions[pvId] = pvVer;
                        }
                        break;
                    }
                    var parent = Directory.GetParent(dir);
                    if (parent == null || parent.FullName == dir) break;
                    dir = parent.FullName;
                }
            }
            catch { }
            return centralVersions;
        }
    }
}
