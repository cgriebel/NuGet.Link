using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Rules;
using NuGet.ProjectModel;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Murphy.SymbolicLink;

namespace NuGet.Link.Command
{
    public class LinkCommandRunner : PackCommandRunner
    {
        public static string BasePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NugetLink");


        private PackArgs _packArgs;
        private PackageBuilder _packageBuilder;
        private CreateProjectFactory _createProjectFactory;
        private const string Configuration = "configuration";

        private static readonly HashSet<string> _allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            NuGetConstants.ManifestExtension,
            ".csproj",
            ".vbproj",
            ".fsproj",
            ".nproj",
            ".btproj",
            ".dxjsproj",
            ".json"
        };

        private static readonly string[] _defaultExcludes = new[] {
            // Exclude previous package files
            @"**\*".Replace('\\', Path.DirectorySeparatorChar) + NuGetConstants.PackageExtension,
            // Exclude all files and directories that begin with "."
            @"**\\.**".Replace('\\', Path.DirectorySeparatorChar), ".**"
        };

        // Target file paths to exclude when building the lib package for symbol server scenario
        private static readonly string[] _libPackageExcludes = new[] {
            @"**\*.pdb".Replace('\\', Path.DirectorySeparatorChar),
            @"src\**\*".Replace('\\', Path.DirectorySeparatorChar)
        };

        // Target file paths to exclude when building the symbols package for symbol server scenario
        private static readonly string[] _symbolPackageExcludes = new[] {
            @"content\**\*".Replace('\\', Path.DirectorySeparatorChar),
            @"tools\**\*.ps1".Replace('\\', Path.DirectorySeparatorChar)
        };

        private static readonly IReadOnlyList<string> defaultIncludeFlags = LibraryIncludeFlagUtils.NoContent.ToString().Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        private readonly HashSet<string> _excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public LinkCommandRunner(PackArgs packArgs, CreateProjectFactory createProjectFactory, PackageBuilder packageBuilder)
            : base(packArgs, createProjectFactory, packageBuilder)
        {
            this._packageBuilder = packageBuilder;
            Init(packArgs, createProjectFactory);
        }

        public LinkCommandRunner(PackArgs packArgs, CreateProjectFactory createProjectFactory)
            : base(packArgs, createProjectFactory)
        {
            Init(packArgs, createProjectFactory);
        }

        private void Init(PackArgs packArgs, CreateProjectFactory createProjectFactory)
        {
            this._createProjectFactory = createProjectFactory;
            this._packArgs = packArgs;
            Rules = RuleSet.PackageCreationRuleSet;
            GenerateNugetPackage = true;
        }

        public new void BuildPackage()
        {
            var path = Path.GetFullPath(Path.Combine(_packArgs.CurrentDirectory, _packArgs.Path));
            LinkPackage(path);
        }

