/*
 * phosphorus five, copyright 2014 - Mother Earth, Jannah, Gaia
 * phosphorus five is licensed as mit, see the enclosed LICENSE file for details
 */

using System;
using System.Collections.Generic;
using phosphorus.core;

namespace phosphorus.lambda.iterators
{
    /// <summary>
    /// "stop iterator" which is useful for using as "root iterators" of children <see cref="phosphorus.execute.iterators.IteratorGroup"/>
    /// iterators
    /// </summary>
    public class IteratorLeftParent : Iterator
    {
        private Iterator _leftParent;

        /// <summary>
        /// initializes a new instance of the <see cref="phosphorus.execute.iterators.IteratorLeftParent"/> class.
        /// </summary>
        /// <param name="leftParent">the last iterator of the parent group iterator</param>
        public IteratorLeftParent (Iterator leftParent)
        {
            _leftParent = leftParent;
        }

        public override IEnumerable<Node> Evaluate {
            get {
                return _leftParent.Evaluate;
            }
        }
    }
}
