using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace Music
{
    public class LocalStorage
    {
        private Dictionary<string, string> storage = new Dictionary<string, string>();
        private StorageFolder folder;
        private string directory;
        public bool AutoSave { get; set; }

        public LocalStorage(bool autoSave = true, string relativeDirectory = "data")
        {
            directory = relativeDirectory;
            AutoSave = autoSave;
        }
        public async Task Initialize()
        {
            if (await ApplicationData.Current.LocalFolder.TryGetItemAsync(directory) == null)
            {
                folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(directory, CreationCollisionOption.ReplaceExisting);
            }
            else
            {
                folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(directory);
                IReadOnlyList<IStorageItem> items = await folder.GetItemsAsync();
                foreach (IStorageItem item in items)
                {
                    StorageFile file = (StorageFile)item;
                    string value = await FileIO.ReadTextAsync(file);
                    storage.Add(file.DisplayName, value);
                }
            }
        }

        public async void Clear()
        {
            storage.Clear();
            IReadOnlyList<IStorageItem> toDelete = await folder.GetItemsAsync();
            foreach (IStorageItem item in toDelete)
            {
                await item.DeleteAsync(StorageDeleteOption.Default);
            }
        }
        public async void Remove(string key)
        {
            storage.Remove(key);

            IStorageItem toDelete = await folder.GetItemAsync(key + ".json");
            await toDelete.DeleteAsync(StorageDeleteOption.Default);
        }


        public int Count
        {
            get
            {
                return storage.Count;
            }
        }

        public Dictionary<string, string>.KeyCollection Keys
        {
            get
            {
                return storage.Keys;
            }
        }

        public Dictionary<string, string>.ValueCollection Values
        {
            get
            {
                return storage.Values;
            }
        }

        public bool ContainsKey(string key)
        {
            return storage.ContainsKey(key);
        }

        private async void Add(string key, string value)
        {
            StorageFile file = (StorageFile)await folder.TryGetItemAsync(key + ".json");
            if (file == null)
            {
                file = await folder.CreateFileAsync(key + ".json", CreationCollisionOption.ReplaceExisting);
            }

            try
            {
                await FileIO.WriteTextAsync(file, value);
            }
            catch
            {
                //nikksssss
            }
        }
        public void Save(string key)
        {
            Add(key, storage[key]);
        }
        public void Save()
        {
            foreach (KeyValuePair<string, string> pair in storage)
            {
                Add(pair.Key, pair.Value);
            }
        }

        public string this[string key]
        {
            get
            {
                if (!storage.ContainsKey(key))
                {
                    return null;
                }
                return storage[key];
            }
            set
            {
                if (storage.ContainsKey(key))
                {
                    storage[key] = value;
                }
                else
                {
                    storage.Add(key, value);
                }
                if (AutoSave)
                {
                    Save(key);
                }
            }
        }
    }
}
