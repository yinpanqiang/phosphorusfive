/*
 * Phosphorus Five, copyright 2014 - 2017, Thomas Hansen, thomas@gaiasoul.com
 * 
 * This file is part of Phosphorus Five.
 *
 * Phosphorus Five is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License version 3, as published by
 * the Free Software Foundation.
 *
 *
 * Phosphorus Five is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Phosphorus Five.  If not, see <http://www.gnu.org/licenses/>.
 * 
 * If you cannot for some reasons use the GPL license, Phosphorus
 * Five is also commercially available under Quid Pro Quo terms. Check 
 * out our website at http://gaiasoul.com for more details.
 */

using System;
using System.IO;
using System.Web;
using System.Linq;
using System.Security;
using System.Globalization;
using System.Text.RegularExpressions;
using p5.exp;
using p5.core;
using p5.exp.exceptions;

namespace p5.auth.helpers
{
    /// <summary>
    ///     Class wrapping authentication helper features of Phosphorus Five
    /// </summary>
    static class AuthenticationHelper
    {
        // Name of credential cookie, used to store username and hashsalted password
        const string _credentialCookieName = "_p5_user";
        const string _credentialsNotAcceptedException = "Credentials not accepted";
        const string _contextTicketSessionName = ".p5.auth.context-ticket";

        /*
         * Returns user Context Ticket (Context "user")
         */
        public static ContextTicket GetTicket (ApplicationContext context)
        {
            if (HttpContext.Current.Session [_contextTicketSessionName] == null) {

                // No user is logged in, using default impersonated user
                HttpContext.Current.Session [_contextTicketSessionName] = CreateDefaultTicket (context);
            }
            return HttpContext.Current.Session [_contextTicketSessionName] as ContextTicket;
        }

        /*
         * Returns true if Context Ticket is already set
         */
        public static bool ContextTicketIsSet {
            get {
                return HttpContext.Current.Session != null && HttpContext.Current.Session [_contextTicketSessionName] != null;
            }
        }

        /*
         * Tries to login user according to given user credentials
         */
        public static void Login (ApplicationContext context, Node args)
        {
            // Defaulting result of Active Event to unsuccessful.
            args.Value = false;

            // Retrieving supplied credentials
            string username = args.GetExChildValue<string> ("username", context);
            string password = args.GetExChildValue<string> ("password", context);
            args.FindOrInsert ("password").Value = "xxx"; // In case an exception occurs.
            bool persist = args.GetExChildValue ("persist", context, false);

            /*
             * Checking if current username has attempted to login just recently, and the
             * configured timespan for each successive login attempt per user, has not passed.
             * 
             * This should be able to defend us from a "brute force password attack".
             * 
             * Notice, we also use the web cache, to avoid having an adversary able to 
             * flood the memory of the server, by providing multiple different passwords,
             * which if we used the application object would flood it with new entries
             * until the server's memory was exhausted.
             */
            var bruteConf = new Node (".p5.config.get", ".p5.auth.cooldown-period");
            var cooldown = context.RaiseEvent (".p5.config.get", bruteConf) [0]?.Get (context, -1) ?? -1;
            if (cooldown != -1) {

                // User has configured the system to have a "cooldown period" for successive login attempts.
                var bruteForceLastAttempt = new Node (".p5.web.cache.get", ".p5.io.last-login-attempt-for-" + username);
                var lastAttemptNode = context.RaiseEvent (".p5.web.cache.get", bruteForceLastAttempt);
                if (lastAttemptNode.Count > 0) {

                    // Previous attempt has been attempted.
                    var date = lastAttemptNode [0].Get<DateTime> (context, DateTime.MinValue);
                    int timeSpanSeconds = Convert.ToInt32 ((DateTime.Now - date).TotalSeconds);
                    if (timeSpanSeconds < cooldown) {
                        throw new LambdaException ("You need to wait " + (cooldown - timeSpanSeconds) + " seconds before you can try again", args, context);
                    }
                }
            }
            
            // Getting system salt.
            var serverSalt = ServerSalt (context);

            // Then creating system fingerprint from given password.
            var hashedPassword = context.RaiseEvent ("p5.crypto.hash.create-sha256", new Node ("", serverSalt + password)).Get<string> (context);

            // Retrieving password file as a Node.
            Node pwdFile = AuthFile.GetAuthFile (context);

            /*
             * Checking for match on specified username.
             * 
             * Notice, we do this after we have retrieved server salt and created our hash, to make it more
             * difficult for an adversary to "guess" usernames in the system by brute force, trying multiple
             * different usernames, and record the time it took for a failed attempt to return to client.
             * 
             * We also throw the exact same exception if the username doesn't exist, as we do if the passwords
             * don't match. This might be paranoia level of 12 out of 10, but allows the application developer
             * to avoid communicating anything out about the system, if he needs that kind of security.
             */
            Node userNode = pwdFile ["users"] [username];
            if (userNode == null)
                throw new LambdaSecurityException (_credentialsNotAcceptedException, args, context);

            // Checking for match on password.
            if (userNode ["password"].Get<string> (context) != hashedPassword) {

                // Making sure we guard against brute force password attacks.
                var bruteForceLastAttempt = new Node (".p5.web.cache.set", ".p5.io.last-login-attempt-for-" + username);
                bruteForceLastAttempt.Add ("src", DateTime.Now);
                context.RaiseEvent (".p5.web.cache.set", bruteForceLastAttempt);
                throw new LambdaSecurityException (_credentialsNotAcceptedException, args, context);
            }

            // Success, creating our ticket.
            string role = userNode ["role"].Get<string> (context);
            SetTicket (new ContextTicket (username, role, false));
            args.Value = true;

            // Associating newly created Ticket with Application Context, since user now possibly have extended rights.
            context.UpdateTicket (GetTicket (context));

            // Checking if we should create persistent cookie on disc to remember username for given client.
            if (persist) {

                // Caller wants to create persistent cookie to remember username/password.
                var cookie = new HttpCookie (_credentialCookieName);
                cookie.Expires = DateTime.Now.AddDays (context.RaiseEvent (
                    ".p5.config.get",
                    new Node (".p5.config.get", "p5.auth.credential-cookie-valid")) [0].Get<int> (context));
                cookie.HttpOnly = true; // To avoid JavaScript access to credential cookie.

                /*
                 * The value of our cookie is in "username hashed-password" format.
                 *
                 * This is an entropy of roughly 1.1579e+77, making a brute force attack
                 * impossible, at least without a Rainbow/Dictionary attack, which should
                 * be effectively prevented, by having a single static server salt,
                 * which again is cryptographically secured and persisted to disc
                 * in the "auth" file, and hence normally inaccessible for an adversary.
                 */
                cookie.Value = username + " " + hashedPassword;
                HttpContext.Current.Response.Cookies.Add (cookie);
            }

            // Making sure we invoke an [.onlogin] lambda callbacks for user.
            var onLogin = new Node ();
            GetSettings (context, onLogin);
            if (onLogin [".onlogin"] != null) {
                var lambda = onLogin [".onlogin"].Clone ();
                context.RaiseEvent ("eval", lambda);
            }
        }

