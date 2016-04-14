using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Windows.Media;
using Windows.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Foundation;
using System.IO;
using System;
using System.Threading.Tasks;
using Windows.UI.Core;
using System.Threading;
using Windows.UI.Xaml.Controls;

namespace Music
{
    public class Song
    {
        public string FullTitle { get; }
        public string Path { get; }
        public long DateModified { get; }
        public int PlayCount { get; set; }

        public Song(string title, string path, long dateModified, int playCount = 0, string niceTitle = null)
        {
            FullTitle = title;
            Path = path;
            DateModified = dateModified;
            PlayCount = playCount;

            NiceTitle = ParseTitle(title);
        }

        private string ParseTitle(string title)
        {
            string newTitle = title.Substring(0, title.Length - title.Split('.').Last<string>().Length - 1);//.mp3 etc er af gehaald

            newTitle = new Regex(@"\[([^]]+)\]").Replace(newTitle, "");
            newTitle = new Regex(@"\(([^]]+)\)").Replace(newTitle, "");
            newTitle = new Regex(@"\{([^]]+)\}").Replace(newTitle, "");

            if (newTitle.Replace(" ", "").Length == 0)
            {
                newTitle = title.Substring(0, title.Length - title.Split('.').Last<string>().Length - 1);
            }

            newTitle = newTitle.Replace("_", " ");
            newTitle = newTitle.Replace("  ", " ");
            newTitle = newTitle.Replace("  ", " ");

            double n;
            string first = newTitle.Split(' ')[0];
            if (double.TryParse(first, out n))
            {
                newTitle = newTitle.Substring(first.Length, newTitle.Length - first.Length);
            }

            while (newTitle[0] == ' ' || newTitle[0] == '-')
            {
                newTitle = newTitle.Substring(1);
            }

            while (newTitle[newTitle.Length - 1] == ' ')
            {
                newTitle = newTitle.Substring(0, newTitle.Length - 1);
            }

            return newTitle;
        }

        public string NiceTitle { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
    public class Playlist
    {
        public string Name { get; set; }
        public List<Song> Songs { get; set; }

        public Playlist(string name, List<Song> songs)
        {
            Name = name;
            Songs = songs;
        }


        public void Add(Song song)
        {
            Songs.Add(song);
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

        private MainPage mainPage;
        private LocalStorage localStorage;
        private SystemMediaTransportControlsDisplayUpdater updater;
        private StorageFile audio;
        private CoreDispatcher dispatcher;
        private Timer musicTimer;
        private bool thisSongPlayed = false;
        private MediaElement media;
        private bool firstLoad;
        private Slider seekBar;
        private ListView songsListView;


        public MusicManager(List<Song> allSongs, MainPage mp, List<string> musicFolders, List<Playlist> playlists = null, int currentList = 0, string sort = "date", bool ascending = false, int songIndex = 0, long startTime = 0, bool shuffle = false, bool repeat = true, int volume = 100)
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
            {
                mp.repeatButton.Opacity = 0.5;
            }
            if (Shuffle)
            {
                mp.shuffleButton.Opacity = 1;
            }

            StartTimer();
        }

        public void StartTimer()
        {
            musicTimer = new Timer(async (obj) =>
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (media.CanSeek)
                    {
                        if (media.Position.TotalSeconds > media.NaturalDuration.TimeSpan.TotalSeconds / 2 && !thisSongPlayed)
                        {
                            thisSongPlayed = true;
                            Playlists[CurrentList].Songs[NowPlaying].PlayCount++;
                        }

                        if (!mainPage.seekDown)
                        {
                            seekBar.Value = media.Position.TotalSeconds * 10;
                            mainPage.timeTextBlock = TimeToString(media.Position) + "/" + TimeToString(media.NaturalDuration.TimeSpan);
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
            {
                volume = 100;
            }
            else if (volume < 0)
            {
                volume = 0;
            }
            Volume = volume;
            media.Volume = (double)volume / 100;
            if (firstLoad||changeVal)
            {
                mainPage.volumeSlider.Value = volume;
            }
        }

        public void SetSongInfo()
        {
            updater.MusicProperties.Title = Playlists[CurrentList].Songs[NowPlaying].NiceTitle;
            updater.Update();
            thisSongPlayed = false;

            mainPage.titleBox = Playlists[CurrentList].Songs[NowPlaying].NiceTitle;

            mainPage.listView.SelectedIndex = NowPlaying;
            mainPage.listView.ScrollIntoView(mainPage.listView.Items[NowPlaying], ScrollIntoViewAlignment.Leading);
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
            {
                list.Reverse();
            }
            return list;
        }
        private void DisplayList(Playlist list)
        {
            if (songsListView.Items.Count == list.Songs.Count)
            {
                for (int i = 0; i < list.Songs.Count; i++)
                {
                    TextBlock tb = (TextBlock)songsListView.Items[i];
                    tb.Text = list.Songs[i].NiceTitle;
                }
            }
            else
            {
                songsListView.Items.Clear();
                foreach (Song song in list.Songs)
                {
                    TextBlock tb = new TextBlock();
                    tb.Text = song.NiceTitle;
                    songsListView.Items.Add(tb);
                }
            }
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
            DisplayList(Playlists[CurrentList]);
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

        public void Loaded()//zodra liedje geladen is
        {
            double duration = media.NaturalDuration.TimeSpan.TotalSeconds;
            seekBar.Maximum = duration * 10;
            if (!firstLoad)
            {
                media.Play();
                mainPage.timeTextBlock = TimeToString(media.Position) + "/" + TimeToString(media.NaturalDuration.TimeSpan);
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

            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                SetSongInfo();
            });

            if (audio != null)
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    IRandomAccessStream stream = await audio.OpenAsync(FileAccessMode.Read);
                    media.SetSource(stream, audio.ContentType);
                    seekBar.Value = 0;
                });
            }
        }

        public async void Play(int index = -1)
        {
            if (index != -1)
            {
                await LoadSong(index);
            }
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                media.Play();
            });

        }
        public async void Pause()
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                mainPage.media.Pause();
            });
        }
        public void Next()
        {
            if (NowPlaying == Playlists[CurrentList].Songs.Count - 1)
            {
                Play(0);
            }
            else
            {
                Play(NowPlaying + 1);
            }

        }
        public void Previous()
        {
            if (NowPlaying == 0)
            {
                Play(Playlists[CurrentList].Songs.Count - 1);
            }
            else
            {
                Play(NowPlaying - 1);
            }
        }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        private string TimeToString(TimeSpan time)
        {
            if (time.TotalHours >= 1)
            {
                return time.Hours.ToString() + ":" + time.Minutes.ToString("D2") + ":" + time.Seconds.ToString("D2");
            }
            else
            {
                return time.Minutes.ToString() + ":" + time.Seconds.ToString("D2");
            }
        }
    }

    static class extention
    {
        private static Random rng = new Random();

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static int FindSong(this IList<Song> list, Song toFind)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == toFind)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
