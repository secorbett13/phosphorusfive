/*
 * Phosphorus Five, copyright 2014 - 2015, Thomas Hansen, phosphorusfive@gmail.com
 * Phosphorus Five is licensed under the terms of the MIT license, see the enclosed LICENSE file for details
 */

using System;
using System.Linq;
using System.Collections.Generic;
using p5.core;

namespace p5.exp.iterators
{
    /// <summary>
    ///     Returns the "next node"
    /// </summary>
    [Serializable]
    public class IteratorShiftRight : Iterator
    {
        public override IEnumerable<Node> Evaluate (ApplicationContext context)
        {
            return Left.Evaluate (context).Select (idxCurrent => idxCurrent.NextNode).Where (next => next != null);
        }
    }
}
