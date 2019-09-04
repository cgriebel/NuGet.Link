using System;
using System.IO;
using System.Runtime.Versioning;

using NuGet.Packaging;

namespace Link.Command
{
    public sealed class LinkPackageArchiveReader : PackageArchiveReader
    {
        private Stream _stream;

        private LinkPackageArchiveReader(Stream stream) : base(stream)
        {
        }

        public static LinkPackageArchiveReader Create(string filePath)
        {
            var fileStream = File.OpenRead(filePath);
            return new LinkPackageArchiveReader(fileStream)
            {
                _stream = fileStream
            };
        }

        public new void Dispose()
        {
            base.Dispose();
            _stream?.Dispose();
        }

        public string GetShortFolderName(FrameworkName frameworkName)
        {
            var supportedFrameworks = GetSupportedFrameworks();
            var providerType = CompatibilityProvider.GetType();

            // IFrameworkCompatibilityProvider and NuGetFramework are
            // internal in the release version of NuGet.exe
            var method = providerType.GetMethod(nameof(CompatibilityProvider.IsCompatible));
            var param1 = method.GetParameters()[1];
            var nugetFrameworkType = param1.ParameterType;
            var targetFramework = Activator.CreateInstance(nugetFrameworkType, frameworkName.Identifier, frameworkName.Version, frameworkName.Profile);
            foreach(var framework in supportedFrameworks)
            {
                if((bool)method.Invoke(CompatibilityProvider, new[] { targetFramework, framework }))
                {
                    return framework.GetShortFolderName();
                }
            }
            return null;
        }
    }
}
