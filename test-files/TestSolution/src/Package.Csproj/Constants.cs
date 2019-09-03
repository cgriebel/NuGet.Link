namespace Package
{
    public class Constants
    {
        private static string _packageSourceMessage = "From Csproj Package Source";
        private static string _linkedMessage = "From Csproj Linked Package";
        public static string Message => _linkedMessage;
    }
}
