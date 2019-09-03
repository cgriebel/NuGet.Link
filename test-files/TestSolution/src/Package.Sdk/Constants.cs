namespace Package
{
    public static class Constants
    {
        private static string _packageSourceMessage = "From Sdk Package Source";
        private static string _linkedMessage = "From Sdk Linked Package";
        public static string Message => _linkedMessage;
    }
}
