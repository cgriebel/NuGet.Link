using System.IO;

using Link.Command;

using NuGet.Link.Command.Args;

using NUnit.Framework;

namespace Nuget.Link.Tests
{
    [TestFixture]
    public class LinkCommandRunnerTests
    {
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            BaseCommandRunner.BasePath = Constants.TestBasePath;
        }

        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
            {
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            }

            foreach (FileInfo file in source.GetFiles())
            {
                file.CopyTo(Path.Combine(target.FullName, file.Name));
            }
        }

        [SetUp]
        public void Setup()
        {
            CleanUp();
            var targetDirectory = Directory.CreateDirectory(Constants.TestBasePath);
            var copyFromPath = Path.Combine(Constants.RepositoryRoot, "test-files/linked-files");
            var copyFromDirectory = new DirectoryInfo(copyFromPath);
            CopyFilesRecursively(copyFromDirectory, targetDirectory);
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
        public void LinkTargetSdkProject()
        {
            // Arrange
            var targetDirectory = Path.Combine(Constants.TestSoltuionSrc, "Console.PackageReference");
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
            runner.Link();

            // Assert
            FileAssert.Exists(dllPath);
        }

        [Test]
        public void LinkTargetCsprojProject()
        {
            // Arrange
            var targetDirectory = Path.Combine(Constants.TestSoltuionSrc, "Console.PackageConfig");
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
            runner.Link();

            // Assert
            FileAssert.Exists(dllPath);
        }

        [Test]
        public void LinkSourceSdkProject()
        {
            // Arrange
            var dllPath = Path.Combine(LinkCommandRunner.BasePath, @"Package.Sdk\1.0.0\lib\netstandard2.0\Package.Sdk.dll");

            var csprojPath = Path.Combine(Constants.TestSoltuionSrc, @"Package.Sdk\Package.Sdk.csproj");
            var console = new NuGet.CommandLine.Console();
            var linkArgs = new LinkArgs
            {
                CurrentDirectory = Path.GetDirectoryName(csprojPath),
                Console = console,
            };
            var runner = new LinkCommandRunner(linkArgs);

            // Act
            runner.Link();

            // Assert
            FileAssert.Exists(dllPath);
        }

        [Test]
        public void LinkSourceCsprojProject()
        {
            // Arrange
            var dllPath = Path.Combine(LinkCommandRunner.BasePath, @"Package.Csproj\1.0.0\lib\net472\Package.Csproj.dll");

            var csprojPath = Path.Combine(Constants.TestSoltuionSrc, @"Package.Csproj\Package.Csproj.csproj");
            var console = new NuGet.CommandLine.Console();
            var packArgs = new LinkArgs
            {
                CurrentDirectory = Path.GetDirectoryName(csprojPath),
                Console = console,
            };
            var runner = new LinkCommandRunner(packArgs);

            // Act
            runner.Link();

            // Assert
            FileAssert.Exists(dllPath);
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
