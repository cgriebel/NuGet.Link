namespace NuGet.Link.Command
{
    // TODO: better description, add additional properties
    [Command("Link", "Links a package for use in other projects")]
    public class LinkCommand : CommandLine.Command
    {
        public override void ExecuteCommand()
        {
            System.Console.WriteLine("Here");
            base.ExecuteCommand();
        }
    }
}
