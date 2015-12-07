﻿/*
 * Phosphorus Five, copyright 2014 - 2015, Thomas Hansen, phosphorusfive@gmail.com
 * Phosphorus Five is licensed under the terms of the MIT license, see the enclosed LICENSE file for details
 */

using System;
using System.IO;
using System.Web;
using System.Linq;
using System.Security;
using System.Configuration;
using System.Collections.Generic;
using p5.exp;
using p5.core;
using p5.exp.exceptions;

namespace p5.security
{
    /// <summary>
    ///     Class wrapping authentication helper features of Phosphorus Five
    /// </summary>
    internal static class AuthenticationHelper
    {
        // Used to lock access to password file
        private static object _passwordFileLocker = new object ();

        // Name of credential cookie, used to store username and hashsalted password
        private const string _credentialCookieName = "_p5_user";

        /*
         * Returns user Context Ticket (Context "user")
         */
        internal static ApplicationContext.ContextTicket GetTicket (ApplicationContext context)
        {
            // If we have no session or HttpContext, we login "default impersonated user" automatically
            if (HttpContext.Current == null || HttpContext.Current.Session == null)
                return CreateDefaultTicket (context);

            if (HttpContext.Current.Session["_ContextTicket"] == null) {

                // No user is logged in, using default impersonated user
                HttpContext.Current.Session ["_ContextTicket"] = CreateDefaultTicket (context);
            }
            return HttpContext.Current.Session["_ContextTicket"] as ApplicationContext.ContextTicket;
        }

        /*
         * Sets user Context Ticket (context "user")
         */
        internal static void SetTicket (ApplicationContext.ContextTicket ticket)
        { 
            HttpContext.Current.Session["_ContextTicket"] = ticket;
        }

        /*
         * Tries to login user according to given user credentials
         */
        internal static void Login (ApplicationContext context, Node args)
        {
            // Checking for a brute force login attack
            GuardAgainstBruteForce(context);

            // Defaulting result of Active Event to unsuccessful
            args.Value = false;

            // Retrieving supplied credentials
            string username = args.GetExChildValue<string> ("username", context);
            string password = args.GetExChildValue<string> ("password", context);
            bool persist = args.GetExChildValue ("persist", context, false);

            // Getting password file in Node format, but locking file access as we retrieve it
            Node pwdFile = null;
            lock (_passwordFileLocker) {
                pwdFile = GetPasswordFile(context);
            }

            // Checking for match on specified username
            Node userNode = pwdFile["users"][username];
            if (userNode == null)
                throw new SecurityException("Credentials not accepted");

            // Checking for match on password
            if (userNode["password"].Get<string> (context) != password)
                throw new SecurityException("Credentials not accepted");

            // Success, creating our ticket
            string role = userNode["role"].Get<string>(context);
            SetTicket (new ApplicationContext.ContextTicket(
                username, 
                role, 
                false));
            args.Value = true;

            // Removing last login attempt, to reset brute force login cool off seconds for user's IP address
            LastLoginAttemptForIP = DateTime.MinValue;

            // Associating newly created Ticket with Application Context, since user now possibly have extended rights
            context.UpdateTicket (GetTicket (context));

            // Checking if we should create persistent cookie on disc to remember username for given client
            // Notice, we do NOT allow root account to persist to cookie
            if (role != "root" && persist) {

                // Caller wants to create persistent cookie to remember username/password
                HttpCookie cookie = new HttpCookie(_credentialCookieName);
                cookie.Expires = DateTime.Now.AddDays(context.RaiseNative ("p5.security.get-credential-cookie-days").Get<int> (context));
                cookie.HttpOnly = true;
                string salt = userNode["salt"].Get<string>(context);
                cookie.Value = username + " " + context.RaiseNative ("md5-hash", new Node("", salt + password)).Value;
                HttpContext.Current.Response.Cookies.Add(cookie);
            }
        }

        /*
         * Logs out user
         */
        internal static void Logout (ApplicationContext context)
        {
            // By destroying Ticket, default user will be used for current session, until user logs in again
            SetTicket (null);

            // Destroying persistent credentials cookie, if there is one
            HttpCookie cookie = HttpContext.Current.Request.Cookies.Get(_credentialCookieName);
            if (cookie != null) {

                // Making sure cookie is destroyed on the client side by setting its expiration date to "today - 1 day"
                cookie.Expires = DateTime.Now.AddDays(-1);
                HttpContext.Current.Response.Cookies.Add(cookie);
            }
        }