        /*
         * Logs out user.
         */
        public static void Logout (ApplicationContext context)
        {
            // Making sure we invoke an [.onlogin] lambda callbacks for user.
            var onLogout = new Node ();
            GetSettings (context, onLogout);
            if (onLogout [".onlogout"] != null) {
                var lambda = onLogout [".onlogout"].Clone ();
                context.RaiseEvent ("eval", lambda);
            }

            // By destroying Ticket, default user will be used for current session, until user logs in again.
            SetTicket (null);

            // Destroying persistent credentials cookie, if there is one.
            HttpCookie cookie = HttpContext.Current.Request.Cookies.Get (_credentialCookieName);
            if (cookie != null) {

                // Making sure cookie is destroyed on the client side by setting its expiration date to "today - 1 day".
                cookie.Expires = DateTime.Now.AddDays (-1);
                HttpContext.Current.Response.Cookies.Add (cookie);
            }
        }

        /*
         * Lists all users in system.
         */
        public static void ListUsers (ApplicationContext context, Node args)
        {
            // Retrieving "auth" file in node format.
            var authFile = AuthFile.GetAuthFile (context);
            
            // Retrieving guest account name, to make sure we exclude it as a user, since it's not a "real user" per se.
            var guestAccountName = context.RaiseEvent (".p5.auth.get-default-context-username").Get<string> (context);

            // Looping through each user in [users] node of "auth" file.
            foreach (var idxUserNode in authFile ["users"].Children) {

                if (idxUserNode.Name == guestAccountName)
                    continue;

                // Returning user's name, and role he belongs to.
                args.Add (idxUserNode.Name, idxUserNode ["role"].Value);
            }
        }
        
        /*
         * Returns server-salt for application.
         */
        public static string ServerSalt (ApplicationContext context)
        {
            // Retrieving "auth" file in node format.
            var authFile = AuthFile.GetAuthFile (context);
            return authFile.GetChildValue<string> ("server-salt", context);
        }
        
        /*
         * Returns PGP key's fingerprint.
         */
        public static string GnuPGKeypair (ApplicationContext context)
        {
            // Retrieving "auth" file in node format.
            var authFile = AuthFile.GetAuthFile (context);
            return authFile.GetChildValue<string> ("gnupg-keypair", context);
        }
        
        /*
         * Sets the server salt for server.
         */
        public static void SetServerSalt (ApplicationContext context, Node args, string salt)
        {
            AuthFile.ModifyAuthFile (context, delegate (Node node) {
                if (node.Children.Any (ix => ix.Name == "server-salt"))
                    throw new LambdaSecurityException ("Tried to change server salt after initial creation", args, context);
                node.FindOrInsert ("server-salt").Value = salt;
            });
        }
        
        /*
         * Sets the GnuPG keypair for server.
         */
        public static void SetGnuPGKeypair (ApplicationContext context, Node args, string fingerprint)
        {
            AuthFile.ModifyAuthFile (context, delegate (Node node) {
                if (node.Children.Any (ix => ix.Name == "gnupg-keypair"))
                    throw new LambdaSecurityException ("Tried to change GnuPG keypair after initial creation", args, context);
                node.FindOrInsert ("gnupg-keypair").Value = fingerprint;
            });
        }