        private void LinkPackage(string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Equals(NuGetConstants.ManifestExtension, StringComparison.OrdinalIgnoreCase))
            {
                LinkFromNuspec(path);
            }
            else
            {
                LinkFromProjectFile(path);
            }
        }

        private PackageArchiveReader LinkFromNuspec(string path)
        {
            PackageBuilder packageBuilder = CreatePackageBuilderFromNuspec(path);

            PackageArchiveReader packageArchiveReader = null;

            InitCommonPackageBuilderProperties(packageBuilder);

            if (_packArgs.InstallPackageToOutputPath)
            {
                string outputPath = GetOutputPath(packageBuilder, _packArgs);
                packageArchiveReader = BuildPackage(packageBuilder, outputPath: outputPath);
            }
            else
            {
                if (_packArgs.Symbols && packageBuilder.Files.Any())
                {
                    // remove source related files when building the lib package
                    ExcludeFilesForLibPackage(packageBuilder.Files);

                    if (!packageBuilder.Files.Any())
                    {
                        throw new PackagingException(NuGetLogCode.NU5004, string.Format(CultureInfo.CurrentCulture, Strings.Error_PackageCommandNoFilesForLibPackage, path, Strings.NuGetDocs));
                    }
                }

                packageArchiveReader = BuildPackage(packageBuilder);

                if (_packArgs.Symbols)
                {
                    BuildSymbolsPackage(path);
                }
            }

            if (packageArchiveReader != null && !_packArgs.NoPackageAnalysis)
            {
                AnalyzePackage(packageArchiveReader);
            }

            return packageArchiveReader;
        }

        private PackageBuilder CreatePackageBuilderFromNuspec(string path)
        {
            // Set the version property if the flag is set
            if (!string.IsNullOrEmpty(_packArgs.Version))
            {
                _packArgs.Properties["version"] = _packArgs.Version;
            }

            // If a nuspec file is being set via dotnet.exe then the warning properties and logger has already been initialized via PackTask.
            if (_packArgs.WarningProperties == null)
            {
                _packArgs.WarningProperties = WarningProperties.GetWarningProperties(
                treatWarningsAsErrors: _packArgs.GetPropertyValue("TreatWarningsAsErrors") ?? string.Empty,
                warningsAsErrors: _packArgs.GetPropertyValue("WarningsAsErrors") ?? string.Empty,
                noWarn: _packArgs.GetPropertyValue("NoWarn") ?? string.Empty);
                _packArgs.Logger = new PackCollectorLogger(_packArgs.Logger, _packArgs.WarningProperties);
            }

            if (string.IsNullOrEmpty(_packArgs.BasePath))
            {
                return new PackageBuilder(path, _packArgs.GetPropertyValue, !_packArgs.ExcludeEmptyDirectories);
            }
            return new PackageBuilder(path, _packArgs.BasePath, _packArgs.GetPropertyValue, !_packArgs.ExcludeEmptyDirectories);
        }

        private void LinkFromProjectFile(string path)
        {
            // PackTargetArgs is only set for dotnet.exe pack code path, hence the check.
            if ((string.IsNullOrEmpty(_packArgs.MsBuildDirectory?.Value) || _createProjectFactory == null) && _packArgs.PackTargetArgs == null)
            {
                throw new PackagingException(NuGetLogCode.NU5009, string.Format(CultureInfo.CurrentCulture, Strings.Error_CannotFindMsbuild));
            }

            var factory = _createProjectFactory.Invoke(_packArgs, path);
            if (_packArgs.WarningProperties == null && _packArgs.PackTargetArgs == null)
            {
                _packArgs.WarningProperties = factory.GetWarningPropertiesForProject();
                // Reinitialize the logger with Console as the inner logger and the obtained warning properties
                _packArgs.Logger = new PackCollectorLogger(_packArgs.Logger, _packArgs.WarningProperties);
                factory.Logger = _packArgs.Logger;
            }

            // Add the additional Properties to the properties of the Project Factory
            foreach (var property in _packArgs.Properties)
            {
                if (factory.GetProjectProperties().ContainsKey(property.Key))
                {
                    _packArgs.Logger.Log(PackagingLogMessage.CreateWarning(
                        string.Format(CultureInfo.CurrentCulture, Strings.Warning_DuplicatePropertyKey, property.Key),
                        NuGetLogCode.NU5114));
                }
                factory.GetProjectProperties()[property.Key] = property.Value;
            }

            NuGetVersion version = null;
            if (_packArgs.Version != null)
            {
                version = new NuGetVersion(_packArgs.Version);
            }

            // Create a builder for the main package as well as the sources/symbols package
            var mainPackageBuilder = factory.CreateBuilder(_packArgs.BasePath, version, _packArgs.Suffix, buildIfNeeded: true, builder: this._packageBuilder);

            if (mainPackageBuilder == null)
            {
                throw new PackagingException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PackFailed, path));
            }

            InitCommonPackageBuilderProperties(mainPackageBuilder);

            var packageRoot = Path.Combine(BasePath, mainPackageBuilder.Id, mainPackageBuilder.Version.ToNormalizedString());
            Directory.CreateDirectory(packageRoot);
            foreach(var file in mainPackageBuilder.Files)
            {
                if(file is PhysicalPackageFile physicalFile)
                {
                    var target = Path.Combine(packageRoot, file.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));

                    // TODO: Prompt for this, just delete for now
                    if (File.Exists(target))
                    {
                        File.Delete(target);
                    }

                    SymbolicLink.create(physicalFile.SourcePath, target);
                }
            }
        }

        private void InitCommonPackageBuilderProperties(PackageBuilder builder)
        {
            if (!String.IsNullOrEmpty(_packArgs.Version))
            {
                builder.Version = new NuGetVersion(_packArgs.Version);
                builder.HasSnapshotVersion = false;
            }

            if (!string.IsNullOrEmpty(_packArgs.Suffix) && !builder.HasSnapshotVersion)
            {
                string version = VersionFormatter.Instance.Format("V", builder.Version, VersionFormatter.Instance);
                builder.Version = new NuGetVersion($"{version}-{_packArgs.Suffix}");
            }

            if (_packArgs.Serviceable)
            {
                builder.Serviceable = true;
            }

            if (_packArgs.MinClientVersion != null)
            {
                builder.MinClientVersion = _packArgs.MinClientVersion;
            }


            CheckForUnsupportedFrameworks(builder);

            ExcludeFiles(builder.Files);
        }

        internal static void ExcludeFilesForLibPackage(ICollection<IPackageFile> files)
        {
            PathResolver.FilterPackageFiles(files, file => file.Path, _libPackageExcludes);
        }

        private void BuildSymbolsPackage(string path)
        {
            var symbolsBuilder = CreatePackageBuilderFromNuspec(path);
            if (_packArgs.SymbolPackageFormat == SymbolPackageFormat.Snupkg) // Snupkgs can only have 1 PackageType. 
            {
                symbolsBuilder.PackageTypes.Clear();
                symbolsBuilder.PackageTypes.Add(PackageType.SymbolsPackage);
            }

            // remove unnecessary files when building the symbols package
            ExcludeFilesForSymbolPackage(symbolsBuilder.Files, _packArgs.SymbolPackageFormat);

            if (!symbolsBuilder.Files.Any())
            {
                throw new PackagingException(NuGetLogCode.NU5005, string.Format(CultureInfo.CurrentCulture, Strings.Error_PackageCommandNoFilesForSymbolsPackage, path, Strings.NuGetDocs));
            }

            var outputPath = GetOutputPath(symbolsBuilder, _packArgs, symbols: true);

            InitCommonPackageBuilderProperties(symbolsBuilder);
            BuildPackage(symbolsBuilder, outputPath)?.Dispose();
        }

        internal static void ExcludeFilesForSymbolPackage(ICollection<IPackageFile> files, SymbolPackageFormat symbolPackageFormat)
        {
            PathResolver.FilterPackageFiles(files, file => file.Path, _symbolPackageExcludes);
            if (symbolPackageFormat == SymbolPackageFormat.Snupkg)
            {
                var toRemove = files.Where(t => !string.Equals(Path.GetExtension(t.Path), ".pdb", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var fileToRemove in toRemove)
                {
                    files.Remove(fileToRemove);
                }
            }
        }

        internal void AnalyzePackage(PackageArchiveReader package)
        {
            IEnumerable<IPackageRule> packageRules = Rules;
            IList<PackagingLogMessage> issues = new List<PackagingLogMessage>();

            foreach (var rule in packageRules)
            {
                issues.AddRange(rule.Validate(package).OrderBy(p => p.Code.ToString(), StringComparer.CurrentCulture));
            }

            if (issues.Count > 0)
            {
                foreach (var issue in issues)
                {
                    PrintPackageIssue(issue);
                }
            }
        }

        private void PrintPackageIssue(PackagingLogMessage issue)
        {
            if (!String.IsNullOrEmpty(issue.Message))
            {
                _packArgs.Logger.Log(issue);
            }
        }

        private void WriteLine(string message, object arg = null)
        {
            _packArgs.Logger.Log(PackagingLogMessage.CreateMessage(String.Format(CultureInfo.CurrentCulture, message, arg?.ToString()), LogLevel.Information));
        }

        internal void ExcludeFiles(ICollection<IPackageFile> packageFiles)
        {
            // Always exclude the nuspec file
            // Review: This exclusion should be done by the package builder because it knows which file would collide with the auto-generated
            // manifest file.
            var wildCards = _excludes.Concat(new[] { @"**\*" + NuGetConstants.ManifestExtension });
            if (!_packArgs.NoDefaultExcludes)
            {
                // The user has not explicitly disabled default filtering.
                var excludedFiles = PathResolver.GetFilteredPackageFiles(packageFiles, ResolvePath, _defaultExcludes);
                if (excludedFiles != null)
                {
                    foreach (var file in excludedFiles)
                    {
                        if (file is PhysicalPackageFile)
                        {
                            var physicalPackageFile = file as PhysicalPackageFile;
                            _packArgs.Logger.Log(PackagingLogMessage.CreateWarning(
                                String.Format(CultureInfo.CurrentCulture, Strings.Warning_FileExcludedByDefault, physicalPackageFile.SourcePath),
                                NuGetLogCode.NU5119));

                        }
                    }
                }
            }
            wildCards = wildCards.Concat(_packArgs.Exclude);

            PathResolver.FilterPackageFiles(packageFiles, ResolvePath, wildCards);
        }

        private string ResolvePath(IPackageFile packageFile)
        {
            var basePath = string.IsNullOrEmpty(_packArgs.BasePath) ? _packArgs.CurrentDirectory : _packArgs.BasePath;

            return ResolvePath(packageFile, basePath);
        }

        private static string ResolvePath(IPackageFile packageFile, string basePath)
        {
            var physicalPackageFile = packageFile as PhysicalPackageFile;

            // For PhysicalPackageFiles, we want to filter by SourcePaths, the path on disk. The Path value maps to the TargetPath
            if (physicalPackageFile == null)
            {
                return packageFile.Path;
            }

            var path = physicalPackageFile.SourcePath;

            // Make sure that the basepath has a directory separator
            int index = path.IndexOf(PathUtility.EnsureTrailingSlash(basePath), StringComparison.OrdinalIgnoreCase);
            if (index != -1)
            {
                // Since wildcards are going to be relative to the base path, remove the BasePath portion of the file's source path.
                // Also remove any leading path separator slashes
                path = path.Substring(index + basePath.Length).TrimStart(Path.DirectorySeparatorChar);
            }

            return path;
        }
        private void CheckForUnsupportedFrameworks(PackageBuilder builder)
        {
            foreach (var reference in builder.FrameworkReferences)
            {
                foreach (var framework in reference.SupportedFrameworks)
                {
                    if (framework.IsUnsupported)
                    {
                        throw new PackagingException(NuGetLogCode.NU5003, String.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidTargetFramework, reference.AssemblyName));
                    }
                }
            }
        }

    }
}