        /*
         * Creates a new user
         */
        internal static void CreateUser (ApplicationContext context, Node args)
        {
            string username = args.GetExChildValue<string>("username", context);
            string password = args.GetExChildValue<string>("password", context);
            string role = args.GetExChildValue<string>("role", context);
            if (role == "root")
                throw new LambdaSecurityException("[create-user] Active Event tried to create 'root' user", args, context);

            // We need this guy to save passwords file, and create user folder structure
            string rootFolder = context.RaiseNative("p5.core.application-folder").Get<string>(context);

            // Verifying username is valid, since we'll need to create a folder for user
            VerifyUsernameValid (username);

            // Locking access to password file
            lock (_passwordFileLocker) {

                Node pwdFile = GetPasswordFile(context);
                if (pwdFile["users"][username] != null)
                    throw new ApplicationException("Sorry, that username is already taken by another user in the system");
                pwdFile["users"].Add(username);

                // Creates a salt for user
                var salt = CreateNewSalt ();
                pwdFile ["users"].LastChild.Add("salt", salt);
                pwdFile ["users"].LastChild.Add("password", password);
                pwdFile ["users"].LastChild.Add("role", role);

                // Getting path to 'auth' file
                string pwdFilePath = context.RaiseNative ("p5.security.get-auth-file").Get<string> (context).Replace("~/", rootFolder);

                // Saving password file
                using (TextWriter writer = File.CreateText(pwdFilePath)) {

                    // Converting lambda to Hyperlisp
                    Node lambdaNode = new Node();
                    lambdaNode.AddRange(pwdFile.Children);
                    writer.Write(context.RaiseNative ("lambda2lisp", lambdaNode).Get<string> (context));
                }
            }

            // Creating newly created user's directory structure
            CreateUserDirectory (rootFolder, username);
        }

        /*
         * Verifies that given username is valid
         */
        private static void VerifyUsernameValid (string username)
        {
            foreach (var charIdx in username) {
                if ("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890_-".IndexOf(charIdx) == -1)
                    throw new SecurityException("Sorry, you cannot use character '" + charIdx + "' in username");
            }
        }

        /*
         * Creates folder structure for user
         */
        private static void CreateUserDirectory (string rootFolder, string username)
        {
            // Creating folders for user, and making sure private directory stays private ...
            if (!Directory.Exists (rootFolder + "users/" + username))
                Directory.CreateDirectory (rootFolder + "users/" + username);

            if (!Directory.Exists (rootFolder + "users/" + username + "/documents"))
                Directory.CreateDirectory(rootFolder + "users/" + username + "/documents");

            if (!Directory.Exists (rootFolder + "users/" + username + "/documents/private"))
                Directory.CreateDirectory(rootFolder + "users/" + username + "/documents/private");

            if (!Directory.Exists (rootFolder + "users/" + username + "/documents/public"))
                Directory.CreateDirectory(rootFolder + "users/" + username + "/documents/public");

            if (!Directory.Exists (rootFolder + "users/" + username + "/tmp"))
                Directory.CreateDirectory(rootFolder + "users/" + username + "/tmp");

            if (!File.Exists (rootFolder + "users/" + username + "/documents/private/web.config"))
                File.Copy (
                    rootFolder + "users/root/documents/private/web.config", 
                    rootFolder + "users/" + username + "/documents/private/web.config");
        }

        /*
         * Returns all existing roles in system
         */
        internal static void GetRoles (ApplicationContext context, Node args)
        {
            // Getting password file in Node format, such that we can traverse file for all roles
            Node pwdFile = null;

            // Locking access to password file as we retrieve it
            lock (_passwordFileLocker) {

                // Retrieving password file
                pwdFile = GetPasswordFile(context);
            }

            // Looping through each user object in password file, retrieving all roles
            foreach (var idxUserNode in pwdFile["users"].Children) {

                // Checking if currently iterated user's role was already added
                if (args.Children.FirstOrDefault(ix => ix.Name == idxUserNode["role"].Get<string>(context)) == null) {

                    // Default Context role was not already added
                    args.Add(idxUserNode["role"].Get<string>(context));
                }
            }

            // Making sure default role is added
            string defaultRole = context.RaiseNative ("p5.security.get-default-context-role").Get<string> (context);
            if (!string.IsNullOrEmpty(defaultRole)) {

                // There exist a default role, checking if it's already added
                if (args.Children.FirstOrDefault(ix => ix.Name == defaultRole) == null) {

                    // Default Role was not already added, therefor we add it to return lambda node
                    args.Add(defaultRole);
                }
            }
        }