        /*
         * Checks if password is good and in accordance to password regime.
         */
        public static bool IsGoodPassword (ApplicationContext context, string password)
        {
            // Retrieving password rules from web.config, if any.
            var pwdRulesNode = new Node (".p5.config.get", "p5.auth.password-rules");
            var pwdRule = context.RaiseEvent (".p5.config.get", pwdRulesNode) [0]?.Get (context, "");
            if (!string.IsNullOrEmpty (pwdRule)) {

                // Verifying that specified password obeys by rules from web.config.
                Regex regex = new Regex (pwdRule);
                if (!regex.IsMatch (password)) {

                    // New password was not accepted, returning false.
                    return false;
                }
            }
            return true;
        }
            
        /*
         * Creates a new user.
         */
        public static void CreateUser (ApplicationContext context, Node args)
        {
            // Retrieving arguments.
            string username = args.GetExValue<string> (context);
            string password = args.GetExChildValue<string> ("password", context);
            string role = args.GetExChildValue<string> ("role", context);
            
            // Sanity checking role name towards guest account name.
            if (role == context.RaiseEvent (".p5.auth.get-default-context-role").Get<string> (context))
                throw new LambdaException ("Sorry, but that's the name of our guest account role.", args, context);
            
            // Sanity checking username towards guest account name.
            if (username == context.RaiseEvent (".p5.auth.get-default-context-username").Get<string> (context))
                throw new LambdaException ("Sorry, but that's the name of our guest account.", args, context);

            // Making sure [password] never leaves method in case of an exception.
            args.FindOrInsert ("password").Value = "xxx";
            
            // Retrieving password rules from web.config, if any.
            if (!IsGoodPassword (context, password)) {

                // New password was not accepted, throwing an exception.
                var pwdRulesNode = new Node (".p5.config.get", "p5.auth.password-rules");
                var pwdRule = context.RaiseEvent (".p5.config.get", pwdRulesNode) [0]?.Get (context, "");
                throw new LambdaSecurityException ("Password didn't obey by your configuration settings, which are as follows; " + pwdRule, args, context);
            }

            // Basic sanity check new user's data.
            if (string.IsNullOrEmpty (username) || string.IsNullOrEmpty (password) || string.IsNullOrEmpty (role))
                throw new LambdaException (
                    "User must have username as value, [password] and [role] at the very least",
                    args,
                    context);

            // Verifying username is valid, since we'll need to create a folder for user.
            VerifyUsernameValid (username);

            // Retrieving system salt before we enter write lock.
            var serverSalt = ServerSalt (context);

            // Then salting user's password.
            var userPasswordFingerprint = context.RaiseEvent ("p5.crypto.hash.create-sha256", new Node ("", serverSalt + password)).Get<string> (context);

            // Locking access to password file as we create new user object.
            AuthFile.ModifyAuthFile (
                context,
                delegate (Node authFile) {

                    // Checking if user exist from before.
                    if (authFile ["users"] [username] != null)
                        throw new LambdaException (
                            "Sorry, that [username] is already taken by another user in the system",
                            args,
                            context);

                    // Adding user.
                    authFile ["users"].Add (username);

                    // Creates a salt and password for user.
                    authFile ["users"].LastChild.Add ("password", userPasswordFingerprint);

                    // Adding user to specified role.
                    authFile ["users"].LastChild.Add ("role", role);

                    // Adding all other specified objects to user.
                    foreach (var idxNode in args.Children.Where (ix => ix.Name != "username" && ix.Name != "password" && ix.Name != "role")) {

                        // Only adding nodes with some sort of actual value.
                        if (idxNode.Value != null || idxNode.Count > 0)
                            authFile ["users"].LastChild.Add (idxNode.Clone ());
                    }
                });

            // Creating newly created user's directory structure.
            CreateUserDirectory (context, username);
        }

        /*
         * Retrieves a specific user from the system.
         */
        public static void GetUser (ApplicationContext context, Node args)
        {
            // Retrieving "auth" file in node format.
            var authFile = AuthFile.GetAuthFile (context);

            // Iterating all users requested by caller.
            foreach (var idxUsername in XUtil.Iterate<string> (context, args)) {

                // Checking if user exist
                if (authFile ["users"] [idxUsername] == null)
                    throw new LambdaException (
                        string.Format ("User '{0}' does not exist", idxUsername),
                        args,
                        context);

                // Adding user's node as return value, and each property of user, except [password]
                args.Add (idxUsername);
                args [idxUsername].AddRange (authFile ["users"] [idxUsername].Clone ().Children.Where (ix => ix.Name != "password"));
            }
        }

