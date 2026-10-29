using Microsoft.VisualStudio.Settings;

namespace ExtensibilityMargin
{
    internal sealed class MarginVisibilitySettings
    {
        internal const string Collection = "ExtensibilityMargin";
        internal const string Property = "Enabled";
        private readonly WritableSettingsStore _store;

        public MarginVisibilitySettings(WritableSettingsStore store)
        {
            _store = store;
        }

        public bool Load() => _store.CollectionExists(Collection) && _store.GetBoolean(Collection, Property, false);

        public void Save(bool enabled)
        {
            if (!_store.CollectionExists(Collection))
            {
                _store.CreateCollection(Collection);
            }
            _store.SetBoolean(Collection, Property, enabled);
        }
    }
}
