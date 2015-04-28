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
            Log.Ok("JSON: Done.");

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
            Log.Ok("SSO: Done.");
            _jsonClient = new JsonClient(ssoBase64);
        }

        public bool IsInit()
        {
            return _isInit;
        }

        public void CreateUpdateUsers()
        {
            var createUpdateUsers = _inputJson["createUpdateUsers"];
            if (createUpdateUsers == null)
            {
                Log.Info("No users will be created or updated.");
                return;
            }
            foreach (var user in createUpdateUsers)
            {
                var login = Tools.GetJsonValue(user, "login");
                _jsonClient.SetUser(login, Tools.GetJsonValueNullable(user, "name") ?? login, Tools.GetJsonValue(user, "password"),
                    Tools.GetJsonValueNullable(user, "email") ?? string.Empty, Tools.GetJsonValueNullable(user, "emailCc") ?? string.Empty);
            }
        }

        public void CreateGroups()
        {
            var createGroups = _inputJson["createGroups"];
            if (createGroups == null)
            {
                Log.Info("No groups will be created.");
                return;
            }
            foreach (var group in createGroups)
            {
                _jsonClient.SetGroup(Tools.GetJsonValue(group, "name"));
            }
        }

        public void AddUsersToGroups()
        {
            var addUsersToGroups = _inputJson["addUsersToGroups"];
            if (addUsersToGroups == null)
            {
                Log.Info("No users will be added to groups.");
                return;
            }
            foreach (var groupMembership in addUsersToGroups)
            {
                if (groupMembership["groups"] == null || groupMembership["userLogins"] == null)
                {
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
            var setUserRoles = _inputJson["setUserRoles"];
            if (setUserRoles == null)
            {
                Log.Info("No roles will be set for users.");
                return;
            }

            foreach (var setUserRole in setUserRoles)
            {
                var login = Tools.GetJsonValue(setUserRole, "login");
                var roles = setUserRole["roles"];
                if (!roles.Children().Any())
                {
                    Log.Warn("SetUserRoles: No roles were provided for login: " + login);
                    continue;
                }

                foreach (var role in roles)
                {
                    if (role["projects"] == null)
                    {
                        throw new Exception("SetUserRoles: 'projects' is a required field.");
                    }
                    var projects = role["projects"].Values<string>().ToArray();
                    var roleList = role["roleList"];
                    if (projects.Length > 0 && roleList != null && role.Children().Any())
                    {
                        var rolesDict = ParseRoles(roleList);
                        if (rolesDict.Count == 0)
                        {
                            Log.Warn("SetUserRoles: No roles will be set for login '" + login + "'. See previous warnings.");
                        }
                        else
                        {
                            _jsonClient.SetUserRoles(login, projects, rolesDict);
                        }
                    }
                    else
                    {
                        Log.Warn("SetUserRoles: Either no projects or users were provided for login: " + login);
                    }
                }
            }
        }

        public void SetGroupRoles()
        {
            var setGroupRoles = _inputJson["setGroupRoles"];
            if (setGroupRoles == null || !setGroupRoles.Children().Any())
            {
                Log.Info("No roles will be set for groups.");
                return;
            }

            foreach (var setGroupRole in setGroupRoles)
            {
                var group = Tools.GetJsonValue(setGroupRole, "group");
                var roles = setGroupRole["roles"];
                if (!roles.Children().Any())
                {
                    Log.Warn("SetGroupRoles: No roles were provided for group: " + group);
                    continue;
                }

                foreach (var role in roles)
                {
                    if (role["projects"] == null)
                    {
                        throw new Exception("SetUserRoles: 'projects' is a required field.");
                    }
                    var projects = role["projects"].Values<string>().ToArray();
                    var roleList = role["roleList"];
                    if (projects.Length > 0 && roleList != null && role.Children().Any())
                    {
                        var rolesDict = ParseRoles(roleList);
                        if (rolesDict.Count == 0)
                        {
                            Log.Warn("SetGroupRoles: No roles will be set for group '" + group + "'. See previous warnings.");
                        }
                        else
                        {
                            _jsonClient.SetGroupRoles(group, projects, rolesDict);
                        }
                    }
                    else
                    {
                        Log.Warn("SetGroupRoles: Either no projects or users were provided for group: " + group);
                    }
                }
            }
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