        /*
         * Retrieves a specific user from system
         */
        public static void DeleteUser (ApplicationContext context, Node args)
        {
            // Locking access to password file as we create new user object
            AuthFile.ModifyAuthFile (
                context,
                delegate (Node authFile) {

                    // Iterating all users requested deleted by caller
                    foreach (var idxUsername in XUtil.Iterate<string> (context, args)) {

                        // Checking if user exist
                        if (authFile ["users"] [idxUsername] == null)
                            throw new LambdaException (
                                string.Format ("User '{0}' does not exist", idxUsername),
                                args,
                                context);

                        // Deleting currently iterated user
                        authFile ["users"] [idxUsername].UnTie ();

                        // Deleting user's home directory
                        context.RaiseEvent ("p5.io.folder.delete", new Node ("", "/users/" + idxUsername + "/"));
                    }
                });
        }

        /*
         * Edits an existing user
         */
        public static void EditUser (ApplicationContext context, Node args)
        {
            // Retrieving username, and sanity checking invocation.
            string username = args.GetExValue<string> (context);
            if (args ["username"] != null)
                throw new LambdaSecurityException ("Cannot change username for user", args, context);

            // Retrieving new password and role, defaulting to null, which will not update existing values.
            string newPassword = args.GetExChildValue<string> ("password", context);
            string newRole = args.GetExChildValue<string> ("role", context);

            // Sanity checking role name towards guest account name.
            if (newRole == context.RaiseEvent (".p5.auth.get-default-context-role").Get<string> (context))
                throw new LambdaException ("Sorry, but that's the name of our guest account role.", args, context);

            // Retrieving password rules from web.config, if any.
            // But only if a new password was given.
            if (!string.IsNullOrEmpty (newPassword)) {
                
                // Verifying password conforms to password rules.
                var pwdRulesNode = new Node (".p5.config.get", "p5.auth.password-rules");
                var pwdRule = context.RaiseEvent (".p5.config.get", pwdRulesNode) [0]?.Get (context, "");
                if (!string.IsNullOrEmpty (pwdRule)) {

                    // Verifying that specified password obeys by rules from web.config.
                    Regex regex = new Regex (pwdRule);
                    if (!regex.IsMatch (newPassword)) {

                        // New password was not accepted, throwing an exception.
                        args.FindOrInsert ("password").Value = "xxx";
                        throw new LambdaSecurityException ("Password didn't obey by your configuration settings, which are as follows; " + pwdRule, args, context);
                    }
                }
            }

            // Retrieving system salt before we enter write lock. (important, since otherwise we'd have a deadlock condition here).
            var serverSalt = newPassword == null ? null : ServerSalt (context);

            // Locking access to password file as we edit user object.
            AuthFile.ModifyAuthFile (
                context,
                delegate (Node authFile) {

                    // Checking to see if user exist.
                    if (authFile ["users"] [username] == null)
                        throw new LambdaException (
                            "Sorry, that user does not exist",
                            args,
                            context);

                    // Updating user's password, but only if a new password was supplied by caller.
                    if (!string.IsNullOrEmpty (newPassword)) {

                        // Making sure we salt password with system salt, before we create our SHA256 value, which is what we actually store in our "auth" file.
                        var userPasswordFingerprint = context.RaiseEvent ("p5.crypto.hash.create-sha256", new Node ("", serverSalt + newPassword)).Get<string> (context);
                        authFile ["users"] [username] ["password"].Value = userPasswordFingerprint;
                    }

                    // Updating user's role, if a new role was supplied by caller.
                    if (newRole != null) {
                        authFile ["users"] [username] ["role"].Value = newRole;
                    }

                    // Checking if caller wants to edit settings.
                    if (args.Name == "p5.auth.users.edit") {

                        // Removing old settings.
                        authFile ["users"] [username].RemoveAll (ix => ix.Name != "password" && ix.Name != "role");

                        // Adding all other specified objects to user.
                        foreach (var idxNode in args.Children.Where (ix => ix.Name != "password" && ix.Name != "role")) {

                            authFile ["users"] [username].Add (idxNode.Clone ());
                        }
                    }
                });
        }

        /*
         * Retrieves settings for currently logged in user
         */
        public static void GetSettings (ApplicationContext context, Node args)
        {
            // Retrieving "auth" file in node format
            var authFile = AuthFile.GetAuthFile (context);

            // Checking if user exist
            if (authFile ["users"] [context.Ticket.Username] == null)
                throw new LambdaException (
                    "You do not exist",
                    args,
                    context);

            // Checking if caller is retieving a single section.
            var section = args.GetExValue (context, "");
            if (string.IsNullOrEmpty (section)) {
                
                // All settings invocation.
                args.AddRange (authFile ["users"] [context.Ticket.Username].Clone ().Children.Where (ix => ix.Name != "password" && ix.Name != "role"));

            } else if (section != "password" && section != "role") {

                // Single section invocation.
                var sectionNode = authFile ["users"] [context.Ticket.Username] [section]?.Clone ();
                if (sectionNode != null)
                    args.Add (sectionNode);

            } else {

                // Illegal attempt at trying to retrieve role or password.
                throw new LambdaSecurityException ("Illegal invocation, you can't retrieve [password] or [role]", args, context);
            }
        }

