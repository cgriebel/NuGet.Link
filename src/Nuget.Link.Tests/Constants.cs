using System.IO;
using System.Reflection;

namespace Nuget.Link.Tests
{
    public class Constants
    {
        public static string TestSoltuionSrc =>
            Path.Combine(RepositoryRoot, @"test-files\TestSolution\src\");

        public static string TestBasePath =>
            Path.Combine(RepositoryRoot, "TestOutput");

        public static string RepositoryRoot =>
            Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "../../../../"
            );
    }
}
