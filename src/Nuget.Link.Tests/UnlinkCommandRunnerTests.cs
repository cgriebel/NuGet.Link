using System.IO;

using Link.Command;

using NuGet.Link.Command.Args;

using NUnit.Framework;

namespace Nuget.Link.Tests
{
    [TestFixture]
    public class UnlinkCommandRunnerTests
    {
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            BaseCommandRunner.BasePath = Constants.TestBasePath;
        }

        [SetUp]
        public void Setup()
        {
            CleanUp();
            Directory.CreateDirectory(Constants.TestBasePath);
        }

        [TearDown]
        public void TearDown()
        {
            CleanUp();
        }

        private void CleanUp()
        {
            if (Directory.Exists(Constants.TestBasePath))
            {
                Directory.Delete(Constants.TestBasePath, true);
            }
        }

        [Test]
        public void UnlinkSourceSdkProject()
        {
            // Arrange
            var dllPath = Path.Combine(Constants.TestBasePath, @"Package.Sdk\1.0.0\lib\netstandard2.0\Package.Sdk.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(dllPath));
            File.WriteAllText(dllPath, "");
            var csprojPath = Path.Combine(Constants.TestSoltuionSrc, @"Package.Sdk\Package.Sdk.csproj");
            var console = new NuGet.CommandLine.Console();
            var linkArgs = new UnlinkArgs
            {
                CurrentDirectory = Path.GetDirectoryName(csprojPath),
                Console = console,
            };
            var runner = new UnlinkCommandRunner(linkArgs);

            // Act
            runner.Unlink();

            // Assert
            DirectoryAssert.DoesNotExist(Path.Combine(Constants.TestBasePath, LinkCommandRunner.BasePath, @"Package.Sdk\1.0.0"));
        }

        [Test]
        public void UnlinkSourceCsprojProject()
        {
            // Arrange
            var dllPath = Path.Combine(LinkCommandRunner.BasePath, @"Package.Csproj\1.0.0\lib\net472\Package.Csproj.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(dllPath));
            File.WriteAllText(dllPath, "");

            var csprojPath = Path.Combine(Constants.TestSoltuionSrc, @"Package.Csproj\Package.Csproj.csproj");
            var console = new NuGet.CommandLine.Console();
            var packArgs = new UnlinkArgs
            {
                CurrentDirectory = Path.GetDirectoryName(csprojPath),
                Console = console,
            };
            var runner = new UnlinkCommandRunner(packArgs);

            // Act
            runner.Unlink();

            // Assert
            DirectoryAssert.DoesNotExist(Path.Combine(Constants.TestBasePath, LinkCommandRunner.BasePath, @"Package.Csproj\1.0.0"));
        }

        //[Test]
        //public void LinkCsprojNuSpec()
        //{
        //    // Arrange
        //    var dllPath = Path.Combine(LinkCommandRunner.BasePath, @"Package.Csproj\1.0.0\lib\net472\Package.Csproj.dll");

        //    var nuspecPath = Path.Combine(TEST_SOLUTION_SRC, @"Package.Csproj\Package.Csproj.nuspec");
        //    var console = new NuGet.CommandLine.Console();
        //    var packArgs = new PackArgs
        //    {
        //        CurrentDirectory = Path.GetDirectoryName(nuspecPath),
        //        Path = nuspecPath,
        //        MsBuildDirectory = new Lazy<string>(() => MsBuildUtility.GetMsBuildDirectoryFromMsBuildPath(null, null, console).Value.Path),
        //        Logger = console,
        //        Exclude = new string[0],
        //    };
        //    var runner = new LinkCommandRunner(packArgs);

        //    // Act
        //    runner.LinkPackage();

        //    // Assert
        //    FileAssert.Exists(dllPath);
        //}
    }
}
