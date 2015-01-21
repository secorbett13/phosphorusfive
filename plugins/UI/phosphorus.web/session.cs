
/*
 * phosphorus five, copyright 2014 - Mother Earth, Jannah, Gaia
 * phosphorus five is licensed as mitx11, see the enclosed LICENSE file for details
 */

using System;
using System.Web;
using System.Text;
using System.Web.UI;
using System.Globalization;
using System.Security.Cryptography;
using phosphorus.core;
using phosphorus.lambda;
using phosphorus.ajax.widgets;

namespace phosphorus.web
{
    /// <summary>
    /// helper to retrieve and set session values
    /// </summary>
    public static class session
    {
        /// <summary>
        /// sets the given session key to the nodes given as children of [pf.set-session]. if no nodes are given,
        /// the session with the given key is cleared
        /// </summary>
        /// <param name="context"><see cref="phosphorus.Core.ApplicationContext"/> for Active Event</param>
        /// <param name="e">parameters passed into Active Event</param>
        [ActiveEvent (Name = "pf.web.session.set")]
        private static void pf_web_session_set (ApplicationContext context, ActiveEventArgs e)
        {
            string sessionKey = e.Args.Get<string> ();
            if (e.Args.Count > 0)
                HttpContext.Current.Session [sessionKey] = e.Args.Clone ();
            else
                HttpContext.Current.Session.Remove (sessionKey);
        }

        /// <summary>
        /// returns the session object given through the value of [pf.get-session] as a node
        /// </summary>
        /// <param name="context"><see cref="phosphorus.Core.ApplicationContext"/> for Active Event</param>
        /// <param name="e">parameters passed into Active Event</param>
        [ActiveEvent (Name = "pf.web.session.get")]
        private static void pf_web_session_get (ApplicationContext context, ActiveEventArgs e)
        {
            string sessionKey = e.Args.Get<string> ();
            object tmp = HttpContext.Current.Session [sessionKey];
            if (tmp != null)
                e.Args.AddRange ((tmp as Node).Clone ().Children);
        }
    }
}
