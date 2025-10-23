// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Prefs.Tests
{
    using System.Collections.Generic;

    /// <summary>
    /// In-memory implementation of <see cref="IPrefsImplementation"/> for testing purposes.
    /// </summary>
    public sealed class FakePrefs : IPrefsImplementation
    {
        private readonly Dictionary<string, object> values = new();

        public void Save() { }

        public void DeleteAll()
        {
            if (values.Count is 0)
            {
                return;
            }

            values.Clear();
        }

        public void DeleteKey(string key) => values.Remove(key);
        public void SetString(string key, string value) => values[key] = value;
        public bool HasKey(string key) => values.ContainsKey(key);
        public string GetString(string key) => GetString(key, default);
        public string GetString(string key, string defaultValue) => values.TryGetValue(key, out var value) && value is string stringValue ? stringValue : defaultValue;
        public void SetBool(string key, bool value) => values[key] = value;
        public bool GetBool(string key, bool defaultValue) => values.TryGetValue(key, out var value) && value is bool boolValue ? boolValue : defaultValue;
        public void SetFloat(string key, float value) => values[key] = value;
        public float GetFloat(string key) => GetFloat(key, default);
        public float GetFloat(string key, float defaultValue) => values.TryGetValue(key, out var value) && value is float floatValue ? floatValue : defaultValue;
        public void SetInt(string key, int value) => values[key] = value;
        public int GetInt(string key) => GetInt(key, default);
        public int GetInt(string key, int defaultValue) => values.TryGetValue(key, out var value) && value is int intValue ? intValue : defaultValue;
    }
}
