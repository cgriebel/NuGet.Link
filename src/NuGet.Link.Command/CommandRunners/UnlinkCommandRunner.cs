using System.IO;

using NuGet.Link.Command.Args;

namespace Link.Command
{
    public class UnlinkCommandRunner : BaseCommandRunner
    {
        private readonly UnlinkArgs _unlinkArgs;

        public UnlinkCommandRunner(UnlinkArgs packArgs)
            : base(packArgs)
        {
            _unlinkArgs = packArgs;
        }

        public void Unlink()
        {
            if(_unlinkArgs.PackageId == null)
            {
                UnlinkSource();
            }
            else
            {
                UnlinkTarget();
            }
        }

        private void UnlinkTarget()
        {
            _unlinkArgs.Console.WriteWarning("");
            foreach (var fileLink in GetFileLinks(_unlinkArgs.PackageId))
            {
                if (File.Exists(fileLink.Target))
                {
                    File.Delete(fileLink.Target);
                }
            }
        }

        private void UnlinkSource()
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