        /*
         * Changes password of "root" account, but only if existing root account's password 
         * is null. Used during setup of server
         */
        internal static void SetRootPassword (ApplicationContext context, Node args)
        {
            // Retrieving password given
            string password = args.GetExChildValue<string>("password", context);
            if (string.IsNullOrEmpty(password))
                throw new SecurityException("You cannot set the root password to empty");

            // Retrieving password file, locking access to it as we do, such that we can verify root account's password is null
            Node rootPwdNode = null;
            lock (_passwordFileLocker) {
                rootPwdNode = GetPasswordFile(context)["users"]["root"];
            }
            if (rootPwdNode["password"].Value != null)
                throw new SecurityException("Somebody tried to use installation Active event [p5.web.set-root-password] to change password of existing root account");

            // Temporary logging in root user now, before changing password, 
            // since passwords can only be changed for "currently logged in user"
            SetTicket (new ApplicationContext.ContextTicket("root", "root", false));
            context.UpdateTicket(GetTicket (context));
            try {

                // Changing password of "root"
                ChangePassword(context, password);
            } finally {

                // Making sure we log OUT root account again, before returning ...
                Logout (context);
            }
        }

        /*
         * Returns true if root account's password is null, which means that server is not setup yet
         */
        internal static bool RootPasswordIsNull (ApplicationContext context)
        {
            // Retrieving password file, and making sure we lock access to file as we do
            Node rootPwdNode = null;
            lock (_passwordFileLocker) {
                rootPwdNode = GetPasswordFile(context)["users"]["root"];
            }

            // Returning true if root account's password is null
            return rootPwdNode["password"].Value == null;
        }

        /*
         * Changes password of the currently logged in Context user account
         */
        internal static void ChangePassword (ApplicationContext context, string newPwd)
        {
            // Getting root folder of app, needed to save passwords file later
            string rootFolder = context.RaiseNative("p5.core.application-folder").Get<string>(context);

            // Locking access to password file
            lock (_passwordFileLocker) {

                // Retrieving password file
                Node pwdFile = GetPasswordFile(context);

                // Changing user's password
                pwdFile["users"][context.Ticket.Username]["password"].Value = newPwd;

                // Saving password file to disc
                using (TextWriter writer = File.CreateText(
                    context.RaiseNative ("p5.security.get-auth-file").Get<string> (context).Replace("~/", rootFolder))) {

                    // Creating Hyperlisp out of lambda password file
                    Node lambdaNode = new Node();
                    lambdaNode.AddRange(pwdFile.Children);
                    writer.Write(context.RaiseNative ("lambda2lisp", lambdaNode).Get<string> (context));
                }
            }
        }

        /*
         * Helper to retrieve "_passwords" file as lambda object
         */
        internal static Node GetPasswordFile (ApplicationContext context)
        {
            // Getting filepath to pwd file
            string rootFolder = context.RaiseNative("p5.core.application-folder").Get<string>(context);
            string pwdFilePath = context.RaiseNative ("p5.security.get-auth-file").Get<string>(context).Replace("~", rootFolder);

            // Checking file exist
            if (!File.Exists(pwdFilePath))
                CreateDefaultPasswordFile (context, pwdFilePath);

            // Reading up passwords file
            using (TextReader reader = new StreamReader(File.OpenRead(pwdFilePath))) {

                // Returning file as lambda
                string users = reader.ReadToEnd();
                Node usersNode = context.RaiseNative("lisp2lambda", new Node("", users));
                return usersNode;
            }
        }

        /*
         * Will try to login from persistent cookie
         */
        internal static bool TryLoginFromPersistentCookie(ApplicationContext context)
        {
            // Checking if we have any session associated with current invocation, which is NOT the case if this is the
            // ApplicationContext created during "application-startup"
            if (HttpContext.Current == null || HttpContext.Current.Session == null)
                return false; // Creation of ApplicationContext from [p5.core.application-start], before we have any session available - Ignoring ...

            try
            {

                // Checking if client has persistent cookie
                HttpCookie cookie = HttpContext.Current.Request.Cookies.Get(_credentialCookieName);
                if (cookie != null) {

                    // We have a cookie, try to use it as credentials
                    if (LoginFromCookie (cookie, context))
                        return true;
                }
            } 
            catch
            {

                // Making sure we delete cookie
                // We do not rethrow this, since reason might be because "salt" has changed, to explicitly log user
                // out, and that is actually not a "security issue", but a "feature". Besides, login-cooloff-seconds
                // will make sure "brute force" login through cookies are virtually impossible
                HttpCookie cookie = HttpContext.Current.Request.Cookies.Get(_credentialCookieName);
                cookie.Expires = DateTime.Now.AddDays(-1);
                HttpContext.Current.Response.Cookies.Add(cookie);
            }
            return false;
        }

        #region [ -- Private helper methods -- ]

