﻿using System.IO;

using NuGet.Link.Command;
using NuGet.Link.Command.Args;
using NuGet.Packaging;

namespace Link.Command
{
    public class LinkCommandRunner : BaseCommandRunner
    {
        private readonly LinkArgs _linkArgs;

        public LinkCommandRunner(LinkArgs linkArgs)
            : base(linkArgs)
        {
            _linkArgs = linkArgs;
        }

        public void Link()
        {
            if(_linkArgs.PackageId == null)
            {
                LinkSource();
            }
            else
            {
                LinkTarget();
            }
        }

        private void LinkTarget()
        {
            foreach(var fileLink in GetFileLinks(_linkArgs.PackageId))
            {
                if (File.Exists(fileLink.Target))
                {
                    File.Delete(fileLink.Target);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(fileLink.Target));
                SymbolicLink.Create(fileLink.Source, fileLink.Target);
            }
        }

        private void LinkSource()
        {
            var packageBuilder = CreatePackageBuilder();
            var packageRoot = Path.Combine(BasePath, packageBuilder.Id, packageBuilder.Version.ToNormalizedString());
            Directory.CreateDirectory(packageRoot);

            var packageOutputPath = GetOutputPath(packageBuilder, false, packageBuilder.Version, packageRoot);
            var archive = BuildPackage(packageBuilder, packageOutputPath);
            archive?.Dispose();

            foreach (var file in packageBuilder.Files)
            {
                if (file is PhysicalPackageFile physicalFile)
                {
                    var target = Path.Combine(packageRoot, file.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));

                    if (File.Exists(target))
                    {
                        File.Delete(target);
                    }

                    SymbolicLink.Create(physicalFile.SourcePath, target);
                }
            }
        }
    }
}
