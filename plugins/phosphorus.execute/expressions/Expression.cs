/*
 * phosphorus five, copyright 2014 - Mother Earth, Jannah, Gaia
 * phosphorus five is licensed as mit, see the enclosed LICENSE file for details
 */

using System;
using System.Globalization;
using System.Collections.Generic;
using phosphorus.core;
using phosphorus.execute.iterators;

namespace phosphorus.execute
{
    /// <summary>
    /// expression class, for retrieving and changing values in node tree according to execution expressions
    /// </summary>
    public class Expression
    {
        private string _expression;

        /// <summary>
        /// initializes a new instance of the <see cref="phosphorus.execute.Expression"/> class
        /// </summary>
        /// <param name="expression">execution engine expression</param>
        public Expression (string expression)
        {
            if (!IsExpression (expression))
                throw new ArgumentException (string.Format ("'{0}' is not a valid expression", expression));
            _expression = expression;
        }

        /// <summary>
        /// determines if string is an expression or not
        /// </summary>
        /// <returns><c>true</c> if string is an expression; otherwise, <c>false</c>.</returns>
        /// <param name="value">string to check</param>
        public static bool IsExpression (string value)
        {
            return value != null && 
                value.StartsWith ("@") && 
                !value.StartsWith (@"@""") && 
                value.Length > 1;
        }

        /// <summary>
        /// returns formatted node according to children nodes
        /// </summary>
        /// <returns>the formatted string expression</returns>
        /// <param name="node">node to format</param>
        public static string FormatNode (Node node)
        {
            string retVal = null;
            if (node.Count > 0) {
                List<string> childrenValues = new List<string> ();
                foreach (Node idxNode in node.Children) {
                    string value = idxNode.Count == 0 ? 
                        idxNode.Get<string> () : // simple value
                        FormatNode (idxNode); // recursive formatting string literal
                    if (IsExpression (value))
                        value = new Expression (value).Evaluate (idxNode).GetValue (0, string.Empty);
                    childrenValues.Add (value);
                }
                retVal = string.Format (CultureInfo.InvariantCulture, retVal, childrenValues.ToArray ());
            } else {
                retVal = node.Get<string> ();
            }
            return retVal;
        }

        /// <summary>
        /// evaluates expression for given <see cref="phosphorus.core.Node"/>  and returns <see cref="phosphorus.execute.Expression.Match"/>
        /// </summary>
        public Match Evaluate (Node node)
        {
            IteratorGroup current = new IteratorGroup (node, null);
            string typeOfExpression = null;
            bool seenQuestionMark = false;
            string previousToken = null;
            foreach (string idxToken in TokenizeExpression (_expression)) {
                if (seenQuestionMark) {
                    typeOfExpression = idxToken;
                } else if (idxToken == "?") {
                    seenQuestionMark = true;
                } else {
                    current = FindMatches (current, idxToken, previousToken, node);
                }
                previousToken = idxToken;
            }

            if (current.ParentGroup != null)
                throw new ArgumentException ("unclosed group while evaluating; " + _expression);

            // returning match object
            return new Match (current, typeOfExpression);
        }
        
        /*
         * return matches according to token
         */
        private IteratorGroup FindMatches (IteratorGroup current, string token, string previousToken, Node node)
        {
            switch (token) {
            case "(":
                current = new IteratorGroup (node, current);
                break;
            case ")":
                current = current.ParentGroup;
                break;
            case "/":
                if (current.LastIterator.Left == null) {
                    // this is first real token in this group, hence we return "root" match
                    current.AddIterator (new IteratorRoot ());
                } else if (previousToken == "/") {
                    // two slashes "//" preceded each other, hence we're looking for a named value, where its name is string.Empty
                    current.AddIterator (new IteratorNamed (string.Empty));
                }
                break;
            case "*":
                current.AddIterator (new IteratorChildren ());
                break;
            case "**":
                current.AddIterator (new IteratorDescendants ());
                break;
            case ".":
                current.AddIterator (new IteratorParents ());
                break;
            case "|":
                current.AddLogical (node, new Logical (Logical.LogicalType.OR));
                break;
            case "&":
                current.AddLogical (node, new Logical (Logical.LogicalType.AND));
                break;
            case "!":
                current.AddLogical (node, new Logical (Logical.LogicalType.NOT));
                break;
            case "^":
                current.AddLogical (node, new Logical (Logical.LogicalType.XOR));
                break;
            case "=":
                current.AddIterator (new IteratorValued ()); // actual value will be set in next token
                break;
            default:
                if (current.Left is IteratorValued) {
                    // looking for value, current token is value to look for, changing Value of current MatchIterator
                    ((IteratorValued)current.LastIterator).Value = token;
                } else {
                    // looking for name
                    if (IsAllNumbers (token)) {
                        current.AddIterator (new IteratorNumbered (int.Parse (token)));
                    } else {
                        current.AddIterator (new IteratorNamed (token));
                    }
                }
                break;
            }
            return current;
        }
        
        /*
         * responsible for tokenizing expression
         */
        private static IEnumerable<string> TokenizeExpression (string expression)
        {
            string buffer = string.Empty;
            for (int idxNo = 1 /* skipping first @ character */; idxNo < expression.Length; idxNo++) {
                char idxChar = expression [idxNo];
                if (@"/\.|&!^()=?".IndexOf (idxChar) > -1) {
                    if (buffer != string.Empty) {
                        yield return buffer;
                        buffer = string.Empty;
                    }
                } else if (@"""@".IndexOf (idxChar) > -1) {
                    if (buffer != string.Empty) {
                        yield return buffer;
                        buffer = string.Empty;
                    }
                    yield return Utilities.GetStringToken (expression, ref idxNo);
                    idxNo -= 1;
                } else {
                    buffer += idxChar;
                }
            }
            if (buffer != string.Empty)
                yield return buffer;
        }

        /*
         * returns true if string contains nothing but numbers
         */
        private bool IsAllNumbers (string token)
        {
            foreach (char idx in token) {
                if ("0123456789".IndexOf (idx) == -1)
                    return false;
            }
            return true;
        }
    }
}
