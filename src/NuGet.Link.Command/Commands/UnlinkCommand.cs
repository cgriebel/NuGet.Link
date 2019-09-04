
using NuGet;
using NuGet.Link.Command.Args;

namespace Link.Command
{
    // TODO: better description, add additional properties
    [Command("unlink", "Unlinks a linked package",
        UsageSummary = "[<packageId>] [options]")]
    public class UnlinkCommand : NuGet.CommandLine.Command
    {
        public string PackageId { get; set; }

        public override void ExecuteCommand()
        {
            var unlinkArgs = new UnlinkArgs
            {
                Console = Console,
                Verbosity = Verbosity,
                PackageId = Arguments.Count >= 1 ? Arguments[0] : null
            };
            var unlinkCommandRunner = new UnlinkCommandRunner(unlinkArgs);
            unlinkCommandRunner.Unlink();
        }
    }
}