        /*
         * Changes the settings for currently logged in user
         */
        public static void ChangeSettings (ApplicationContext context, Node args)
        {
            // Getting username for current context.
            string username = context.Ticket.Username;

            // Making sure default user cannot change his settings.
            if (context.Ticket.IsDefault)
                throw new LambdaSecurityException ("The default user cannot change his settings", args, context);

            // Verifying that there's no "funny business" going on here.
            if (args ["password"] != null || args ["role"] != null)
                throw new LambdaSecurityException ("You cannot change your password or role with this Active Event", args, context);

            // Locking access to password file as we edit user object
            AuthFile.ModifyAuthFile (
                context,
                delegate (Node authFile) {

                    // Checking if invocation is for a single section, or if it's for everything.
                    var section = args.GetExValue (context, "");
                    if (string.IsNullOrEmpty (section)) {

                        // Removing old settings
                        authFile ["users"] [username].RemoveAll (ix => ix.Name != "password" && ix.Name != "role");

                        // Changing all settings for user
                        foreach (var idxNode in args.Children) {
                            authFile ["users"] [username].Add (idxNode.Clone ());
                        }

                    } else if (args.Count == 1) {

                        // Removing old settings
                        authFile ["users"] [username] [section]?.UnTie (); 

                        // Changing all settings for user.
                        authFile ["users"] [username].Add (args.FirstChild.Clone ());

                    } else {

                        // Oops, can't set a single section to multiple values.
                        throw new LambdaException ("You can't set a single section to multiple values", args, context);
                    }
                });
        }

        /*
         * Changes the password for currently logged in user
         */
        public static void ChangePassword (ApplicationContext context, Node args)
        {
            // Retrieving new password, and doing some basic sanity check.
            string password = args.GetExValue (context, "");
            if (string.IsNullOrEmpty (password))
                throw new LambdaException ("No password supplied", args, context);
            
            // Retrieving password rules from web.config, if any.
            var pwdRulesNode = new Node (".p5.config.get", "p5.auth.password-rules");
            var pwdRule = context.RaiseEvent (".p5.config.get", pwdRulesNode) [0]?.Get (context, "");
            if (!string.IsNullOrEmpty (pwdRule)) {

                // Verifying that specified password obeys by rules from web.config.
                Regex regex = new Regex (pwdRule);
                if (!regex.IsMatch (password)) {

                    // New password was not accepted, throwing an exception.
                    args.FindOrInsert ("password").Value = "xxx";
                    throw new LambdaSecurityException ("Password didn't obey by your configuration settings, which are as follows; " + pwdRule, args, context);
                }
            }

            // Figuring out username of current context.
            string username = context.Ticket.Username;

            // Retrieving system salt before we enter write lock.
            var serverSalt = ServerSalt (context);

            // Locking access to password file as we edit user object
            AuthFile.ModifyAuthFile (
                context,
                delegate (Node authFile) {

                    // Changing user's password
                    // Then salting password with user salt and system, before salting it with system salt
                    var userPasswordFingerprint = context.RaiseEvent ("p5.crypto.hash.create-sha256", new Node ("", serverSalt + password)).Get<string> (context);
                    authFile ["users"] [username] ["password"].Value = userPasswordFingerprint;
                });
        }

        /*
         * Returns all existing roles in system
         */
        public static void GetRoles (ApplicationContext context, Node args)
        {
            // Making sure default role is added first.
            string defaultRole = context.RaiseEvent (".p5.auth.get-default-context-role").Get<string> (context);
            if (!string.IsNullOrEmpty (defaultRole)) {

                // There exist a default role, checking if it's already added
                if (args.Children.FirstOrDefault (ix => ix.Name == defaultRole) == null) {

                    // Default Role was not already added, therefor we add it to return lambda node
                    args.Add (defaultRole);
                }
            }

            // Getting password file in Node format, such that we can traverse file for all roles
            Node pwdFile = AuthFile.GetAuthFile (context);

            // Looping through each user object in password file, retrieving all roles
            foreach (var idxUserNode in pwdFile ["users"].Children) {

                // Retrieving role name of currently iterated user
                var role = idxUserNode ["role"].Get<string> (context);

                // Adding currently iterated role, unless already added, and incrementing user count for it
                args.FindOrInsert (role).Value = args [role].Get (context, 0) + 1;
            }
        }
        
        /*
         * Returns all access objects for system.
         */
        public static void ListAccess (ApplicationContext context, Node args)
        {
            // Getting password file in Node format, such that we can traverse file for all roles
            Node pwdFile = AuthFile.GetAuthFile (context);

            // Checking if we have any custom access rights in system.
            if (pwdFile ["access"] == null)
                return;

            // Checking which role caller requests access objects on behalf.
            string roles = null;
            if (context.Ticket.Role == "root") {

                // Checking if caller requested a particular role.
                roles = args.GetExChildValue<string> ("role", context, null);

            } else {

                // A non-root user is not allowed to request anything besides his own access objects.
                if (args ["role"] != null)
                    throw new LambdaException ("A non-root user cannot request access objects for anything but his own role", args, context);
                roles = context.Ticket.Role;
            }

            // Looping through each user object in password file, retrieving all roles
            foreach (var idxUserNode in pwdFile ["access"].Children) {

                // Adding currently iterated access to return args.
                if (roles == null) {

                    // Adding everything.
                    args.Add (idxUserNode.Clone ());

                } else {

                    // Adding only access objects requested by caller.
                    if (idxUserNode.Name == "*" || idxUserNode.Name == roles)
                        args.Add (idxUserNode.Clone ());
                }
            }
        }
        
