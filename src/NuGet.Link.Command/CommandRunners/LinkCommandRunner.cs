using System;
using System.IO;

using Murphy.SymbolicLink;

using NuGet.Link.Command.Args;
using NuGet.Packaging;

namespace NuGet.Link.Command
{
    public class LinkCommandRunner : BaseCommandRunner
    {
        public static string BasePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NugetLink");



        public LinkCommandRunner(LinkArgs linkArgs)
            : base(linkArgs)
        {
        }

        public void LinkTarget()
        {
        }

        public void LinkSource()
        {
            var packageBuilder = CreatePackageBuilder();
            var packageRoot = Path.Combine(BasePath, packageBuilder.Id, packageBuilder.Version.ToNormalizedString());
            Directory.CreateDirectory(packageRoot);
            foreach (var file in packageBuilder.Files)
            {
                if (file is PhysicalPackageFile physicalFile)
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
    }
}
