﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using NuGet.CommandLine;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Link.Command;
using NuGet.Link.Command.Args;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Rules;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Link.Command
{
    public class BaseCommandRunner : MSBuildUser
    {
        public static string BasePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NugetLink");
        protected PackArgs _packArgs;

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

        public BaseCommandRunner(BaseArgs baseArgs)
        {
            var packArgs = new PackArgs
            {
                CurrentDirectory = baseArgs.CurrentDirectory,
                Logger = baseArgs.Console,
                Arguments = new string[0],
                MsBuildDirectory = new Lazy<string>(() => MsBuildUtility.GetMsBuildDirectoryFromMsBuildPath(null, null, baseArgs.Console).Value.Path)
            };

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

        protected PackageBuilder CreatePackageBuilder()
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
            if ((string.IsNullOrEmpty(_packArgs.MsBuildDirectory?.Value)) && _packArgs.PackTargetArgs == null)
            {
                throw new PackagingException(NuGetLogCode.NU5009, string.Format(CultureInfo.CurrentCulture, Strings.Error_CannotFindMsbuild));
            }

            var factory = CreateProjectFactory(_packArgs, path);
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

        public IProjectFactory CreateProjectFactory(PackArgs packArgs, string path)
        {
            var msbuildDirectory = packArgs.MsBuildDirectory.Value;
            LoadAssemblies(msbuildDirectory);

            // Create project, allowing for assembly load failures
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(AssemblyResolve);
            dynamic project;
            try
            {
                var projectCollection = Activator.CreateInstance(_projectCollectionType);
                project = Activator.CreateInstance(
                    _projectType,
                    path,
                    packArgs.Properties,
                    null,
                    projectCollection);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(AssemblyResolve);
            }

            return new ProjectFactory(packArgs.MsBuildDirectory.Value, project)
            {
                IsTool = packArgs.Tool,
                LogLevel = packArgs.LogLevel,
                Logger = packArgs.Logger,
                MachineWideSettings = packArgs.MachineWideSettings,
                Build = packArgs.Build,
                IncludeReferencedProjects = packArgs.IncludeReferencedProjects,
                SymbolPackageFormat = packArgs.SymbolPackageFormat
            };
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

        public PackageArchiveReader BuildPackage(PackageBuilder builder, string outputPath = null)
        {
            outputPath = outputPath ?? GetOutputPath(builder, false, builder.Version);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            // Track if the package file was already present on disk
            bool isExistingPackage = File.Exists(outputPath);
            try
            {
                using (Stream stream = File.Create(outputPath))
                {
                    builder.Save(stream);
                }
            }
            catch
            {
                if (!isExistingPackage && File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
                throw;
            }

            if (_packArgs.LogLevel == LogLevel.Verbose)
            {
                PrintVerbose(outputPath, builder);
            }

            if (_packArgs.InstallPackageToOutputPath)
            {
                _packArgs.Logger.Log(PackagingLogMessage.CreateMessage(string.Format(CultureInfo.CurrentCulture, Strings.Log_PackageCommandInstallPackageToOutputPath, "Package", outputPath), LogLevel.Minimal));
                WriteResolvedNuSpecToPackageOutputDirectory(builder);
                WriteSHA512PackageHash(builder);
            }

            // _packArgs.Logger.Log(PackagingLogMessage.CreateMessage(String.Format(CultureInfo.CurrentCulture, Strings.Log_PackageCommandSuccess, outputPath), LogLevel.Minimal));
            return new PackageArchiveReader(outputPath);
        }

        private void PrintVerbose(string outputPath, PackageBuilder builder)
        {
            WriteLine(String.Empty);

            WriteLine("Id: {0}", builder.Id);
            WriteLine("Version: {0}", builder.Version);
            WriteLine("Authors: {0}", String.Join(", ", builder.Authors));
            WriteLine("Description: {0}", builder.Description);
            if (builder.LicenseUrl != null)
            {
                WriteLine("License Url: {0}", builder.LicenseUrl);
            }
            if (builder.ProjectUrl != null)
            {
                WriteLine("Project Url: {0}", builder.ProjectUrl);
            }
            if (builder.Tags.Count > 0)
            {
                WriteLine("Tags: {0}", String.Join(", ", builder.Tags));
            }
            if (builder.DependencyGroups.Count > 0)
            {
                WriteLine("Dependencies: {0}", String.Join(", ", builder.DependencyGroups.SelectMany(d => d.Packages).Select(d => d.ToString())));
            }
            else
            {
                WriteLine("Dependencies: None");
            }

            WriteLine(String.Empty);

            using (var package = new PackageArchiveReader(outputPath))
            {
                foreach (var file in package.GetFiles().OrderBy(p => p))
                {
                    WriteLine(Strings.Log_PackageCommandAddedFile, file);
                }
            }

            WriteLine(String.Empty);
        }

        private void WriteLine(string message, object arg = null)
        {
            _packArgs.Logger.Log(PackagingLogMessage.CreateMessage(String.Format(CultureInfo.CurrentCulture, message, arg?.ToString()), LogLevel.Information));
        }

        /// <summary>
        /// Writes the resolved NuSpec file to the package output directory.
        /// </summary>
        /// <param name="builder">The package builder</param>
        private void WriteResolvedNuSpecToPackageOutputDirectory(PackageBuilder builder)
        {
            var outputPath = GetOutputPath(builder, false, builder.Version);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var resolvedNuSpecOutputPath = Path.Combine(
                Path.GetDirectoryName(outputPath),
                new VersionFolderPathResolver(outputPath).GetManifestFileName(builder.Id, builder.Version));

            _packArgs.Logger.Log(PackagingLogMessage.CreateMessage(string.Format(CultureInfo.CurrentCulture, Strings.Log_PackageCommandInstallPackageToOutputPath, "NuSpec", resolvedNuSpecOutputPath), LogLevel.Minimal));

            if (string.Equals(_packArgs.Path, resolvedNuSpecOutputPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new PackagingException(NuGetLogCode.NU5001, string.Format(CultureInfo.CurrentCulture, Strings.Error_WriteResolvedNuSpecOverwriteOriginal, _packArgs.Path));
            }

            // We must use the Path.GetTempPath() which NuGetFolderPath.Temp uses as a root because writing temp files to the package directory with a guid would break some build tools caching
            var manifest = new Manifest(new ManifestMetadata(builder), null);
            var tempOutputPath = Path.Combine(NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp), Path.GetFileName(resolvedNuSpecOutputPath));
            using (Stream stream = new FileStream(tempOutputPath, FileMode.Create))
            {
                manifest.Save(stream);
            }

            FileUtility.Replace(tempOutputPath, resolvedNuSpecOutputPath);
        }

        /// <summary>
        /// Writes the sha512 package hash file to the package output directory
        /// </summary>
        /// <param name="builder">The package builder</param>
        private void WriteSHA512PackageHash(PackageBuilder builder)
        {
            var outputPath = GetOutputPath(builder, false, builder.Version);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var sha512OutputPath = Path.Combine(outputPath + ".sha512");

            // We must use the Path.GetTempPath() which NuGetFolderPath.Temp uses as a root because writing temp files to the package directory with a guid would break some build tools caching
            var tempOutputPath = Path.Combine(NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp), Path.GetFileName(sha512OutputPath));

            _packArgs.Logger.Log(PackagingLogMessage.CreateMessage(string.Format(CultureInfo.CurrentCulture, Strings.Log_PackageCommandInstallPackageToOutputPath, "SHA512", sha512OutputPath), LogLevel.Minimal));

            byte[] sha512hash;
            var cryptoHashProvider = new CryptoHashProvider("SHA512");
            using (var fileStream = new FileStream(outputPath, FileMode.Open, FileAccess.Read))
            {
                sha512hash = cryptoHashProvider.CalculateHash(fileStream);
            }

            File.WriteAllText(tempOutputPath, Convert.ToBase64String(sha512hash));
            FileUtility.Replace(tempOutputPath, sha512OutputPath);
        }

        // Gets the full path of the resulting nuget package including the file name
        public string GetOutputPath(PackageBuilder builder, bool symbols = false, NuGetVersion nugetVersion = null, string outputDirectory = null, bool isNupkg = true)
        {
            NuGetVersion versionToUse;
            if (nugetVersion != null)
            {
                versionToUse = nugetVersion;
            }
            else
            {
                if (string.IsNullOrEmpty(_packArgs.Version))
                {
                    if (builder.Version == null)
                    {
                        // If the version is null, the user will get an error later saying that a version
                        // is required. Specifying a version here just keeps it from throwing until
                        // it gets to the better error message. It won't actually get used.
                        versionToUse = NuGetVersion.Parse("1.0.0");
                    }
                    else
                    {
                        versionToUse = builder.Version;
                    }
                }
                else
                {
                    versionToUse = NuGetVersion.Parse(_packArgs.Version);
                }
            }

            var outputFile = GetOutputFileName(builder.Id,
                versionToUse,
                isNupkg: isNupkg,
                symbols: symbols,
                symbolPackageFormat: _packArgs.SymbolPackageFormat,
                excludeVersion: _packArgs.OutputFileNamesWithoutVersion);

            var finalOutputDirectory = _packArgs.OutputDirectory ?? _packArgs.CurrentDirectory;
            finalOutputDirectory = outputDirectory ?? finalOutputDirectory;
            return Path.Combine(finalOutputDirectory, outputFile);
        }

        public static string GetOutputFileName(string packageId, NuGetVersion version, bool isNupkg, bool symbols, SymbolPackageFormat symbolPackageFormat, bool excludeVersion = false)
        {
            // Output file is {id}.{version}
            var normalizedVersion = version.ToNormalizedString();
            var outputFile = excludeVersion ? packageId : packageId + "." + normalizedVersion;

            var extension = isNupkg ? NuGetConstants.PackageExtension : NuGetConstants.ManifestExtension;
            var symbolsExtension = isNupkg
                ? (symbolPackageFormat == SymbolPackageFormat.Snupkg ? NuGetConstants.SnupkgExtension : NuGetConstants.SymbolsExtension)
                : NuGetConstants.ManifestSymbolsExtension;

            // If this is a source package then add .symbols.nupkg to the package file name
            if (symbols)
            {
                outputFile += symbolsExtension;
            }
            else
            {
                outputFile += extension;
            }

            return outputFile;
        }

        public IEnumerable<FileLink> GetFileLinks(string packageId)
        {
            var rv = new List<FileLink>();
            var archivePath = GetArchivePath(packageId);
            using (var sourceArchive = LinkPackageArchiveReader.Create(archivePath))
            {
                var path = Path.GetFullPath(Path.Combine(_packArgs.CurrentDirectory, _packArgs.Path));
                var projectWrapper = ProjectWrapper.ProjectCreator(_packArgs, path);

                var supportedFrameworks = sourceArchive.GetSupportedFrameworks();

                var frameworkDir = sourceArchive.GetShortFolderName(projectWrapper.TargetFramework);
                if (frameworkDir != null)
                {
                    var root = Path.GetDirectoryName(archivePath);
                    var dir = Path.Combine(root, "lib", frameworkDir);
                    if (Directory.Exists(dir))
                    {
                        foreach (var file in Directory.GetFiles(dir))
                        {
                            var fileName = Path.GetFileName(file);
                            var targetPath = Path.Combine(projectWrapper.OutputPath, fileName);
                            rv.Add(new FileLink
                            {
                                Target = targetPath,
                                Source = file
                            });
                        }
                    }
                    else
                    {
                        //TODO: log something
                    }
                }
                else
                {
                    // TODO: Log someting
                }
            }
            return rv;
        }

        private string GetArchivePath(string packageId)
        {
            //var directory = GetPackageDirectory(packageId);
            // todo split version if provided
            var packageFolder = packageId;
            var packageRoot = Path.Combine(BasePath, packageFolder);
            if (Directory.Exists(packageRoot))
            {
                var folders = Directory.EnumerateDirectories(packageRoot);
                // TODO: Pick highest version if none is provided
                var directory = folders.First();
                var archives = Directory.GetFiles(directory, "*.nupkg");
                if (archives.Length == 1)
                {
                    return archives[0];
                }
                else
                {
                    //TODO: Log something
                    return null;
                }
            }
            else
            {
                // TODO: Display error
                return null;
            }
        }
    }
}