        /*
         * Returns all access objects for system.
         */
        public static void AddAccess (ApplicationContext context, Node args)
        {
            // Locking access to password file as we create new access object.
            AuthFile.ModifyAuthFile (
                context,
                delegate (Node authFile) {

                    // Verifying access rights exists.
                    if (authFile ["access"] == null)
                        authFile.Add ("access");

                    // Iterating all access objects passed in by caller, and adding them to root access node.
                    var access = authFile ["access"];
                    var newAccessRights = XUtil.Iterate<Node> (context, args).ToList ();
                    foreach (var idxAccess in newAccessRights) {

                        // Sanity checking.
                        var val = idxAccess.GetExValue (context, "");
                        if (string.IsNullOrEmpty (val)) {

                            // Creating a new random GUID as the ID of our access object.
                            val = Guid.NewGuid ().ToString ();
                            idxAccess.Value = val;

                        } else {

                            // Verifying access ID is unique.
                            if (access.Children.Any (ix => ix.Get (context, "") == val))
                                throw new LambdaException ("Each access right must have a unique name/value combination, and there's already another access right with the same name/value combination in your access list", idxAccess, context);
                        }

                        // Sanity checking access object.
                        if (idxAccess.Count == 0)
                            throw new LambdaException ("There's no actual content in your access object", idxAccess, context);

                        // Adding currently iterated access object.
                        access.Add (idxAccess.Clone ()); 
                    }
                });
        }
        
        /*
         * Returns all access objects for system.
         */
        public static void SetAccess (ApplicationContext context, Node args)
        {
            // Locking access to password file as we create new access object.
            AuthFile.ModifyAuthFile (
                context,
                delegate (Node authFile) {

                    // Verifying access rights exists.
                    if (authFile ["access"] == null)
                        authFile.Add ("access");

                    // Retrieving access root node.
                    var access = authFile ["access"];

                    // Clearing all previous access objects.
                    access.Clear ();

                    // Iterating all access objects supplied by caller, adding them to our access node.
                    var newAccessRights = XUtil.Iterate<Node> (context, args).ToList ();
                    foreach (var idxAccess in newAccessRights) {

                        // Sanity checking.
                        var val = idxAccess.GetExValue (context, "");
                        if (string.IsNullOrEmpty (val)) {

                            // Creating a new random GUID as the ID of our access object.
                            val = Guid.NewGuid ().ToString ();
                            idxAccess.Value = val;

                        } else {

                            // Verifying access ID is unique.
                            if (access.Children.Any (ix => ix.Get (context, "") == val))
                                throw new LambdaException ("Each access right must have a unique name/value combination, and there's already another access right with the same name/value combination in your access list", idxAccess, context);
                        }
                        
                        // Sanity checking access object.
                        if (idxAccess.Count == 0)
                            throw new LambdaException ("There's no actual content in your access object", idxAccess, context);

                        // Adding currently iterated access object.
                        access.Add (idxAccess.Clone ()); 
                    }
                });
        }

