using NuGet.CommandLine;

namespace NuGet.Link.Command.Args
{
    public class BaseArgs
    {
        public string PackageId { get; set; }
        public IConsole Console { get; set; }
        public Verbosity Verbosity { get; set; }
        public string CurrentDirectory { get; set; }
    }
}
