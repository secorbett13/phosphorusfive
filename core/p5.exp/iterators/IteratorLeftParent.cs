/*
 * Phosphorus Five, copyright 2014 - 2015, Thomas Hansen, phosphorusfive@gmail.com
 * Phosphorus Five is licensed under the terms of the MIT license, see the enclosed LICENSE file for details
 */

using System;
using System.Collections.Generic;
using p5.core;

namespace p5.exp.iterators
{
    /// <summary>
    ///     Root iterators for nested IteratorGroup iterators
    /// </summary>
    [Serializable]
    public class IteratorLeftParent : Iterator
    {
        private readonly Iterator _leftParent;

        /// <summary>
        ///     Initializes a new instance of the <see cref="phosphorus.expressions.iterators.IteratorLeftParent" /> class
        /// </summary>
        /// <param name="leftParent">The last iterator of the parent group iterator</param>
        public IteratorLeftParent (Iterator leftParent)
        {
            _leftParent = leftParent;
        }

        public override IEnumerable<Node> Evaluate (ApplicationContext context)
        {
            return _leftParent.Evaluate (context);
        }
    }
}
