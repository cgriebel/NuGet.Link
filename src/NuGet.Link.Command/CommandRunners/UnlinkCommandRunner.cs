using System;
using System.IO;

using NuGet.Link.Command.Args;

namespace NuGet.Link.Command
{
    public class UnlinkCommandRunner : BaseCommandRunner
    {
        public static string BasePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NugetLink");



        public UnlinkCommandRunner(UnlinkArgs packArgs)
            : base(packArgs)
        {
        }

        public void UnlinkTarget()
        {
        }

        public void UnlinkSource()
        {
            var packageBuilder = CreatePackageBuilder();
            var packageRoot = Path.Combine(BasePath, packageBuilder.Id, packageBuilder.Version.ToNormalizedString());
            if (Directory.Exists(packageRoot))
            {
                Directory.Delete(packageRoot, true);
            }
        }
    }
}
