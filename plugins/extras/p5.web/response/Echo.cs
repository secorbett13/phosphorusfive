/*
 * Phosphorus Five, copyright 2014 - 2016, Thomas Hansen, phosphorusfive@gmail.com
 * Phosphorus Five is licensed under the terms of the MIT license, see the enclosed LICENSE file for details.
 */

using System.IO;
using System.Web;
using p5.exp;
using p5.core;

namespace p5.web.ui.response {
    /// <summary>
    ///     Class encapsulating the [p5.web.response.echo] Active Event
    /// </summary>
    public static class Echo
    {
        /// <summary>
        ///     Echo content back to client
        /// </summary>
        /// <param name="context">Application Context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "echo", Protection = EventProtection.LambdaClosed)]
        public static void echo (ApplicationContext context, ActiveEventArgs e)
        {
            // Discarding current response, and removing session cookie, unless caller explicitly said he wanted to keep it
            HttpContext.Current.Response.Filter = null;
            HttpContext.Current.Response.ClearContent ();

            // Rendering content back on wire
            byte[] val = e.Args.Value as byte[];
            if (val != null) {

                // Content is binary type of content
                HttpContext.Current.Response.BinaryWrite (val);
            } else {

                // Content is string, integer, etc type of content
                HttpContext.Current.Response.Write (XUtil.Single<string> (context, e.Args));
            }

            // Flushing response, and making sure default content is never rendered
            HttpContext.Current.Response.OutputStream.Flush ();
            HttpContext.Current.Response.Flush ();
            HttpContext.Current.Response.SuppressContent = true;
        }

        /// <summary>
        ///     Echo file back to client
        /// </summary>
        /// <param name="context">Application Context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "echo-file", Protection = EventProtection.LambdaClosed)]
        public static void echo_file (ApplicationContext context, ActiveEventArgs e)
        {
            // Discarding current response, and removing session cookie, unless caller explicitly said he wanted to keep it
            HttpContext.Current.Response.Filter = null;
            HttpContext.Current.Response.ClearContent ();

            // Retrieving root node of web application
            var rootFolder = context.RaiseNative ("p5.core.application-folder").Get<string> (context);

            // Finding filename
            var fileName = XUtil.Single<string> (context, e.Args);

            // Verifying user is authorized to reading from currently iterated file
            context.RaiseNative ("p5.io.authorize.read-file", new Node ("", fileName).Add ("args", e.Args));

            // Rendering file back to client
            using (Stream fileStream = File.OpenRead (rootFolder + fileName)) {
                fileStream.CopyTo (HttpContext.Current.Response.OutputStream);
            }

            // Flushing response, and making sure default content is never rendered
            HttpContext.Current.Response.OutputStream.Flush ();
            HttpContext.Current.Response.Flush ();
            HttpContext.Current.Response.SuppressContent = true;
        }
    }
}