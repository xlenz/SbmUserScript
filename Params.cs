﻿namespace userScript
{
    public class Params
    {
        public static string SBMHostPort;
        public static string User;
        public static string Password;
        public static string PathToJson;
        public static string OEHost = null;
        public static string OEPort = "8085";
        public static bool UseHttps = false;

        public static bool AreParamsValid()
        {
            bool valid = true;
            if (string.IsNullOrEmpty(SBMHostPort) || string.IsNullOrEmpty(User) || string.IsNullOrEmpty(PathToJson) || Password == null)
            {
                Log.Err("One or more required fields are not valid or were not provided.");
                valid = false;
            }
            if ((OEHost != null && OEHost == string.Empty) || string.IsNullOrEmpty(OEPort))
            {
                Log.Err("Invalid value for SSO host or port.");
                valid = false;
            }
            if (OEHost == null)
            {
                OEHost = SBMHostPort.Split(':')[0];
            }

            return valid;
        }
    }
}
