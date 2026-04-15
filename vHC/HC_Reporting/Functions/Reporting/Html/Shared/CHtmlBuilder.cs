// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Text;

namespace VeeamHealthCheck.Functions.Reporting.Html.Shared
{
    /// <summary>
    /// A StringBuilder-backed fluent HTML builder that enforces correct tag nesting.
    /// Uses a stack to track open tags and prevent malformed HTML.
    /// </summary>
    internal class CHtmlBuilder
    {
        private readonly StringBuilder _sb = new();
        private readonly Stack<string> _tagStack = new();

        /// <summary>
        /// Opens an HTML tag with optional attributes.
        /// </summary>
        public CHtmlBuilder OpenTag(string tag, params (string attr, string val)[] attrs)
        {
            _sb.Append('<');
            _sb.Append(tag);
            foreach (var (attr, val) in attrs)
            {
                _sb.Append(' ');
                _sb.Append(attr);
                _sb.Append("=\"");
                _sb.Append(HtmlEncode(val));
                _sb.Append('"');
            }
            _sb.Append('>');
            _tagStack.Push(tag);
            return this;
        }

        /// <summary>
        /// Closes the most recently opened tag. Validates it matches the expected tag name.
        /// </summary>
        public CHtmlBuilder CloseTag(string tag)
        {
            if (_tagStack.Count == 0)
                throw new InvalidOperationException($"Cannot close tag '{tag}': no open tags on the stack.");

            string expected = _tagStack.Pop();
            if (!string.Equals(expected, tag, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Tag mismatch: expected to close '{expected}' but got '{tag}'.");

            _sb.Append("</");
            _sb.Append(tag);
            _sb.Append('>');
            return this;
        }

        /// <summary>
        /// Emits a self-closing tag (e.g., br, img, hr).
        /// </summary>
        public CHtmlBuilder SelfClose(string tag, params (string attr, string val)[] attrs)
        {
            _sb.Append('<');
            _sb.Append(tag);
            foreach (var (attr, val) in attrs)
            {
                _sb.Append(' ');
                _sb.Append(attr);
                _sb.Append("=\"");
                _sb.Append(HtmlEncode(val));
                _sb.Append('"');
            }
            _sb.Append(" />");
            return this;
        }

        /// <summary>
        /// Appends text content, HTML-encoding it for safety.
        /// </summary>
        public CHtmlBuilder Text(string content)
        {
            _sb.Append(HtmlEncode(content ?? string.Empty));
            return this;
        }

        /// <summary>
        /// Appends raw HTML without encoding. Use for legacy compatibility only.
        /// </summary>
        public CHtmlBuilder Raw(string html)
        {
            _sb.Append(html ?? string.Empty);
            return this;
        }

        /// <summary>
        /// Returns true if all opened tags have been closed.
        /// </summary>
        public bool IsBalanced => _tagStack.Count == 0;

        /// <summary>
        /// Returns the number of unclosed tags.
        /// </summary>
        public int OpenTagCount => _tagStack.Count;

        /// <summary>
        /// Returns the built HTML string. Throws if tags are unbalanced.
        /// </summary>
        public string Build()
        {
            if (_tagStack.Count > 0)
                throw new InvalidOperationException(
                    $"Cannot build: {_tagStack.Count} unclosed tag(s). Innermost unclosed: '{_tagStack.Peek()}'.");

            return _sb.ToString();
        }

        /// <summary>
        /// Returns the built HTML string without balance validation.
        /// Useful for building fragments that will be composed elsewhere.
        /// </summary>
        public string BuildFragment()
        {
            return _sb.ToString();
        }

        /// <summary>
        /// Minimal HTML encoding for attribute values and text content.
        /// </summary>
        private static string HtmlEncode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}
