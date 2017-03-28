using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace Music
{
    public class LocalStorage
    {
        private readonly string directory;
        private readonly Dictionary<string, string> storage = new Dictionary<string, string>();
        private StorageFolder folder;

        public LocalStorage(bool autoSave = true, string relativeDirectory = "data")
        {
            directory = relativeDirectory;
            AutoSave = autoSave;
        }

        public bool AutoSave { get; set; }


        public int Count => storage.Count;

        public Dictionary<string, string>.KeyCollection Keys => storage.Keys;

        public Dictionary<string, string>.ValueCollection Values => storage.Values;

        public string this[string key]
        {
            get { return !storage.ContainsKey(key) ? null : storage[key]; }
            set
            {
                if (storage.ContainsKey(key))
                    storage[key] = value;
                else
                    storage.Add(key, value);
                if (AutoSave)
                    Save(key);
            }
        }

        public async Task Initialize()
        {
            if (await ApplicationData.Current.LocalFolder.TryGetItemAsync(directory) == null)
            {
                folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(directory,
                    CreationCollisionOption.ReplaceExisting);
            }
            else
            {
                folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(directory);
                var items = await folder.GetItemsAsync();
                foreach (var item in items)
                {
                    var file = (StorageFile) item;
                    var value = await FileIO.ReadTextAsync(file);
                    storage.Add(file.DisplayName, value);
                }
            }
        }

        public async void Clear()
        {
            storage.Clear();
            var toDelete = await folder.GetItemsAsync();
            foreach (var item in toDelete)
                await item.DeleteAsync(StorageDeleteOption.Default);
        }

        public async void Remove(string key)
        {
            storage.Remove(key);

            var toDelete = await folder.GetItemAsync(key + ".json");
            await toDelete.DeleteAsync(StorageDeleteOption.Default);
        }

        public bool ContainsKey(string key)
        {
            return storage.ContainsKey(key);
        }

        private async void Add(string key, string value)
        {
            var file = (StorageFile) await folder.TryGetItemAsync(key + ".json") ??
                       await folder.CreateFileAsync(key + ".json", CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteTextAsync(file, value);
        }

        public void Save(string key)
        {
            Add(key, storage[key]);
        }

        public void Save()
        {
            foreach (var pair in storage)
                Add(pair.Key, pair.Value);
        }
    }
}