using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace ExtensibilityMargin
{
    internal static class DiagnosticDetails
    {
        public static string ContentTypeSummary(IContentType contentType, string viewType, string documentType, IEnumerable<string> roles)
        {
            var text = new StringBuilder("Content type: ").AppendLine(contentType.TypeName);
            text.Append("Identifies which editor extensions apply here.");

            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<IContentType>();
            var pending = new Stack<IContentType>();
            pending.Push(contentType);
            while (pending.Count != 0)
            {
                IContentType current = pending.Pop();
                if (!visited.Add(current))
                {
                    continue;
                }
                if (!string.Equals(current.TypeName, contentType.TypeName, StringComparison.OrdinalIgnoreCase))
                {
                    names.Add(current.TypeName);
                }
                foreach (IContentType parent in current.BaseTypes)
                {
                    pending.Push(parent);
                }
            }
            if (names.Count != 0)
            {
                text.AppendLine().AppendLine().AppendLine("Also matches (base types)");
                text.Append(string.Join(", ", names));
            }

            if (!string.Equals(viewType, contentType.TypeName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(documentType, contentType.TypeName, StringComparison.OrdinalIgnoreCase))
            {
                text.AppendLine().AppendLine().AppendLine("Buffer context");
                text.Append("View: ").AppendLine(viewType).Append("Document: ").Append(documentType);
            }

            string[] roleNames = roles.Distinct(StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal).ToArray();
            if (roleNames.Length != 0)
            {
                text.AppendLine().AppendLine().AppendLine("Text view roles");
                text.AppendLine("Capabilities used by role-based editor extensions.");
                text.Append(string.Join(", ", roleNames));
            }
            return text.ToString();
        }

        public static string ContentType(IContentType contentType, string viewType, string documentType, IEnumerable<string> roles)
        {
            var text = new StringBuilder("Content type: ");
            AppendHierarchy(text, contentType, t => t.TypeName, t => t.BaseTypes, new HashSet<IContentType>(), 0);
            text.AppendLine().Append("View content type: ").AppendLine(viewType);
            text.Append("Document content type: ").AppendLine(documentType);
            text.Append("Text view roles:");
            foreach (string role in roles.OrderBy(r => r, StringComparer.Ordinal))
            {
                text.AppendLine().Append("  ").Append(role);
            }
            return text.ToString().TrimEnd();
        }

        public static string Classifications(IEnumerable<IClassificationType> types)
        {
            var text = new StringBuilder("Classifications at caret:");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (IClassificationType type in types)
            {
                if (names.Add(type.Classification))
                {
                    text.AppendLine();
                    AppendHierarchy(text, type, t => t.Classification, t => t.BaseTypes, new HashSet<IClassificationType>(), 1);
                }
            }
            if (names.Count == 0)
            {
                text.AppendLine().Append("  None");
            }
            return text.ToString().TrimEnd();
        }

        private static void AppendHierarchy<T>(StringBuilder text, T item, Func<T, string> name, Func<T, IEnumerable<T>> baseTypes, HashSet<T> path, int depth)
        {
            text.Append(' ', depth * 2).Append(name(item));
            if (!path.Add(item))
            {
                text.Append(" (cycle)");
                return;
            }
            foreach (T parent in baseTypes(item).OrderBy(name, StringComparer.Ordinal))
            {
                text.AppendLine();
                AppendHierarchy(text, parent, name, baseTypes, path, depth + 1);
            }
            path.Remove(item);
        }
    }
}
