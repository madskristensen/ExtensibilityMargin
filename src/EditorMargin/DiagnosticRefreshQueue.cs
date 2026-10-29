using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace ExtensibilityMargin
{
    internal sealed class DiagnosticRefreshQueue : IDisposable
    {
        internal const int Capacity = 64;
        private readonly object _gate = new object();
        private readonly List<SnapshotSpan> _classifications = new List<SnapshotSpan>();
        private readonly List<IMappingSpan> _tags = new List<IMappingSpan>();
        private bool _active, _disposed, _scheduled, _force, _buffersDirty;

        public void SetActive(bool active)
        {
            lock (_gate)
            {
                _active = active && !_disposed;
                if (!_active)
                {
                    Clear();
                }
            }
        }

        public bool Request(SnapshotSpan? classification = null, IMappingSpan tags = null, bool force = false, bool buffersDirty = false)
        {
            lock (_gate)
            {
                if (!_active)
                {
                    return false;
                }
                _buffersDirty |= buffersDirty;
                if (force || _classifications.Count + _tags.Count >= Capacity)
                {
                    // A burst becomes one full refresh, never an unbounded snapshot backlog.
                    ClearSpans();
                    _force = true;
                }
                if (!_force)
                {
                    if (classification.HasValue)
                    {
                        _classifications.Add(classification.Value);
                    }
                    if (tags != null)
                    {
                        _tags.Add(tags);
                    }
                }
                bool schedule = !_scheduled;
                _scheduled = true;
                return schedule;
            }
        }

        public Batch Drain()
        {
            lock (_gate)
            {
                _scheduled = false;
                var batch = new Batch(_classifications.ToArray(), _tags.ToArray(), _force, _buffersDirty);
                Clear();
                return batch;
            }
        }

        private void ClearSpans()
        {
            _classifications.Clear();
            _tags.Clear();
        }

        private void Clear()
        {
            ClearSpans();
            _force = false;
            _buffersDirty = false;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _active = false;
                Clear();
            }
        }

        internal sealed class Batch
        {
            public Batch(SnapshotSpan[] classifications, IMappingSpan[] tags, bool force, bool buffersDirty)
            {
                Classifications = classifications;
                Tags = tags;
                Force = force;
                BuffersDirty = buffersDirty;
            }

            public SnapshotSpan[] Classifications { get; }
            public IMappingSpan[] Tags { get; }
            public bool Force { get; }
            public bool BuffersDirty { get; }
        }
    }
}