using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace userScript.WebAdmin
{
    public class JsonClient
    {
        private readonly string _protocol;
        private readonly string _ssoBase64;
        private bool _getAlldone;
        private readonly Dictionary<string, object> _objectExcahnge;

        public JsonClient(string ssoBase64)
        {
            _objectExcahnge = new Dictionary<string, object>();
            _protocol = "http";
            _ssoBase64 = ssoBase64;
            var useHttps = Params.UseHttps;
            if (useHttps)
                _protocol += "s";
        }


        public void SetGroupRoles(string inputGroup, string[] inputProjects, Dictionary<string, int> inputRoles)
        {
            SetRoles(inputGroup, inputProjects, inputRoles, 2);
        }

        public void SetUserRoles(string inputUser, string[] inputProjects, Dictionary<string, int> inputRoles)
        {
            SetRoles(inputUser, inputProjects, inputRoles, 1);
        }

        public void SetUser(string inputUserLogin, string inputUserName, string inputUserPwd, string email, string emailCc)
        {
            GetAll();

            int action;
            string data;
            var dateFormatted = (String.Format("{0:MM/dd/yyyy}", DateTime.Now)).Replace('.', '/');

            if (((Dictionary<string, int>) _objectExcahnge["SBM.Users"]).ContainsKey(inputUserLogin))
            {
                action = 2; //for at least update
                Log.Info("Modifying user: " + inputUserLogin);
                data =
                    JsonConvert.SerializeObject(new SBMSetUser(inputUserLogin, inputUserPwd, dateFormatted, inputUserName, email, emailCc,
                        ((Dictionary<string, int>) _objectExcahnge["SBM.Users"])[inputUserLogin]));
            }
            else
            {
                action = 1; //for creation
                Log.Info("Creating user: " + inputUserLogin);
                data = JsonConvert.SerializeObject(new SBMSetUser(inputUserLogin, inputUserPwd, dateFormatted, inputUserName, email, emailCc));

                _objectExcahnge.Remove("SBM.Users");
            }

            var jsonUrl =
                string.Format(
                    "{0}://{1}/tmtrack/tmtrack.dll?JSONPage&command=updateusergeneral&action={2}&timestamp=-1&passWasChanged=1&checkForConcurrency=0", _protocol,
                    Params.SBMHostPort, action);
            WebReq.Make(jsonUrl, ssoBase64: _ssoBase64, data: data);

            if (action == 1) GetUsers();
        }

        public void SetGroup(string grpName)
        {
            GetAll();

            if (((Dictionary<string, int>) _objectExcahnge["SBM.Groups"]).ContainsKey(grpName))
            {
                Log.Warn("Group: '" + grpName + "' already exists.");
            }
            else
            {
                Log.Info("Creating group: " + grpName);

                var jsonStruct = new SBMSetGroup[1];
                jsonStruct[0] = new SBMSetGroup(grpName);

                var data = JsonConvert.SerializeObject(jsonStruct);
                var jsonUrl = string.Format("{0}://{1}/tmtrack/tmtrack.dll?JSONPage&command=updategroupgeneral&action=1&count=1&checkForConcurrency=0",
                    _protocol, Params.SBMHostPort);

                WebReq.Make(jsonUrl, ssoBase64: _ssoBase64, data: data);

                _objectExcahnge.Remove("SBM.Groups");
                GetGroups();
            }
        }

        public void SetMembershipForUser(string inputUserLogin, string[] inputGroups)
        {
            GetAll();
            int userId;
            if (((Dictionary<string, int>) _objectExcahnge["SBM.Users"]).TryGetValue(inputUserLogin, out userId))
            {
                var jsonUrl = string.Format(
                    "{0}://{1}/tmtrack/tmtrack.dll?JSONPage&command=updatememberships&timestamp=-1&checkForConcurrency=0&subjectType=1", _protocol, Params.SBMHostPort);
                var groupIDs = new List<int>();
                foreach (var groupName in inputGroups)
                {
                    int tmpGroupId;
                    if (((Dictionary<string, int>) _objectExcahnge["SBM.Groups"]).TryGetValue(groupName, out tmpGroupId))
                    {
                        groupIDs.Add(tmpGroupId);
                        Log.Info(string.Format("Setting '" + groupName + "' membership for " + inputUserLogin));
                    }
                }
                int count = groupIDs.Count;
                if (count > 0)
                {
                    var jsonStruct = new SetMembership[count];
                    for (int i = 0; i < count; i++)
                        jsonStruct[i] = new SetMembership(groupIDs[i], userId);

                    var data = JsonConvert.SerializeObject(jsonStruct);
                    WebReq.Make(jsonUrl, ssoBase64: _ssoBase64, data: data);
                }
                else Log.Warn(string.Format("No group membership to be set for '" + inputUserLogin + "'"));
            }
            else Log.Warn(string.Format("User '" + inputUserLogin + "' doesn't exist."));
        }

        private void SetRoles(string inputGroupOrUser, IEnumerable<string> inputProjects, Dictionary<string, int> inputRoles, byte userOrGroup)
        {
            GetAll();

            int userOrGroupId;
            if (userOrGroup == 2 && ((Dictionary<string, int>) _objectExcahnge["SBM.Groups"]).TryGetValue(inputGroupOrUser, out userOrGroupId)) {}
            else if (userOrGroup == 1 && ((Dictionary<string, int>) _objectExcahnge["SBM.Users"]).TryGetValue(inputGroupOrUser, out userOrGroupId)) {}
            else
            {
                Log.Warn(string.Format("Skipping user or group '" + inputGroupOrUser + "' that was not found."));
                return;
            }
            foreach (var proj in inputProjects)
            {
                if (proj == "*")
                    foreach (var project in ((Dictionary<string, SBMRolesPerProject>) _objectExcahnge["SBM.RolesPerProject"]).Values)
                        SetRolesHelper(inputRoles, userOrGroup, userOrGroupId, project, String.Empty);
                else
                {
                    SBMRolesPerProject project;
                    if (((Dictionary<string, SBMRolesPerProject>) _objectExcahnge["SBM.RolesPerProject"]).TryGetValue(proj, out project))
                        SetRolesHelper(inputRoles, userOrGroup, userOrGroupId, project, proj);
                    else Log.Warn(string.Format("Skipping project '" + proj + "' that was not found."));
                }
            }
        }

        private void SetRolesHelper(Dictionary<string, int> inputRoles, byte userOrGroup, int userOrGroupId, SBMRolesPerProject project, string proj)
        {
            var jsonUrl = string.Format("{0}://{1}/tmtrack/tmtrack.dll?JSONPage&command=updatesecuritycontrols&timestamp=-1", _protocol, Params.SBMHostPort);

            //get roles ids and grants to lists
            var permissionIds = new List<int>();
            var grantList = new List<int>();
            //set all roles for group/user
            int firstRoleGranted;
            if (inputRoles.Count == 1 && inputRoles.TryGetValue("*", out firstRoleGranted))
                foreach (var role in project.Roles)
                    UpdatePermissionsGrantLists(role.Value, firstRoleGranted, role.Key, proj, ref permissionIds, ref grantList);
            else
                foreach (var role in inputRoles)
                {
                    int tmpRole;
                    if (project.Roles.TryGetValue(role.Key, out tmpRole))
                        UpdatePermissionsGrantLists(tmpRole, role.Value, role.Key, proj, ref permissionIds, ref grantList);
                }
            //prepare json struct
            int count = permissionIds.Count;
            if (count > 0)
            {
                var jsonStruct = new SBMSetRole[count];
                for (int i = 0; i < count; i++)
                    jsonStruct[i] = new SBMSetRole(project.id, userOrGroupId, userOrGroup) {permissionId = permissionIds[i], granted = grantList[i]};

                var data = JsonConvert.SerializeObject(jsonStruct);
                WebReq.Make(jsonUrl, ssoBase64: _ssoBase64, data: data);
            }
            else Log.Warn(string.Format("No roles found for '" + proj + "'"));
        }

        private void UpdatePermissionsGrantLists(int permissionId, int granted, string roleName, string projectName, ref List<int> permissionIds,
            ref List<int> grantList)
        {
            permissionIds.Add(permissionId);
            grantList.Add(granted);
            if (projectName != String.Empty)
                Log.Info(string.Format("Role '" + roleName + "' will be set for " + projectName));
        }

        private void GetAll()
        {
            if (_getAlldone) return;
            if (!_objectExcahnge.ContainsKey("SBM.Groups")) GetGroups();
            if (!_objectExcahnge.ContainsKey("SBM.Users")) GetUsers();
            if (!_objectExcahnge.ContainsKey("SBM.RolesPerProject"))
            {
                GetProjects(); //should be before GetRoles
                GetRoles();
            }
            _getAlldone = true;
        }

        private void GetGroups()
        {
            var jsonUrl =
                string.Format(
                    "{0}://{1}/tmtrack/tmtrack.dll?JSONPage&command=getgroups&showDeleted=0&onlyManaged=1&startIndex=0&fetchSize=99999&sortBy=name&sortOrder=ascend&searchString=",
                    _protocol, Params.SBMHostPort);

            var response = WebReq.Make(jsonUrl, ssoBase64: _ssoBase64, contentLength: 0);

            JObject jObject = JObject.Parse(response);
            JToken groupList = jObject["groupList"];

            _objectExcahnge.Add("SBM.Groups", new Dictionary<string, int>());
            foreach (var group in groupList)
                ((Dictionary<string, int>) _objectExcahnge["SBM.Groups"]).Add(group["name"].Value<string>(), group["id"].Value<int>());
        }

        private void GetUsers()
        {
            var jsonUrl =
                string.Format(
                    "{0}://{1}/tmtrack/tmtrack.dll?JSONPage&command=getusers&showDeleted=0&showExternal=0&showLimited=0&onlyManaged=1&startIndex=0&fetchSize=99999&sortBy=loginId&sortOrder=ascend&searchString=",
                    _protocol, Params.SBMHostPort);

            var response = WebReq.Make(jsonUrl, ssoBase64: _ssoBase64, contentLength: 0);

            JObject jObject = JObject.Parse(response);
            JToken userList = jObject["userList"];

            _objectExcahnge.Add("SBM.Users", new Dictionary<string, int>());
            foreach (var user in userList)
                ((Dictionary<string, int>) _objectExcahnge["SBM.Users"]).Add(user["loginId"].Value<string>(), user["id"].Value<int>());
        }

        private void GetProjects()
        {
            var getParentIdjsonUrl =
                string.Format(
                    "{0}://{1}/tmtrack/tmtrack.dll?JSONPage&command=getprojects&startIndex=0&fetchSize=1&sortBy=sequence&sortOrder=ascend&action=1&solutionId=-1&filterOption=1",
                    _protocol, Params.SBMHostPort);

            var getParentIdresponse = WebReq.Make(getParentIdjsonUrl, ssoBase64: _ssoBase64, contentLength: 0);
            JObject getParentIdjObject = JObject.Parse(getParentIdresponse);
            JToken getParentIdprojectList = getParentIdjObject["projectList"];
            var parentId = getParentIdprojectList.First["id"].Value<string>();

            var jsonUrl =
                string.Format(
                    "{0}://{1}/tmtrack/tmtrack.dll?JSONPage&command=getprojects&startIndex=0&fetchSize=99999&sortBy=sequence&sortOrder=ascend&searchString=&action=1&solutionId=-1&parentId={2}&projectId=-1",
                    _protocol, Params.SBMHostPort, parentId);

            var response = WebReq.Make(jsonUrl, ssoBase64: _ssoBase64, contentLength: 0);

            JObject jObject = JObject.Parse(response);
            JToken projectList = jObject["projectList"];

            _objectExcahnge.Add("SBM.RolesPerProject", new Dictionary<string, SBMRolesPerProject>());
            foreach (var project in projectList)
            {
                var ids = new SBMRolesPerProject {id = project["id"].Value<int>(), solutionId = project["solutionId"].Value<string>()};
                ((Dictionary<string, SBMRolesPerProject>) _objectExcahnge["SBM.RolesPerProject"]).Add(project["name"].Value<string>(), ids);
            }
        }

        private void GetRoles()
        {
            var projects = (Dictionary<string, SBMRolesPerProject>) _objectExcahnge["SBM.RolesPerProject"];

            foreach (var sbmProject in projects)
            {
                var projectId = sbmProject.Value.id;
                var solutionId = sbmProject.Value.solutionId;

                var jsonUrl =
                    string.Format(
                        "{0}://{1}/tmtrack/tmtrack.dll?JSONPage&command=getapplicationroles&startIndex=0&fetchSize=99999&sortBy=NAME&sortOrder=ascending&applicationId={2}&projectId={3}",
                        _protocol, Params.SBMHostPort, solutionId, projectId);
                var response = WebReq.Make(jsonUrl, ssoBase64: _ssoBase64, contentLength: 0);

                JObject jObject = JObject.Parse(response);
                JToken[] roles = jObject["applicationRoles"]["roles"].ToArray();

                foreach (var role in roles)
                    sbmProject.Value.Roles.Add(role["roleName"].Value<string>(), role["id"].Value<int>());
            }
        }
    }

    public class SBMRolesPerProject
    {
        public Dictionary<string, int> Roles;
        public int id;
        public string solutionId;

        public SBMRolesPerProject()
        {
            Roles = new Dictionary<string, int>();
        }
    }

// ReSharper disable InconsistentNaming
#pragma warning disable 0169, 0649, 0414

    public class SBMSetRole
    {
        [JsonProperty(PropertyName = "changed")] public bool _changed = false;
        [JsonProperty(PropertyName = "checked")] public int _checked = 0;
        [JsonProperty(PropertyName = "contextId")] private int _contextId;
        [JsonProperty(PropertyName = "contextType")] public int _contextType = 1;
        [JsonProperty(PropertyName = "flexClassName")] public string _flexClassName = null;
        [JsonProperty(PropertyName = "id")] public int _id = 0;
        [JsonProperty(PropertyName = "modelLocatorId")] public string _modelLocatorId = null;
        [JsonProperty(PropertyName = "permissionType")] public int _permissionType = 1;
        [JsonProperty(PropertyName = "subjectId")] private int _subjectId;
        [JsonProperty(PropertyName = "subjectType")] private int _subjectType;
        [JsonProperty(PropertyName = "userId")] public string _userId = null;
        [JsonProperty(PropertyName = "granted")] public int granted;
        [JsonProperty(PropertyName = "permissionId")] public int permissionId;

        public SBMSetRole(int projectId, int usrOrGroupId, int usrOrGroup)
        {
            //granted = 0; 1 - Enabled, -1 - Disabled
            //permissionId = 0; - RoleId
            _contextId = projectId;
            _subjectId = usrOrGroupId;
            _subjectType = usrOrGroup; //2 - group, 1 - user
        }
    }

    public class SetMembership
    {
        [JsonProperty(PropertyName = "groupId")] private int _groupId;
        [JsonProperty(PropertyName = "id")] public string _id = null;
        [JsonProperty(PropertyName = "isCreated")] public int _isCreated = 1;
        [JsonProperty(PropertyName = "userId")] private int _userId;

        public SetMembership(int groupId, int userId)
        {
            _userId = userId;
            _groupId = groupId;
        }
    }

    internal class SBMCreateUserMap
    {
        [JsonProperty(PropertyName = "checked")] public int _checked = 0;
        [JsonProperty(PropertyName = "accessType")] public int accessType = 1;
        [JsonProperty(PropertyName = "accessTypeDisableMask")] public int accessTypeDisableMask = 0;
        [JsonProperty(PropertyName = "changed")] public bool changed = false;
        [JsonProperty(PropertyName = "compareObjectExclusions")] public string[] compareObjectExclusions = {"password", "memo"};
        [JsonProperty(PropertyName = "contactAction")] public int contactAction = 1;
        [JsonProperty(PropertyName = "contactExists")] public bool contactExists = false;
        [JsonProperty(PropertyName = "emailAddress")] public string emailAddress = "";
        [JsonProperty(PropertyName = "emailAliases")] public string emailAliases = "";
        [JsonProperty(PropertyName = "emailNotificationCC")] public string emailNotificationCC = "";
        [JsonProperty(PropertyName = "flexClassName")] public string flexClassName = "com.webadmin.vo.user.UserGeneralVO";
        [JsonProperty(PropertyName = "id")] public int id = -1;
        [JsonProperty(PropertyName = "lastLoginDate")] public string lastLoginDate = "";
        [JsonProperty(PropertyName = "loginCreateDate")] public string loginCreateDate;
        [JsonProperty(PropertyName = "loginId")] public string loginId;
        [JsonProperty(PropertyName = "memo")] public string memo = "";
        [JsonProperty(PropertyName = "modelLocatorId")] public string modelLocatorId = null;
        [JsonProperty(PropertyName = "name")] public string name;
        [JsonProperty(PropertyName = "password")] public string password;
        [JsonProperty(PropertyName = "status")] public int status = 0;
        [JsonProperty(PropertyName = "telephone")] public string telephone = "";
        [JsonProperty(PropertyName = "timestamp")] public int timestamp = -1;
        [JsonProperty(PropertyName = "useSystemSettingsForPassword")] public bool useSystemSettingsForPassword = true;
        [JsonProperty(PropertyName = "userId")] public string userId = null;
    }

    public class SBMSetUser
    {
        [JsonProperty(PropertyName = "ids")] private int[] ids = new int[1];
        [JsonProperty(PropertyName = "userGeneral")] private SBMCreateUserMap userGeneral = new SBMCreateUserMap();

        public SBMSetUser(string loginId, string pwd, string loginCreateDate, string name, string emailAddress, string emailNotificationCC, int id)
        {
            userGeneral.id = id;
            ids[0] = id;
            userGeneral.loginId = loginId;
            userGeneral.password = pwd;
            userGeneral.loginCreateDate = loginCreateDate;
            userGeneral.name = name;
            userGeneral.emailAddress = emailAddress;
            userGeneral.emailNotificationCC = emailNotificationCC;
        }

        public SBMSetUser(string loginId, string pwd, string loginCreateDate, string name, string emailAddress, string emailNotificationCC)
        {
            ids[0] = -1;
            userGeneral.loginId = loginId;
            userGeneral.password = pwd;
            userGeneral.loginCreateDate = loginCreateDate;
            userGeneral.name = name;
            userGeneral.emailAddress = emailAddress;
            userGeneral.emailNotificationCC = emailNotificationCC;
        }
    }

    public class SBMSetGroup
    {
        //[JsonProperty(PropertyName = "userGeneral")] SBMCreateGroupMap userGeneral = new SBMCreateGroupMap();
        //[JsonProperty(PropertyName = "ids")] int []  ids = new int[1];

        [JsonProperty(PropertyName = "checked")] public int _checked = 0;
        [JsonProperty(PropertyName = "accessType")] public int accessType = 1;
        [JsonProperty(PropertyName = "accessTypeDisableMask")] public int accessTypeDisableMask = 0;
        [JsonProperty(PropertyName = "addNewExternalUsersAutomatically")] public bool addNewExternalUsersAutomatically = false;
        [JsonProperty(PropertyName = "autoExternalGroupId")] public int autoExternalGroupId = 0;
        [JsonProperty(PropertyName = "autoExternalGroupName")] public string autoExternalGroupName = null;
        [JsonProperty(PropertyName = "changed")] public bool changed = false;
        [JsonProperty(PropertyName = "flexClassName")] public string flexClassName = "com.webadmin.vo.group.GroupGeneralVO";
        [JsonProperty(PropertyName = "id")] public int id = -1;
        [JsonProperty(PropertyName = "memo")] public string memo = "";
        [JsonProperty(PropertyName = "modelLocatorId")] public string modelLocatorId = null;
        [JsonProperty(PropertyName = "name")] public string name;
        [JsonProperty(PropertyName = "predefined")] public int predefined = 0;
        [JsonProperty(PropertyName = "timestamp")] public int timestamp = -1;
        [JsonProperty(PropertyName = "userId")] public string userId = null;

        public SBMSetGroup(string grpName)
        {
            //ids[0] = -1;
            //userGeneral.
            name = grpName;
        }
    }

    internal class SBMCreateGroupMap {}
}