using System;
using Microsoft.VisualStudio.Text;

namespace ExtensibilityMargin
{
    internal static class DiagnosticChangeFilter
    {
        public static bool ContainsCaret(SnapshotSpan span, int caretPosition)
        {
            int position = Math.Max(0, Math.Min(caretPosition, span.Snapshot.Length - 1));
            return span.Contains(position) || (span.Length == 0 && span.Start.Position == position);
        }

        public static bool ContainsCaret(IMappingSpan change, ITextSnapshot snapshot, int caretPosition)
        {
            foreach (SnapshotSpan span in change.GetSpans(snapshot))
            {
                if (ContainsCaret(span, caretPosition))
                {
                    return true;
                }
            }
            return false;
        }
    }
}