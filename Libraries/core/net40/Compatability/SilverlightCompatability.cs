/*

Copyright dotNetRDF Project 2009-12
dotnetrdf-develop@lists.sf.net

------------------------------------------------------------------------

This file is part of dotNetRDF.

dotNetRDF is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

dotNetRDF is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with dotNetRDF.  If not, see <http://www.gnu.org/licenses/>.

------------------------------------------------------------------------

dotNetRDF may alternatively be used under the LGPL or MIT License

http://www.gnu.org/licenses/lgpl.html
http://www.opensource.org/licenses/mit-license.php

If these licenses are not suitable for your intended use please contact
us at the above stated email address to discuss alternative
terms.

*/

#if SILVERLIGHT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using HtmlAgilityPack;
using VDS.RDF.Parsing;
using VDS.RDF.Query;

namespace VDS.RDF
{

    /// <summary>
    /// Extension Methods for the Silverlight build to avoid having to do lots of #if SILVERLIGHT blocks
    /// </summary>
    public static class SilverlightExtensions
    {
        /// <summary>
        /// Splits a String
        /// </summary>
        /// <param name="value">String to split</param>
        /// <param name="chars">Separator</param>
        /// <param name="count">Maximum number of results</param>
        /// <returns></returns>
        public static string[] Split(this string value, char[] chars, int count)
        {
            String[] items = value.Split(chars);
            return items.Length > count ? items.Take(count-1).Concat(String.Join(new String(chars), items, count, items.Length-(count-1)).AsEnumerable()).ToArray() : items;
        }

        /// <summary>
        /// Copies the characters in a specified substring to a character array
        /// </summary>
        /// <param name="value">String</param>
        /// <param name="startIndex">Start Index</param>
        /// <param name="count">Number of characters to copy</param>
        /// <returns></returns>
        public static char[] ToCharArray(this string value, int startIndex, int count)
        {
            char[] result = new char[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = value[startIndex + i];
            }
            return result;
        }

        /// <summary>
        /// Gets whether a URI is a file:/// URI
        /// </summary>
        /// <param name="u">URI</param>
        /// <returns></returns>
        public static bool IsFile(this Uri u)
        {
            return u.Scheme.Equals(Uri.UriSchemeFile);
        }

        /// <summary>
        /// Gets the Segments of the URI
        /// </summary>
        /// <param name="u">URI</param>
        /// <returns></returns>
        public static String[] Segments(this Uri u)
        {
            String path = u.AbsolutePath;
            if (path.Equals("/"))
            {
                return new String[] { "/" };
            }
            else
            {
                String[] segments = path.Split('/');
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    segments[i] += "/";
                }
                return segments;
            }
        }

        /// <summary>
        /// Gets the Path and Query of the URI
        /// </summary>
        /// <param name="u">URI</param>
        /// <returns></returns>
        public static String PathAndQuery(this Uri u)
        {
            return u.AbsolutePath + u.Query;
        }

        public static bool IsHexEncoding(String value, int index)
        {
            if (index + 2 >= value.Length) return false;
            return value[0] == '%' && SparqlSpecsHelper.IsHex(value[1]) && SparqlSpecsHelper.IsHex(value[2]);
        }

        public static char HexUnescape(String value, ref int index)
        {
            if (index + 2 >= value.Length) throw new RdfParseException("Malformed Percent Encoded Escape");
            if (value[index] != '%') throw new RdfParseException("Malformed Percent Encoded Escape");
            index = index + 3;
            return UnicodeSpecsHelper.ConvertToChar(value.Substring(index + 1, 2));
        }

        /// <summary>
        /// Removes all items from a List which return true for the given function
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="l">List</param>
        /// <param name="func">Function</param>
        public static void RemoveAll<T>(this List<T> l, Func<T,bool> func)
        {
            for (int i = 0; i < l.Count; i++)
            {
                if (func(l[i]))
                {
                    l.RemoveAt(i);
                    i--;
                }
            }
        }

        public static HtmlNode SelectSingleNode(this HtmlNode node, String xpath)
        {
            if (!xpath.ToCharArray().All(c => Char.IsLetterOrDigit(c) || c == '/') || xpath.Contains("//"))
            {
                throw new NotSupportedException("Only simple XPath expressions needed by dotNetRDF code are supported by this method");
            }

            if (xpath.StartsWith("/")) xpath = xpath.Substring(1);

            if (xpath.Contains("/"))
            {
                String firstPart = xpath.Substring(0, xpath.IndexOf('/'));
                String rest = xpath.Substring(firstPart.Length);

                foreach (HtmlNode n in node.ChildNodes)
                {
                    if (n.Name.Equals(firstPart))
                    {
                        HtmlNode temp = n.SelectSingleNode(rest);
                        if (temp != null) return temp;
                    }
                }
            }
            else
            {
                foreach (HtmlNode n in node.ChildNodes)
                {
                    if (n.Name.Equals(xpath)) return n;
                }
            }
            return null;
        }

        public static HtmlNodeCollection SelectNodes(this HtmlNode node, String xpath)
        {
            HtmlNodeCollection results = new HtmlNodeCollection(node);
            if (xpath.Equals("comment()"))
            {
                foreach (HtmlNode n in node.ChildNodes)
                {
                    if (n.NodeType == HtmlNodeType.Comment) results.Add(n);
                }
            }
            else
            {
                throw new NotSupportedException("Only the XPath expressions required by dotNetRDF code are supported by this method");
            }

            return results;
        }

        public static int FindIndex<T>(this List<T> list, Predicate<T> match)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (match(list[i])) return i;
            }
            return -1;
        }
    }
}

#endif