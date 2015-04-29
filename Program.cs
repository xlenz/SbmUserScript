using System;

namespace userScript
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            //get & validate args
            Log.Info("Validating passed arguments...");
            GetParams.ReadParams(args);
            if (!GetParams.AreArgsGood() || !Params.AreParamsValid())
            {
                return;
            }
            Log.Ok("Provided arguments validated.");

            //read json and auth
            var webAdmin = new WebAdmin.WebAdmin();
            if (!webAdmin.IsInit()) return;

            Log.Info("Creating/updating users/groups/roles/membership...");
            try
            {
                //users and groups should be created before setting roles.
                //users and groups should be created before adding users to groups.

                //create/update users
                webAdmin.CreateUpdateUsers();
                //create groups
                webAdmin.CreateGroups();
                //add users to groups
                webAdmin.AddUsersToGroups();
                //set user roles
                webAdmin.SetUserRoles();
                //set group roles
                webAdmin.SetGroupRoles();

                Log.Ok("All done.");
            }
            catch (Exception e)
            {
                Log.Err(e.Message);
            }
        }
    }
}