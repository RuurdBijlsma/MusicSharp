using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Music
{
    public class Song
    {
        public Song(string title, string path, long dateModified, int playCount = 0, string niceTitle = null)
        {
            FullTitle = title;
            Path = path;
            DateModified = dateModified;
            PlayCount = playCount;

            NiceTitle = ParseTitle(title);
        }

        public string FullTitle { get; }
        public string Path { get; }
        public long DateModified { get; }
        public int PlayCount { get; set; }

        public string NiceTitle { get; }

        private string ParseTitle(string title)
        {
            string newTitle = title.Substring(0, title.Length - title.Split('.').Last().Length - 1);
            //.mp3 etc er af gehaald

            newTitle = new Regex(@"\[([^]]+)\]").Replace(newTitle, "");
            newTitle = new Regex(@"\(([^]]+)\)").Replace(newTitle, "");
            newTitle = new Regex(@"\{([^]]+)\}").Replace(newTitle, "");

            if (newTitle.Replace(" ", "").Length == 0)
                newTitle = title.Substring(0, title.Length - title.Split('.').Last().Length - 1);

            newTitle = newTitle.Replace("_", " ");
            newTitle = newTitle.Replace("  ", " ");
            newTitle = newTitle.Replace("  ", " ");

            double n;
            string first = newTitle.Split(' ')[0];
            if (double.TryParse(first, out n))
                newTitle = newTitle.Substring(first.Length, newTitle.Length - first.Length);

            while (newTitle[0] == ' ' || newTitle[0] == '-')
                newTitle = newTitle.Substring(1);

            while (newTitle[newTitle.Length - 1] == ' ')
                newTitle = newTitle.Substring(0, newTitle.Length - 1);

            return newTitle;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    public class Playlist
    {
        public Playlist(string name, List<Song> songs)
        {
            Name = name;
            Songs = songs;
        }

        public string Name { get; set; }
        public List<Song> Songs { get; set; }


        public void Add(Song song)
        {
            Songs.Add(song);
        }

        public void AddRange(List<Song> songs)
        {
            Songs.AddRange(songs);
        }

        public override string ToString()
        {
            //string output = "{\n\t\"Name\":\"" + Name + "\",\n\t\"NowPlaying\":" + nowPlaying + ",\n\t\"Songs\":\n\t[\n";
            //for (int i = 0; i < Songs.Count; i++)
            //{
            //    if (i != 0)
            //        output += "\n,";
            //    output += Songs[i].ToString();
            //}
            //output += "\n\t]\n}";

            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    public class MusicManager
    {
        private StorageFile audio;
        private readonly CoreDispatcher dispatcher;
        private bool firstLoad;
        private LocalStorage localStorage;

        private readonly MainPage mainPage;
        private readonly MediaElement media;
        private Timer musicTimer;
        private StorageFolder pictureFolder;
        private readonly Slider seekBar;
        private readonly ListView songsListView;
        private bool thisSongPlayed;
        private Timer timeout;
        private readonly SystemMediaTransportControlsDisplayUpdater updater;


        public MusicManager(List<Song> allSongs, MainPage mp, List<string> musicFolders,
            List<Playlist> playlists = null,
            int currentList = 0, string sort = "date", bool ascending = false, int songIndex = 0, long startTime = 0,
            bool shuffle = false, bool repeat = true, int volume = 100)
        {
            MusicFolders = musicFolders;
            if (playlists == null)
            {
                allSongs = SortList(allSongs, "date", false);
                Playlists = new List<Playlist>();
                Playlists.Add(new Playlist("All", allSongs));
            }
            else
            {
                Playlists = playlists;
            }
            CurrentList = currentList;
            NowPlaying = songIndex;
            StartTime = new TimeSpan(startTime);
            firstLoad = true;
            SortBy = sort;
            Ascending = ascending;

            timeout = null;

            mainPage = mp;
            localStorage = mp.localStorage;
            updater = mp.controls.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            media = mp.media;
            seekBar = mp.seekBar;
            songsListView = mp.songsList;

            SetVolume(volume);

            Repeat = repeat;
            Shuffle = shuffle;
            if (!Repeat)
                mp.repeatButton.Opacity = 0.5;
            if (Shuffle)
                mp.shuffleButton.Opacity = 1;

            StartTimer();
            Initialize();
        }

        public int NowPlaying { get; set; }
        public int CurrentList { get; set; }
        public TimeSpan StartTime { get; set; }
        public List<string> MusicFolders { get; set; }
        public bool Repeat { get; set; }
        public bool Shuffle { get; set; }
        public string SortBy { get; set; }
        public bool Ascending { get; set; }
        public int Volume { get; set; }
        public List<Playlist> Playlists { get; set; }

        public void AddSongs(List<Song> songs)
        {
            Song currentSong = Playlists[CurrentList].Songs[NowPlaying];
            Playlists[0].AddRange(songs);
            if (!Shuffle)
                Playlists[0].Songs = SortList(Playlists[0].Songs, SortBy, Ascending);
            DisplayList(Playlists[CurrentList], Playlists[CurrentList].Songs.IndexOf(currentSong));
        }

        public void RemoveSongs(List<Song> songs)
        {
            Song currentSong = Playlists[CurrentList].Songs[NowPlaying];
            foreach (Song song in songs)
                Playlists[0].Songs.Remove(song);
            if (!Shuffle)
                Playlists[0].Songs = SortList(Playlists[0].Songs, SortBy, Ascending);
            DisplayList(Playlists[CurrentList], Playlists[CurrentList].Songs.IndexOf(currentSong));
        }

        private async void Initialize()
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            pictureFolder = (StorageFolder) await localFolder.TryGetItemAsync("Album Covers");
            if (pictureFolder == null)
                pictureFolder = await localFolder.CreateFolderAsync("Album Covers",
                    CreationCollisionOption.ReplaceExisting);
        }


        public async void SetAlbumArt(string niceTitle)
        {
            var picture = (StorageFile) await pictureFolder.TryGetItemAsync(niceTitle + ".jpg");

            var bitmap = new BitmapImage();

            if (picture != null)
            {
                using (IRandomAccessStreamWithContentType pictureStream = await picture.OpenReadAsync())
                {
                    updater.Thumbnail = RandomAccessStreamReference.CreateFromStream(pictureStream);
                    bitmap.SetSource(pictureStream);
                    pictureStream.Dispose();
                }
            }
            else
            {
                string result = "";
                try
                {
                    string url =
                        @"https://www.googleapis.com/customsearch/v1?key=AIzaSyDrSn8h3ZnHe_zg-FkVGuHUBNYAhJ31Nqw&cx=000001731481601506413:s6vjwyrugku&fileType=jpg&searchType=image&imgSize=large&num=1&q=" +
                        niceTitle;

                    var request = (HttpWebRequest) WebRequest.Create(url);
                    var response = (HttpWebResponse) await request.GetResponseAsync();

                    using (var sr = new StreamReader(response.GetResponseStream()))
                    {
                        result = sr.ReadToEnd();
                    }

                    string image = (string) JObject.Parse(result)["items"][0]["link"];


                    using (Stream originalStream = await new HttpClient().GetStreamAsync(image))
                    {
                        using (var memStream = new MemoryStream())
                        {
                            await originalStream.CopyToAsync(memStream);
                            memStream.Position = 0;

                            await bitmap.SetSourceAsync(memStream.AsRandomAccessStream());
                            updater.Thumbnail =
                                RandomAccessStreamReference.CreateFromStream(memStream.AsRandomAccessStream());

                            StorageFile file = await pictureFolder.CreateFileAsync(niceTitle + ".jpg",
                                CreationCollisionOption.ReplaceExisting);
                            using (Stream fileStream = await file.OpenStreamForWriteAsync())
                            {
                                byte[] buffer = memStream.ToArray();
                                await fileStream.WriteAsync(buffer, 0, buffer.Length);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    //internet is niet availai
                }
            }
            mainPage.albumImage.Source = bitmap;
            updater.Update();
        }

        public void StartTimer()
        {
            musicTimer = new Timer(async obj =>
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (media.CanSeek)
                    {
                        if (media.Position.TotalSeconds > media.NaturalDuration.TimeSpan.TotalSeconds / 2 &&
                            !thisSongPlayed)
                        {
                            thisSongPlayed = true;
                            Playlists[CurrentList].Songs[NowPlaying].PlayCount++;
                        }

                        if (!mainPage.seekDown)
                        {
                            seekBar.Value = media.Position.TotalSeconds * 10;
                            mainPage.timeTextBlock = TimeToString(media.Position) + "/" +
                                                     TimeToString(media.NaturalDuration.TimeSpan);
                        }
                    }
                });
            }, null, 500, 500);
        }

        public void StopTimer()
        {
            musicTimer.Dispose();
        }

        public void SetVolume(int volume, bool changeVal = false)
        {
            if (volume > 100)
                volume = 100;
            else if (volume < 0)
                volume = 0;
            Volume = volume;
            media.Volume = (double) volume / 100;
            if (firstLoad || changeVal)
                mainPage.volumeSlider.Value = volume;
        }

        public void SetSongInfo()
        {
            updater.MusicProperties.Title = Playlists[CurrentList].Songs[NowPlaying].NiceTitle;
            updater.Update();
            thisSongPlayed = false;

            SetAlbumArt(Playlists[CurrentList].Songs[NowPlaying].NiceTitle);

            mainPage.titleBox = Playlists[CurrentList].Songs[NowPlaying].NiceTitle;

            mainPage.listView.SelectedIndex = NowPlaying;
            mainPage.listView.ScrollIntoView(mainPage.listView.Items[NowPlaying], ScrollIntoViewAlignment.Leading);

            mainPage.songInfoBlock.Text = Playlists[CurrentList].Songs[NowPlaying].NiceTitle;
            if (mainPage.songsList.Visibility == Visibility.Collapsed)
            {
                //controls zijn verstopt
                mainPage.songInfoBlock.Opacity = .9;
                if (timeout != null)
                {
                    timeout.Dispose();
                    timeout = null;
                }
                timeout = new Timer(async a =>
                {
                    await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        mainPage.songInfoBlock.Opacity = 0;
                        timeout.Dispose();
                    });
                }, null, 2000, 1000);
            }
        }

        private List<Song> SortList(List<Song> list, string sortBy = "date", bool ascending = true)
        {
            switch (sortBy)
            {
                case "title":
                    list = list.OrderBy(Song => Song.NiceTitle).ToList();
                    break;
                case "date":
                    list = list.OrderBy(Song => Song.DateModified).ToList();
                    break;
                case "playcount":
                    list = list.OrderBy(Song => Song.PlayCount).ToList();
                    break;
                default:
                    list = list.OrderBy(Song => Song.NiceTitle).ToList();
                    break;
            }
            if (!ascending)
                list.Reverse();
            return list;
        }

        private void DisplayList(Playlist list, int selectedIndex)
        {
            if (songsListView.Items.Count == list.Songs.Count)
            {
                for (int i = 0; i < list.Songs.Count; i++)
                {
                    var tb = (TextBlock) songsListView.Items[i];
                    tb.Text = list.Songs[i].NiceTitle;
                }
            }
            else
            {
                songsListView.Items.Clear();
                foreach (Song song in list.Songs)
                {
                    var tb = new TextBlock();
                    tb.Text = song.NiceTitle;
                    tb.FontSize = 18;
                    songsListView.Items.Add(tb);
                }
            }
            NowPlaying = selectedIndex;
            mainPage.dontUpdate = true;
            mainPage.listView.SelectedIndex = selectedIndex;
        }

        public void ToggleShuffle()
        {
            Song currentSong = Playlists[CurrentList].Songs[NowPlaying];
            if (Shuffle)
            {
                Shuffle = false;
                mainPage.shuffleButton.Opacity = 0.5;
                Playlists[CurrentList].Songs = SortList(Playlists[CurrentList].Songs, SortBy, Ascending);
            }
            else
            {
                Shuffle = true;
                mainPage.shuffleButton.Opacity = 1;
                Playlists[CurrentList].Songs.Shuffle();
            }
            int currentIndex = Playlists[CurrentList].Songs.FindSong(currentSong);
            DisplayList(Playlists[CurrentList], currentIndex);
            songsListView.ScrollIntoView(NowPlaying, ScrollIntoViewAlignment.Leading);
        }

        public void ToggleRepeat()
        {
            if (Repeat)
            {
                Repeat = false;
                mainPage.repeatButton.Opacity = 0.5;
            }
            else
            {
                Repeat = true;
                mainPage.repeatButton.Opacity = 1;
            }
        }

        public void SortSongs(int index)
        {
            Song currentSong = Playlists[CurrentList].Songs[NowPlaying];
            switch (index)
            {
                case 0:
                    SortBy = "date";
                    Ascending = false;
                    break;
                case 1:
                    SortBy = "date";
                    Ascending = true;
                    break;
                case 2:
                    SortBy = "title";
                    Ascending = true;
                    break;
                case 3:
                    SortBy = "title";
                    Ascending = false;
                    break;
                case 4:
                    SortBy = "playcount";
                    Ascending = false;
                    break;
                case 5:
                    SortBy = "playcount";
                    Ascending = true;
                    break;
            }
            Shuffle = true;
            ToggleShuffle();
            DisplayList(Playlists[CurrentList], Playlists[CurrentList].Songs.IndexOf(currentSong));
            mainPage.listView.ScrollIntoView(NowPlaying, ScrollIntoViewAlignment.Leading);
        }

        public void Loaded() //zodra liedje geladen is
        {
            double duration = media.NaturalDuration.TimeSpan.TotalSeconds;
            seekBar.Maximum = duration * 10;
            if (!firstLoad)
            {
                media.Play();
                mainPage.timeTextBlock = TimeToString(media.Position) + "/" +
                                         TimeToString(media.NaturalDuration.TimeSpan);
            }
            else
            {
                firstLoad = false;
            }
        }

        public async Task LoadSong(int index)
        {
            NowPlaying = index;
            audio = await StorageFile.GetFileFromPathAsync(Playlists[CurrentList].Songs[index].Path);

            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { SetSongInfo(); });

            if (audio != null)
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    IRandomAccessStream stream = await audio.OpenAsync(FileAccessMode.Read);
                    media.SetSource(stream, audio.ContentType);
                    seekBar.Value = 0;
                });
        }

        public async void Play(int index = -1)
        {
            if (index != -1)
                await LoadSong(index);
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { media.Play(); });
        }

        public async void Pause()
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { mainPage.media.Pause(); });
        }

        public void Next()
        {
            if (NowPlaying == Playlists[CurrentList].Songs.Count - 1)
                Play(0);
            else
                Play(NowPlaying + 1);
        }

        public void Previous()
        {
            if (NowPlaying == 0)
                Play(Playlists[CurrentList].Songs.Count - 1);
            else
                Play(NowPlaying - 1);
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        private string TimeToString(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return time.Hours + ":" + time.Minutes.ToString("D2") + ":" + time.Seconds.ToString("D2");
            return time.Minutes + ":" + time.Seconds.ToString("D2");
        }

        public static int CompareByTitle(Song song1, Song song2)
        {
            return song1.NiceTitle.CompareTo(song2.NiceTitle);
        }
    }

    internal static class extention
    {
        private static readonly Random rng = new Random();

        public static void Shuffle<T>(this IList<T> list)
        {
            for (int n = list.Count - 1; n > 0; n--)
            {
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static int FindSong(this IList<Song> list, Song toFind)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == toFind)
                    return i;
            return -1;
        }
    }

    internal class SongEqualityComparer : IEqualityComparer<Song>
    {
        public bool Equals(Song s1, Song s2)
        {
            if (s1 == null && s2 == null)
                return true;
            if ((s1 == null) | (s2 == null))
                return false;
            return s1.NiceTitle == s2.NiceTitle;
        }

        public int GetHashCode(Song sx)
        {
            return sx.NiceTitle.GetHashCode();
        }
    }
}