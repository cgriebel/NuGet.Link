using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using NuGet.CommandLine;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Link.Command.Args;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Rules;
using NuGet.ProjectModel;
using NuGet.Versioning;

using static NuGet.Commands.PackCommandRunner;

namespace NuGet.Link.Command
{
    public class BaseCommandRunner
    {
        private PackArgs _packArgs;

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

        private readonly HashSet<string> _excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private CreateProjectFactory _createProjectFactory;

        public BaseCommandRunner(BaseArgs baseArgs)
            //: base(packArgs, ProjectFactory.ProjectCreator)
        {
            _createProjectFactory = ProjectFactory.ProjectCreator;
            var packArgs = new PackArgs();
            packArgs.CurrentDirectory = baseArgs.CurrentDirectory;
            packArgs.Logger = baseArgs.Console;
            packArgs.Arguments = new string[0];
            packArgs.MsBuildDirectory = new Lazy<string>(() => MsBuildUtility.GetMsBuildDirectoryFromMsBuildPath(null, null, baseArgs.Console).Value.Path);

            // Get the input file
            packArgs.Path = PackCommandRunner.GetInputFile(packArgs);

            // Set the current directory if the files being packed are in a different directory
            PackCommandRunner.SetupCurrentDirectory(packArgs);
            packArgs.Build = false;
            packArgs.Exclude = new string[0];
            switch (baseArgs.Verbosity)
            {
                case Verbosity.Detailed:
                    {
                        packArgs.LogLevel = LogLevel.Verbose;
                        break;
                    }
                case Verbosity.Normal:
                    {
                        packArgs.LogLevel = LogLevel.Information;
                        break;
                    }
                case Verbosity.Quiet:
                    {
                        packArgs.LogLevel = LogLevel.Minimal;
                        break;
                    }
            }
            this._packArgs = packArgs;
        }

        public PackageBuilder CreatePackageBuilder()
        {
            var path = Path.GetFullPath(Path.Combine(_packArgs.CurrentDirectory, _packArgs.Path));
            return CreatePackageBuilder(path);
        }

        private PackageBuilder CreatePackageBuilder(string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Equals(NuGetConstants.ManifestExtension, StringComparison.OrdinalIgnoreCase))
            {
                return CreatePackageBuilderFromNuspec(path);
            }
            else
            {
                return CreatePackageBuilderFromProjectFile(path);
            }
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
            PackageBuilder packageBuilder;
            if (string.IsNullOrEmpty(_packArgs.BasePath))
            {
                packageBuilder = new PackageBuilder(path, _packArgs.GetPropertyValue, !_packArgs.ExcludeEmptyDirectories);
            }
            else
            {
                packageBuilder = new PackageBuilder(path, _packArgs.BasePath, _packArgs.GetPropertyValue, !_packArgs.ExcludeEmptyDirectories);
            }

            InitCommonPackageBuilderProperties(packageBuilder);
            return packageBuilder;
        }

        private PackageBuilder CreatePackageBuilderFromProjectFile(string path)
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
            var mainPackageBuilder = factory.CreateBuilder(_packArgs.BasePath, version, _packArgs.Suffix, buildIfNeeded: true);

            if (mainPackageBuilder == null)
            {
                throw new PackagingException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PackFailed, path));
            }

            InitCommonPackageBuilderProperties(mainPackageBuilder);
            return mainPackageBuilder;
        }

        private void InitCommonPackageBuilderProperties(PackageBuilder builder)
        {
            if (!string.IsNullOrEmpty(_packArgs.Version))
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
            IEnumerable<IPackageRule> packageRules = RuleSet.PackageCreationRuleSet;
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
            if (!string.IsNullOrEmpty(issue.Message))
            {
                _packArgs.Logger.Log(issue);
            }
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
                                string.Format(CultureInfo.CurrentCulture, Strings.Warning_FileExcludedByDefault, physicalPackageFile.SourcePath),
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
            // For PhysicalPackageFiles, we want to filter by SourcePaths, the path on disk. The Path value maps to the TargetPath
            if (!(packageFile is PhysicalPackageFile physicalPackageFile))
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
                        throw new PackagingException(NuGetLogCode.NU5003, string.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidTargetFramework, reference.AssemblyName));
                    }
                }
            }
        }
    }
}
