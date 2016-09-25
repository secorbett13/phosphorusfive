/*
 * Phosphorus Five, copyright 2014 - 2016, Thomas Hansen, phosphorusfive@gmail.com
 * Phosphorus Five is licensed under the terms of the MIT license, see the enclosed LICENSE file for details
 */

using System.Linq;
using p5.exp;
using p5.core;
using p5.exp.exceptions;

namespace p5.lambda.keywords.core
{
    /// <summary>
    ///     Class wrapping execution engine keyword [insert-before] and [insert-after]
    /// </summary>
    public static class Insert
    {
        /// <summary>
        ///     The [insert-before] keyword allows you to insert nodes before another node
        /// </summary>
        /// <param name="context">Application Context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "insert-before", Protection = EventProtection.LambdaClosed)]
        public static void lambda_insert_before (ApplicationContext context, ActiveEventArgs e)
        {
            InsertNodes (e.Args, context, false);
        }
        
        /// <summary>
        ///     The [insert-after] keyword allows you to insert nodes after another node
        /// </summary>
        /// <param name="context">Application Context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "insert-after", Protection = EventProtection.LambdaClosed)]
        public static void lambda_insert_after (ApplicationContext context, ActiveEventArgs e)
        {
            InsertNodes (e.Args, context, true);
        }

        /*
         * Commonalities for both Active Events
         */
        private static void InsertNodes (Node args, ApplicationContext context, bool after)
        {
            var destEx = args.Value as Expression;
            if (destEx == null)
                throw new LambdaException ("Not a valid destination expression given, make sure you set [" + args.Name + "]'s value to an expression using :x:", args, context);

            // Updating destination(s) with value of source
            foreach (var idxDestination in destEx.Evaluate (context, args, args)) {

                // Raising source Active Event
                var src = XUtil.InvokeSource (context, args, idxDestination.Node);

                // Inseting source nodes to destination
                var tmpNodeIdxDest = idxDestination.Value as Node;
                if (tmpNodeIdxDest == null)
                    throw new LambdaException ("Destination for [" + args.Name + "] was not a node", args, context);

                // Figuring out insertion point
                var index = idxDestination.Node.Parent.IndexOf (idxDestination.Node) + (after ? 1 : 0);

                // Iterating each source, inserting into currently iterated destination
                foreach (var idxSource in src) {

                    // Checking if currently iterated source is a node, and if not, we convert it into a node, which will
                    // result in a "root node", which we remove, and only insert its children, into currently iterated destination
                    if (idxSource is Node) {

                        // Currently iterated source is already a node
                        tmpNodeIdxDest.Parent.Insert (index++, (idxSource as Node).Clone ());
                    } else {

                        // Currently iterated source is not a node, hence we convert it into such, which creates a "wrapper" node, which
                        // we ignore, and only add its children. Making sure we "extract the list of insertion nodes" first, since
                        // inserting the nodex, will change the Children collection, untying the currently iterated source node, changing
                        // the collection
                        var list = Utilities.Convert<Node> (context, idxSource).Children.ToList ();
                        foreach (var idxSourceInner in list) {
                            tmpNodeIdxDest.Parent.Insert (index++, idxSourceInner);
                        }
                    }
                }
            }
        }
    }
}