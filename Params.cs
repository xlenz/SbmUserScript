namespace userScript
{
    public class Params
    {
        public static string SBMHostPort;
        public static string User;
        public static string Password;
        public static string PathToJson;
        public static string SSOHost = null;
        public static string SSOPort = "8085";
        public static bool UseHttps = false;

        public static bool AreParamsValid()
        {
            bool valid = true;
            if (string.IsNullOrEmpty(SBMHostPort) || string.IsNullOrEmpty(User) || string.IsNullOrEmpty(PathToJson) || Password == null)
            {
                Log.Err("One or more required fields are not valid or were not provided.");
                valid = false;
            }
            if ((SSOHost != null && SSOHost == string.Empty) || string.IsNullOrEmpty(SSOPort))
            {
                Log.Err("Invalid value for SSO host or port.");
                valid = false;
            }

            return valid;
        }
    }
}
