using System.IO;
using Link.Command;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using NuGet.Link.Command;
using NuGet.Link.Command.Args;

using NUnit.Framework;

namespace Nuget.Link.Tests
{
    [TestClass]
    public class LinkCommandRunnerTests
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
        public void LinkTargetSdkProject()
        {
            // Arrange
            var targetDirectory = Path.Combine(Constants.TEST_SOLUTION_SRC, "Console.PackageReference");
            var dllPath = Path.Combine(targetDirectory, @"bin\Debug\netcoreapp2.1\Package.Sdk.dll");

            var console = new NuGet.CommandLine.Console();
            var linkArgs = new LinkArgs
            {
                PackageId = "Package.Sdk",
                CurrentDirectory = targetDirectory,
                Console = console,
            };
            var runner = new LinkCommandRunner(linkArgs);

            // Act
            runner.LinkTarget();

            // Assert
            FileAssert.Exists(dllPath);
        }

        [TestMethod]
        public void LinkTargetCsprojProject()
        {
            // Arrange
            var targetDirectory = Path.Combine(Constants.TEST_SOLUTION_SRC, "Console.PackageConfig");
            var dllPath = Path.Combine(targetDirectory, @"bin\Debug\Package.Csproj.dll");

            var console = new NuGet.CommandLine.Console();
            var linkArgs = new LinkArgs
            {
                PackageId = "Package.Csproj",
                CurrentDirectory = targetDirectory,
                Console = console,
            };
            var runner = new LinkCommandRunner(linkArgs);

            // Act
            runner.LinkTarget();

            // Assert
            FileAssert.Exists(dllPath);
        }

        [TestMethod]
        public void LinkSourceSdkProject()
        {
            // Arrange
            var dllPath = Path.Combine(LinkCommandRunner.BasePath, @"Package.Sdk\1.0.0\lib\netstandard2.0\Package.Sdk.dll");

            var csprojPath = Path.Combine(Constants.TEST_SOLUTION_SRC, @"Package.Sdk\Package.Sdk.csproj");
            var console = new NuGet.CommandLine.Console();
            var linkArgs = new LinkArgs
            {
                CurrentDirectory = Path.GetDirectoryName(csprojPath),
                Console = console,
            };
            var runner = new LinkCommandRunner(linkArgs);

            // Act
            runner.LinkSource();

            // Assert
            FileAssert.Exists(dllPath);
        }

        [TestMethod]
        public void LinkSourceCsprojProject()
        {
            // Arrange
            var dllPath = Path.Combine(LinkCommandRunner.BasePath, @"Package.Csproj\1.0.0\lib\net472\Package.Csproj.dll");

            var csprojPath = Path.Combine(Constants.TEST_SOLUTION_SRC, @"Package.Csproj\Package.Csproj.csproj");
            var console = new NuGet.CommandLine.Console();
            var packArgs = new LinkArgs
            {
                CurrentDirectory = Path.GetDirectoryName(csprojPath),
                Console = console,
            };
            var runner = new LinkCommandRunner(packArgs);

            // Act
            runner.LinkSource();

            // Assert
            FileAssert.Exists(dllPath);
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
