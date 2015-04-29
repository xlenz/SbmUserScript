using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace userScript.WebAdmin
{
    public class WebAdmin
    {
        private readonly JObject _inputJson;
        private readonly bool _isInit = true;
        private readonly JsonClient _jsonClient;

        public WebAdmin()
        {
            Log.Info("Reading json file...");
            if (!File.Exists(Params.PathToJson))
            {
                Log.Err("File not found: " + Params.PathToJson);
                _isInit = false;
                return;
            }
            //read file
            string jsonString;
            try
            {
                jsonString = File.ReadAllText(Params.PathToJson);
            }
            catch (Exception e)
            {
                Log.Err(e.Message);
                _isInit = false;
                return;
            }
            //convert to json object
            try
            {
                _inputJson = JObject.Parse(jsonString);
            }
            catch (Exception e)
            {
                Log.Err(e.Message);
                _isInit = false;
                return;
            }
            Log.Ok("Json found and parsed.");

            //auth
            Log.Info("Getting SSO Token...");
            string ssoBase64;
            try
            {
                ssoBase64 = new SSO(Params.SBMHostPort, Params.User, Params.Password,
                    ssoPort: Params.SSOPort,
                    ssoHostname: Params.SSOHost,
                    useHttps: Params.UseHttps
                    ).Get();
            }
            catch (Exception e)
            {
                Log.Err(e.Message);
                _isInit = false;
                return;
            }
            Log.Ok("SSO Token obtained.");
            _jsonClient = new JsonClient(ssoBase64);
        }

        public bool IsInit()
        {
            return _isInit;
        }

        public void CreateUpdateUsers()
        {
            var createUpdateUsers = GetInputProperty("createUpdateUsers", "No users will be created or updated.");
            if (createUpdateUsers == null) return;

            foreach (var user in createUpdateUsers)
            {
                var login = Tools.GetJsonValue(user, "login");
                _jsonClient.SetUser(login, Tools.GetJsonValueNullable(user, "name") ?? login, Tools.GetJsonValue(user, "password"),
                    Tools.GetJsonValueNullable(user, "email") ?? string.Empty, Tools.GetJsonValueNullable(user, "emailCc") ?? string.Empty);
            }
        }

        public void CreateGroups()
        {
            var createGroups = GetInputProperty("createGroups", "No groups will be created.");
            if (createGroups == null) return;

            foreach (var group in createGroups)
            {
                _jsonClient.SetGroup(Tools.GetJsonValue(group, "name"));
            }
        }

        public void AddUsersToGroups()
        {
            var addUsersToGroups = GetInputProperty("addUsersToGroups", "No users will be added to groups.");
            if (addUsersToGroups == null) return;

            foreach (var groupMembership in addUsersToGroups)
            {
                if (groupMembership["groups"] == null || groupMembership["userLogins"] == null)
                {
                    Log.Err(groupMembership);
                    throw new Exception("AddUsersToGroups: Both 'groups' and 'userLogins' properties are required.");
                }
                var groups = groupMembership["groups"].Values<string>().ToArray();
                var userLogins = groupMembership["userLogins"].Values<string>().ToArray();

                if (groups.Length > 0 && userLogins.Length > 0)
                {
                    foreach (var user in userLogins)
                    {
                        _jsonClient.SetMembershipForUser(user, groups);
                    }
                }
                else
                {
                    Log.Warn("AddUsersToGroups: Either no groups or users were provided for one of objects.");
                }
            }
        }

        public void SetUserRoles()
        {
            SetRoles("users", "setUserRoles", "login");
        }

        public void SetGroupRoles()
        {
            SetRoles("groups", "setGroupRoles", "group");
        }

        private void SetRoles(string itemType, string inputJsonKey, string key)
        {
            var setRoles = GetInputProperty(inputJsonKey, "No roles will be set for " + itemType);
            if (setRoles == null) return;

            foreach (var setRole in setRoles)
            {
                var itemName = Tools.GetJsonValue(setRole, key);
                var roles = setRole["roles"];
                if (!roles.Children().Any())
                {
                    Log.Warn(string.Format("SetRoles: No roles were provided for {0}: {1}", key, itemName));
                    continue;
                }

                foreach (var role in roles)
                {
                    if (role["projects"] == null)
                    {
                        Log.Err(role);
                        throw new Exception("SetRoles: 'projects' is a required field.");
                    }
                    var projects = role["projects"].Values<string>().ToArray();
                    var roleList = role["roleList"];
                    if (projects.Length > 0 && roleList != null && role.Children().Any())
                    {
                        var rolesDict = ParseRoles(roleList);
                        if (rolesDict.Count == 0)
                        {
                            Log.Warn(string.Format("SetRoles: No roles will be set for {0} '{1}'. See previous warnings.", key, itemName));
                        }
                        else
                        {
                            switch (itemType)
                            {
                                case "groups":
                                    _jsonClient.SetGroupRoles(itemName, projects, rolesDict);
                                    break;
                                case "users":
                                    _jsonClient.SetUserRoles(itemName, projects, rolesDict);
                                    break;
                                default:
                                    throw new Exception("Invalid item type provided: " + itemType);
                            }
                        }
                    }
                    else
                    {
                        Log.Warn(string.Format("SetRoles: Either no projects or users were provided for {0}: {1}", key, itemName));
                    }
                }
            }
        }

        private IEnumerable<JToken> GetInputProperty(string key, string notFoundMessage)
        {
            var jToken = _inputJson[key];
            if (jToken == null || !jToken.Children().Any())
            {
                Log.Info(notFoundMessage);
                return null;
            }
            return jToken;
        }

        private static Dictionary<string, int> ParseRoles(IEnumerable<JToken> roleList)
        {
            var roles = new Dictionary<string, int>();

            foreach (var jToken in roleList)
            {
                var role = (JProperty) jToken;
                try
                {
                    int intValue = Convert.ToBoolean(role.Value) ? 1 : -1;
                    roles.Add(role.Name, intValue);
                }
                catch (Exception)
                {
                    Log.Warn("Unable to set role '" + role.Name + "' because it's property is not true or false.");
                }
            }
            return roles;
        }
    }
}