using System;
using System.Collections.Generic;

using NuGet.Link.Command.Args;
using NuGet.Packaging.Core;

namespace NuGet.Link.Command
{
    // TODO: better description, add additional properties
    [Command("Unlink", "Unlinks a linked package")]
    public class UnlinkCommand : CommandLine.Command
    {
        internal static readonly string SymbolsExtension = ".symbols" + PackagingCoreConstants.NupkgExtension;

        private readonly HashSet<string> _excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private Version _minClientVersionValue;

        #region Options
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
        #endregion

        public override void ExecuteCommand()
        {
            var unlinkArgs = new UnlinkArgs
            {
                Console = Console,
                Verbosity = Verbosity,
            };
            var unlinkCommandRunner = new UnlinkCommandRunner(unlinkArgs);
            unlinkCommandRunner.UnlinkSource();
        }
    }
}
