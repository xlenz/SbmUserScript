using System;
using System.Collections.Generic;
using System.Linq;

namespace userScript
{
    internal class GetParams
    {
        private static readonly Dictionary<string, string> Parameters = new Dictionary<string, string>();
        private static bool _argsGood = true;

        public static void ReadParams(string[] args)
        {
            var paramList = "sbmHostPort,u,p,ssoHost,ssoPort,useHttps,json".Split(',').ToList();
            const string prefix1 = "/";
            const string prefix2 = "-";

            var paramCount = args.Length;
            if (paramCount <= 7 || (paramCount%2 != 0))
            {
                if (paramCount > 1)
                {
                    _argsGood = false;
                    Log.Err("One or more required fields were not provided or some param has no value provided.");
                }
                else
                {
                    ShowHelp();
                }
                return;
            }

            for (int i = 0; i < paramCount; i++)
            {
                for (int j = 0; j < paramList.Count; j++)
                {
                    var paramName = paramList[j].ToLower();
                    if (args[i].ToLower() == prefix1 + paramName || args[i] == prefix2 + paramName)
                    {
                        Parameters.Add(paramList[j], args[i + 1]);
                        paramList.RemoveAt(j);
                        i++;
                        j = paramList.Count + 1;
                    }
                    else if (j == paramList.Count)
                    {
                        Log.Err("Not valid params provided. See help for details.");
                        _argsGood = false;
                        break;
                    }
                }
            }

            Params.SBMHostPort = Tools.TryGetDictValue(Parameters, "sbmHostPort");
            Params.User = Tools.TryGetDictValue(Parameters, "u");
            Params.Password = Tools.TryGetDictValue(Parameters, "p");
            Params.PathToJson = Tools.TryGetDictValue(Parameters, "json");
            Params.SSOHost = Tools.TryGetDictValue(Parameters, "ssoHost");

            var ssoPort = Tools.TryGetDictValue(Parameters, "ssoPort");
            if (ssoPort != null)
            {
                Params.SSOPort = ssoPort;
            }
            var useHttps = Tools.TryGetDictValue(Parameters, "useHttps");
            if (useHttps != null && useHttps.ToLower() == "true")
            {
                Params.UseHttps = true;
            }
        }

        private static void ShowHelp()
        {
            _argsGood = false;
            Parameters.Clear();
            Console.WriteLine("Usage:\n" +
                              "-sbmHostPort | /sbmHostPort REQUIRED: SBM host:port\n" +
                              "-u           | /u           REQUIRED: Username\n" +
                              "-p           | /p           REQUIRED: Password\n" +
                              "-json        | /json        REQUIRED: Path to json file\n" +
                              "-ssoHost     | /ssoHost     OPTIONAL: SSO host. Default same as SBM host.\n" +
                              "-ssoPort     | /ssoPort     OPTIONAL: SSO port. Default 8085\n" +
                              "-useHttps    | /useHttps    OPTIONAL: true | false. Default false\n" +
                              "\n\n" +
                              "Use -generateSampleJsonFile | /generateSampleJsonFile to create sampleJsonFile.json example file." +
                              "\n\n" +
                              "Example:\n/sbmHostPort stl-qa-oalmt1 /u admin /p \"\" -json rlc52users.json" +
                              "\n\nand some more complicated:\n" +
                              "-sbmHostPort orl-qa-vstst94.qa.ldaptest.net:443 -u LDAP_User1 -p !Mtdnp1111 -ssoHost orl-qa-vstst97.qa.ldaptest.net -ssoPort 8243 -useHttps true -json rlc52users.json" +
                              "\n\nSupported SBM versions from 10.1.3.1 to 10.1.5.2" +
                              "\nmgrybyk@serena.com"
                );
        }

        public static bool AreArgsGood()
        {
            return _argsGood;
        }
    }
}