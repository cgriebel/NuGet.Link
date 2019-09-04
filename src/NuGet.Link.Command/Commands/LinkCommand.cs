
using NuGet;
using NuGet.Link.Command.Args;

namespace Link.Command
{
    [Command("link", "Links a package for use in other projects", 
        UsageSummary = "[<packageId>] [options]")]
    public class LinkCommand : NuGet.CommandLine.Command
    {
        public string PackageId { get; set; }

        public override void ExecuteCommand()
        {
            var linkArgs = new LinkArgs
            {
                Console = Console,
                Verbosity = Verbosity,
                PackageId = Arguments.Count >= 1 ? Arguments[1] : null,
            };
            var linkCommandRunner = new LinkCommandRunner(linkArgs);
            linkCommandRunner.Link();
        }
    }
}