        /*
         * Returns all access objects for system.
         */
        public static void HasAccessToPath (ApplicationContext context, Node args)
        {
            // Checking is user is root, at which he has access to everything.
            if (context.Ticket.Role == "root") {
                args.Add ("explicit", true);
                args.Value = true;
                return;
            }

            // Retrieving [filter] argument.
            var filter = args.GetExChildValue<string> ("filter", context);
            if (string.IsNullOrEmpty (filter))
                throw new LambdaException ("No [filter] supplied", args, context);

            // Retrieving [path] argument.
            var path = args.GetExChildValue<string> ("path", context);
            if (string.IsNullOrEmpty (path))
                throw new LambdaException ("No [path] supplied", args, context);

            // Making sure we unroll path.
            path = context.RaiseEvent (".p5.io.unroll-path", new Node ("", path)).Get<string> (context);

            // Retrieving all access objects.
            var node = new Node ();
            AuthenticationHelper.ListAccess (context, node);

            // Defaulting access to invoker node's existing value.
            var has_access = args.Get (context, false);

            // Checking if we have any access objects at all.
            if (node.Count > 0) {

                // Getting children as list, such that we can more easily modify it.
                var access = node.Children.ToList ();

                // Removing all access right objects not relevant to current user, current path, and current operation type.
                access.RemoveAll (ix => ix.Name != "*" && ix.Name != context.Ticket.Role);
                access.RemoveAll (ix => ix [filter + ".allow"] == null && ix [filter + ".deny"] == null);

                // Notice, to support pats such as "~/xxx" and "@FOO/", we explicitly unroll paths in our access object(s), before doing a comparison for a match.
                access.RemoveAll (ix => !path.StartsWithEx (context.RaiseEvent (".p5.io.unroll-path", new Node ("", ix [0].Get<string> (context))).Get<string> (context)));

                // Checking if we still have some access right object(s).
                if (access.Count > 0) {

                    // Sorting remaining access rights on their path value.
                    access.Sort (delegate (Node lhs, Node rhs) {

                        // First doing a path comparison.
                        var retVal = string.Compare (lhs [0].Get<string> (context), rhs [0].Get<string> (context), true, CultureInfo.InvariantCulture);

                        /*
                         * If the paths were similar, we make sure all asterix (*) roles are sorted before any special role overrides.
                         * We do this such that a specifically mentioned role can override the value for an asterix (*) role declaration.
                         */
                        if (retVal == 0) {
                            if (lhs.Name == "*" && rhs.Name != "*")
                                retVal = -1;
                            else if (lhs.Name != "*" && rhs.Name == "*")
                                retVal = 1;
                        }
                        return retVal;
                    });

                    /*
                     * Looping through any remaining access rights, to see if that modifies our return value.
                     * Making sure we return to caller whether or not anything was found at all.
                     */
                    if (access.Count > 0)
                        args.Add ("explicit", true);
                    foreach (var idxAccess in access) {
                        if (idxAccess [0].Name == filter + ".allow") {

                            // Then we must verify that the file's type is correct, if there is an explicit [file-type] argument in this access object.
                            // Or allow access, if this is a folder request (ending eith "/") and the access object is a "folder type of access object".
                            var file_types = idxAccess [0].GetChildValue ("file-type", context, "").Split (new char [] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                            if (file_types.Length > 0) {

                                // File type declaration, making sure it matches specified path.
                                if (file_types.Any (ix => path.EndsWithEx ("." + ix))) {
                                    has_access = true;
                                } else {
                                    has_access = false;
                                }

                            } else if (path.EndsWithEx ("/") && idxAccess [0].GetChildValue ("folder", context, false)) {

                                // Folder access.
                                has_access = true;

                            } else {

                                // No type declaration for access object.
                                has_access = true;
                            }

                        } else if (idxAccess [0].Name == filter + ".deny") {

                            // Then we must verify that the file's type is correct, if there is an explicit [file-type] argument in this access object.
                            // Or allow access, if this is a folder request (ending eith "/") and the access object is a "folder type of access object".
                            var file_types = idxAccess [0].GetChildValue ("file-type", context, "").Split (new char [] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                            if (file_types.Length > 0) {

                                // File type declaration, making sure it matches specified path.
                                if (file_types.Any (ix => path.EndsWithEx ("." + ix))) {
                                    has_access = true;
                                } else {
                                    has_access = false;
                                }

                            } else if (path.EndsWithEx ("/") && idxAccess [0].GetChildValue ("folder", context, false)) {

                                // Folder access.
                                has_access = false;

                            } else {

                                // No type declaration for access object.
                                has_access = false;
                            }
                        }
                    }
                }
            }

            // Returns access to caller.
            args.Value = has_access;
        }
            
        /*
         * Returns all access objects for system.
         */
        public static void DeleteAccess (ApplicationContext context, Node args)
        {
            // Locking access to password file as we create new access object.
            AuthFile.ModifyAuthFile (
                context,
                delegate (Node authFile) {

                    // Verifying access rights exists.
                    if (authFile ["access"] == null)
                        return;

                    // Iterating all access objects passed in by caller.
                    var access = authFile ["access"];
                    var delAccess = XUtil.Iterate<Node> (context, args).ToList ();
                    foreach (var idxAccess in delAccess) {

                        // Removing all matches.
                        access.Children.First (ix => ix.Name == idxAccess.Name && ix.Get (context, "") == idxAccess.GetExValue (context, "")).UnTie ();
                    }
                });
        }

        /*
         * Changes password of "root" account, but only if existing root account's password 
         * is null. Used during setup of system
         */
        public static void SetRootPassword (ApplicationContext context, Node args)
        {
            // Retrieving password given.
            string password = args.GetExChildValue<string> ("password", context);

            // Retrieving password rules from web.config, if any.
            var pwdRulesNode = new Node (".p5.config.get", "p5.auth.password-rules");
            var pwdRule = context.RaiseEvent (".p5.config.get", pwdRulesNode) [0]?.Get (context, "");
            if (!string.IsNullOrEmpty (pwdRule)) {

                // Verifying that specified password obeys by rules from web.config.
                Regex regex = new Regex (pwdRule);
                if (!regex.IsMatch (password)) {
                    
                    // New password was not accepted, throwing an exception.
                    args.FindOrInsert ("password").Value = "xxx";
                    throw new LambdaSecurityException ("Password didn't obey by your configuration settings, which are as follows; " + pwdRule, args, context);
                }
            }

            // Creating root account.
            var rootAccountNode = new Node ("", "root");
            rootAccountNode.Add ("password", password);
            rootAccountNode.Add ("role", "root");
            CreateUser (context, rootAccountNode);

            // Creating "guest account" section, which is needed for settings among other things.
            var guestAccountName = context.RaiseEvent (".p5.auth.get-default-context-username").Get<string> (context);
            AuthFile.ModifyAuthFile (
                context,
                delegate (Node authFile) {
                    authFile ["users"].Add (guestAccountName);
                    authFile ["users"] ["guest"].Add ("role", context.RaiseEvent (".p5.auth.get-default-context-role").Get<string> (context));
                });
        }

        /*
         * Returns true if root account's password is null, which means that server is not setup yet
         */
        public static bool NoExistingRootAccount (ApplicationContext context)
        {
            // Retrieving password file, and making sure we lock access to file as we do
            Node rootPwdNode = AuthFile.GetAuthFile (context) ["users"] ["root"];

            // Returning true if root account does not exist
            return rootPwdNode == null;
        }

        /*
         * Will try to login from persistent cookie
         */
        public static void TryLoginFromPersistentCookie (ApplicationContext context)
        {
            try {
                // Making sure we do NOT try to login from persistent cookie if root password is null, at which
                // case the system has been reset, and cookie (obviously) is not valid!
                if (NoExistingRootAccount (context)) {

                    // Making sure we delete cookie, since (obviously) it is no longer valid!
                    // The simplest way to do this, is simply to throw exception, which will be handled 
                    // further down, and deletes current cookie!
                    throw null;
                }

                // Checking if client has persistent cookie
                HttpCookie cookie = HttpContext.Current.Request.Cookies.Get (_credentialCookieName);
                if (cookie != null) {

                    // We have a cookie, try to use it as credentials
                    LoginFromCookie (cookie, context);
                }
            } catch {

                // Making sure we delete cookie
                // We do not rethrow this, since reason might be because "salt" has changed, to explicitly log user
                // out, and that is actually not a "security issue", but a "feature". Besides, login-cooloff-seconds
                // will make sure "brute force" login through cookies are virtually impossible
                HttpCookie cookie = HttpContext.Current.Request.Cookies.Get (_credentialCookieName);
                if (cookie != null) {

                    // Deleting cookie!
                    cookie.Expires = DateTime.Now.AddDays (-1);
                    HttpContext.Current.Response.Cookies.Add (cookie);
                }
            }
        }

        #region [ -- Private helper methods -- ]

        /*
         * Sets user Context Ticket (context "user")
         */
        static void SetTicket (ContextTicket ticket)
        {
            HttpContext.Current.Session [_contextTicketSessionName] = ticket;
        }

        /*
         * Verifies that given username is valid
         */
        static void VerifyUsernameValid (string username)
        {
            foreach (var charIdx in username) {
                if ("abcdefghijklmnopqrstuvwxyz1234567890_-".IndexOf (charIdx) == -1)
                    throw new SecurityException ("Sorry, you cannot use the character '" + charIdx + "' in your usernames");
            }
        }

        /*
         * Creates folder structure for user
         */
        static void CreateUserDirectory (ApplicationContext context, string username)
        {
            // Retrieving root folder of system.
            var rootFolder = context.RaiseEvent (".p5.core.application-folder").Get<string> (context);

            // Creating folders for user, and making sure private directory stays private ...
            if (!Directory.Exists (rootFolder + "/users/" + username))
                Directory.CreateDirectory (rootFolder + "/users/" + username);

            if (!Directory.Exists (rootFolder + "/users/" + username + "/documents"))
                Directory.CreateDirectory (rootFolder + "/users/" + username + "/documents");

            if (!Directory.Exists (rootFolder + "/users/" + username + "/documents/private"))
                Directory.CreateDirectory (rootFolder + "/users/" + username + "/documents/private");

            if (!Directory.Exists (rootFolder + "/users/" + username + "/documents/public"))
                Directory.CreateDirectory (rootFolder + "/users/" + username + "/documents/public");

            if (!Directory.Exists (rootFolder + "/users/" + username + "/temp"))
                Directory.CreateDirectory (rootFolder + "/users/" + username + "/temp");
        }

        /*
         * Tries to login with the given cookie as credentials
         */
        static void LoginFromCookie (HttpCookie cookie, ApplicationContext context)
        {
            // User has persistent cookie associated with client
            var cookieSplits = cookie.Value.Split (' ');
            if (cookieSplits.Length != 2)
                throw new SecurityException ("Cookie not accepted");

            string cookieUsername = cookieSplits [0];
            string hashedPassword = cookieSplits [1];
            Node pwdFile = AuthFile.GetAuthFile (context);

            // Checking if user exist
            Node userNode = pwdFile ["users"] [cookieUsername];
            if (userNode == null)
                throw new SecurityException ("Cookie not accepted");

            // Notice, we do NOT THROW if passwords do not match, since it might simply mean that user has explicitly created a new "salt"
            // to throw out other clients that are currently persistently logged into system under his account
            if (hashedPassword == userNode ["password"].Get<string> (context)) {

                // MATCH, discarding previous Context Ticket and creating a new Ticket
                SetTicket (new ContextTicket (
                    userNode.Name,
                    userNode ["role"].Get<string> (context),
                    false));
            }
        }

        /*
         * Creates default Context Ticket according to settings from config file
         */
        static ContextTicket CreateDefaultTicket (ApplicationContext context)
        {
            return new ContextTicket (
                context.RaiseEvent (".p5.auth.get-default-context-username").Get<string> (context),
                context.RaiseEvent (".p5.auth.get-default-context-role").Get<string> (context),
                true);
        }

        #endregion
    }
}