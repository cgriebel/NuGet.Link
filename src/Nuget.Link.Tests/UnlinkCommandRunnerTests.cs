using System.IO;

using Link.Command;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using NuGet.Link.Command.Args;

using NUnit.Framework;

namespace Nuget.Link.Tests
{
    [TestClass]
    public class UnlinkCommandRunnerTests
    {
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            LinkCommandRunner.BasePath = Constants.TEST_OUTPUT_DIR;
        }

        [SetUp]
        public void Setup()
        {
            CleanUp();
            Directory.CreateDirectory(Constants.TEST_OUTPUT_DIR);
        }

        [TearDown]
        public void TearDown()
        {
            CleanUp();
        }

        private void CleanUp()
        {
            if (Directory.Exists(Constants.TEST_OUTPUT_DIR))
            {
                Directory.Delete(Constants.TEST_OUTPUT_DIR, true);
            }
        }

        [TestMethod]
        public void UnlinkSourceSdkProject()
        {
            // Arrange
            var dllPath = Path.Combine(Constants.TEST_OUTPUT_DIR, @"Package.Sdk\1.0.0\lib\netstandard2.0\Package.Sdk.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(dllPath));
            File.WriteAllText(dllPath, "");
            var csprojPath = Path.Combine(Constants.TEST_SOLUTION_SRC, @"Package.Sdk\Package.Sdk.csproj");
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
            DirectoryAssert.DoesNotExist(Path.Combine(Constants.TEST_OUTPUT_DIR, LinkCommandRunner.BasePath, @"Package.Sdk\1.0.0"));
        }

        [TestMethod]
        public void UnlinkSourceCsprojProject()
        {
            // Arrange
            var dllPath = Path.Combine(LinkCommandRunner.BasePath, @"Package.Csproj\1.0.0\lib\net472\Package.Csproj.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(dllPath));
            File.WriteAllText(dllPath, "");

            var csprojPath = Path.Combine(Constants.TEST_SOLUTION_SRC, @"Package.Csproj\Package.Csproj.csproj");
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
            DirectoryAssert.DoesNotExist(Path.Combine(Constants.TEST_OUTPUT_DIR, LinkCommandRunner.BasePath, @"Package.Csproj\1.0.0"));
        }

        //[TestMethod]
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
