using NuGet.CommandLine;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NuGet.Link.Command
{
    // TODO: better description, add additional properties
    [Command("Link", "Links a package for use in other projects")]
    public class LinkCommand : CommandLine.Command
    {
        internal static readonly string SymbolsExtension = ".symbols" + PackagingCoreConstants.NupkgExtension;

        private readonly HashSet<string> _excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private Version _minClientVersionValue;

        [Option("PackageCommandOutputDirDescription")]
        public string OutputDirectory { get; set; }

        [Option("PackageCommandBasePathDescription")]
        public string BasePath { get; set; }

        [Option("PackageCommandVersionDescription")]
        public string Version { get; set; }

        [Option("PackageCommandSuffixDescription")]
        public string Suffix { get; set; }

        [Option("PackageCommandExcludeDescription")]
        public ICollection<string> Exclude
        {
            get { return _excludes; }
        }

        [Option("PackageCommandSymbolsDescription")]
        public bool Symbols { get; set; }

        [Option("PackageCommandToolDescription")]
        public bool Tool { get; set; }

        [Option("PackageCommandBuildDescription")]
        public bool Build { get; set; }

        [Option("PackageCommandNoDefaultExcludes")]
        public bool NoDefaultExcludes { get; set; }

        [Option("PackageCommandNoRunAnalysis")]
        public bool NoPackageAnalysis { get; set; }

        [Option("PackageCommandExcludeEmptyDirectories")]
        public bool ExcludeEmptyDirectories { get; set; }

        [Option("PackageCommandIncludeReferencedProjects")]
        public bool IncludeReferencedProjects { get; set; }

        [Option("PackageCommandPropertiesDescription")]
        public Dictionary<string, string> Properties
        {
            get
            {
                return _properties;
            }
        }

        [Option("PackageCommandMinClientVersion")]
        public string MinClientVersion { get; set; }

        [Option("PackageCommandSymbolPackageFormat")]
        public string SymbolPackageFormat { get; set; }

        [Option("CommandMSBuildVersion")]
        public string MSBuildVersion { get; set; }

        [Option("CommandMSBuildPath")]
        public string MSBuildPath { get; set; }

        [Option("PackageCommandInstallPackageToOutputPath")]
        public bool InstallPackageToOutputPath { get; set; }

        [Option("PackageCommandOutputFileNamesWithoutVersion")]
        public bool OutputFileNamesWithoutVersion { get; set; }

        [Option("PackageCommandConfigFile")]
        public new string ConfigFile { get; set; }

        public override void ExecuteCommand()
        {
            var packArgs = new PackArgs();
            packArgs.Logger = Console;
            packArgs.Arguments = Arguments;
            packArgs.OutputDirectory = OutputDirectory;
            packArgs.BasePath = BasePath;
            packArgs.MsBuildDirectory = new Lazy<string>(() => MsBuildUtility.GetMsBuildDirectoryFromMsBuildPath(MSBuildPath, MSBuildVersion, Console).Value.Path);

            // Get the input file
            packArgs.Path = PackCommandRunner.GetInputFile(packArgs);

            // Set the current directory if the files being packed are in a different directory
            PackCommandRunner.SetupCurrentDirectory(packArgs);

            // Console.WriteLine(LocalizedResourceManager.GetString("PackageCommandAttemptingToBuildPackage"), Path.GetFileName(packArgs.Path));

            if (!string.IsNullOrEmpty(MinClientVersion))
            {
                if (!System.Version.TryParse(MinClientVersion, out _minClientVersionValue))
                {
                    throw new CommandLineException(""); // LocalizedResourceManager.GetString("PackageCommandInvalidMinClientVersion"));
                }
            }

            if (!string.IsNullOrEmpty(SymbolPackageFormat))
            {
                packArgs.SymbolPackageFormat = PackArgs.GetSymbolPackageFormat(SymbolPackageFormat);
            }

            packArgs.Build = Build;
            packArgs.Exclude = Exclude;
            packArgs.ExcludeEmptyDirectories = ExcludeEmptyDirectories;
            packArgs.IncludeReferencedProjects = IncludeReferencedProjects;
            switch (Verbosity)
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
            packArgs.MinClientVersion = _minClientVersionValue;
            packArgs.NoDefaultExcludes = NoDefaultExcludes;
            packArgs.NoPackageAnalysis = NoPackageAnalysis;
            if (Properties.Any())
            {
                packArgs.Properties.AddRange(Properties);
            }
            packArgs.Suffix = Suffix;
            packArgs.Symbols = Symbols;
            packArgs.Tool = Tool;
            packArgs.InstallPackageToOutputPath = InstallPackageToOutputPath;
            packArgs.OutputFileNamesWithoutVersion = OutputFileNamesWithoutVersion;

            if (!string.IsNullOrEmpty(Version))
            {
                NuGetVersion version;
                if (!NuGetVersion.TryParse(Version, out version))
                {
                    throw new PackagingException(NuGetLogCode.NU5010, string.Format(CultureInfo.CurrentCulture, NuGetResources.InstallCommandPackageReferenceInvalidVersion, Version));
                }
                packArgs.Version = version.ToFullString();
            }

            var linkCommandRunner = new LinkCommandRunner(packArgs, ProjectFactory.ProjectCreator)
            {
                GenerateNugetPackage = false
            };
            linkCommandRunner.BuildPackage();
            System.Console.WriteLine("Here");
            base.ExecuteCommand();
        }
    }
}