        /*
         * Tries to login with the given cookie as credentials
         */
        private static bool LoginFromCookie (HttpCookie cookie, ApplicationContext context)
        {
            // Making sure nobody can reach us by brute force, by supplying a new 
            // cookie in a "brute force cookie login" attempt
            GuardAgainstBruteForce (context);

            // User has persistent cookie associated with client
            var cookieSplits = cookie.Value.Split (' ');
            if (cookieSplits.Length != 2)
                throw new SecurityException ("Cookie not accepted");

            string cookieUsername = cookieSplits[0];
            string cookieHashSaltedPwd = cookieSplits[1];
            Node pwdFile = null;

            // Locking access to password file
            lock (_passwordFileLocker) {
                pwdFile = GetPasswordFile(context);
            }

            // Checking if user exist
            Node userNode = pwdFile["users"][cookieUsername];
            if (userNode == null)
                throw new SecurityException ("Cookie not accepted");

            // User exists, retrieving salt and password to see if we have a match
            string salt = userNode["salt"].Get<string> (context);
            string password = userNode["password"].Get<string> (context);
            string hashSaltedPwd = context.RaiseNative("md5-hash", new Node("", salt + password)).Get<string>(context);

            // Notice, we do NOT THROW if passwords do not match, since it might simply mean that user has explicitly created a new "salt"
            // to throw out other clients that are currently persistently logged into system under his account
            if (hashSaltedPwd == cookieHashSaltedPwd) {

                // MATCH, discarding previous Context Ticket and creating a new Ticket
                SetTicket (new ApplicationContext.ContextTicket(
                    userNode.Name, 
                    userNode ["role"].Get<string>(context), 
                    false));
                LastLoginAttemptForIP = DateTime.MinValue;
                context.UpdateTicket (AuthenticationHelper.GetTicket (context));
                return true;
            }
            return false;
        }

        /*
         * Creates default Context Ticket according to settings from config file
         */
        private static ApplicationContext.ContextTicket CreateDefaultTicket (ApplicationContext context)
        {
            return new ApplicationContext.ContextTicket (
                context.RaiseNative ("p5.security.get-default-context-username").Get<string> (context), 
                context.RaiseNative ("p5.security.get-default-context-role").Get<string> (context), 
                true);
        }

        /*
         * Creates a new "salt" for use with hashing of passwords of random size. Salt is randomly created as concatenated GUIDs
         * between 128 and 640 Bits long
         */
        private static string CreateNewSalt()
        {
            var salt = "";
            for (var idxRndNo = 0; idxRndNo < new Random (DateTime.Now.Millisecond).Next (1,5); idxRndNo++) {
                salt += Guid.NewGuid().ToString();
            }
            return salt;
        }

        /*
         * Creates a default authentication/authorization file, and a default "root" user, with a "null" password
         */
        private static void CreateDefaultPasswordFile (ApplicationContext context, string pwdFile)
        {
            // Creates a default authentication/authorization file
            using (TextWriter writer = File.CreateText(pwdFile)) {

                // Creating default root password, salt unique to user, and writing to file
                var salt = CreateNewSalt ();
                salt = salt.Replace("-", "");
                writer.WriteLine(@"users");
                writer.WriteLine(@"  root");
                writer.WriteLine(@"    salt:" + salt);
                writer.WriteLine(@"    password");
                writer.WriteLine(@"    role:root");
            }
        }

        /*
         * Helper to guard against brute force login attempt. Basically denies an IP address to attempt to login without having
         * to wait a configurable amount of seconds between each attempt
         */
        private static void GuardAgainstBruteForce(ApplicationContext context)
        {
            TimeSpan span = DateTime.Now - LastLoginAttemptForIP;

            // Verifying delta is lower than threshold accepted
            int seconds = context.RaiseNative ("p5.security.get-login-cooloff-seconds").Get<int> (context);
            if (span.TotalSeconds < seconds)
                throw new SecurityException (
                    string.Format (
                        "Your IP address is trying to login to frequently, please wait {0} seconds before trying again.", 
                        seconds));

            // Making sure we set the last login attempt to now!
            LastLoginAttemptForIP = DateTime.Now;
        }

        /*
         * Helper to store "last login attempt" for a specific IP address
         */
        private static DateTime LastLoginAttemptForIP
        {
            get {

                // Retrieving Client's IP address, to use as lookup for last login attempt
                string clientIP = HttpContext.Current.Request.UserHostAddress;

                // Checking application object if we have a previous login attempt for given IP
                if (HttpContext.Current.Application ["_last-login-attempt-" + clientIP] != null)
                    return (DateTime)HttpContext.Current.Application ["_last-login-attempt-" + clientIP];

                // No previous login attempt on record, returning DateTime.MinValue
                return DateTime.MinValue;
            }
            set {

                // Retrieving Client's IP address, to use as lookup for last login attempt
                string clientIP = HttpContext.Current.Request.UserHostAddress;

                // Checking if this is a "reset login attempts"
                if (value == DateTime.MinValue)
                    HttpContext.Current.Application.Remove ("_last-login-attempt-" + clientIP);
                else
                    HttpContext.Current.Application ["_last-login-attempt-" + clientIP] = value;
            }
        }

        #endregion
    }
}