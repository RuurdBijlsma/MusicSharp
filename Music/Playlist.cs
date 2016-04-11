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

namespace Music
{
    public class Song
    {
        public string FullTitle { get; }
        public string Path { get; }
        public long DateAdded { get; }
        public int PlayCount { get; set; }

        public Song(string title, string path, long dateAdded, int playCount = 0, string niceTitle = null)
        {
            FullTitle = title;
            Path = path;
            DateAdded = dateAdded;
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
        public int NowPlaying { get; set; }
        private MainPage mainPage;
        private LocalStorage localStorage;
        private SystemMediaTransportControlsDisplayUpdater updater;
        private StorageFile audio;
        private CoreDispatcher dispatcher;
        private Timer musicTick;
        private bool thisSongPlayed = false;
        private Windows.UI.Xaml.Controls.MediaElement media;
        private bool firstLoad;
        private Windows.UI.Xaml.Controls.Slider seekBar;

        public TimeSpan StartTime { get; set; }

        public Playlist(string name, List<Song> songs, MainPage mp, int songIndex = 0, long currentTime = 0)
        {
            Name = name;
            Songs = songs;
            NowPlaying = songIndex;
            StartTime = new TimeSpan(currentTime);
            firstLoad = true;

            mainPage = mp;
            localStorage = mp.localStorage;
            updater = mp.controls.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            media = mp.media;
            seekBar = mp.seekBar;

            MusicTimer();
        }
        
        public void MusicTimer()
        {
            musicTick = new Timer(async (obj) =>
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (media.CanSeek)
                    {
                        if (media.Position.TotalSeconds > media.NaturalDuration.TimeSpan.TotalSeconds / 2 && !thisSongPlayed)
                        {
                            thisSongPlayed = true;
                            Songs[NowPlaying].PlayCount++;
                        }

                        localStorage["Playlist" + Name] = this.ToString();
                        if (!mainPage.seekDown)
                        {
                            seekBar.Value = media.Position.TotalSeconds * 10;
                        }
                    }
                });
            }, null, 1000, 500);
        }

        public void Add(Song song)
        {
            Songs.Add(song);
        }

        public void SetSongInfo()
        {
            updater.MusicProperties.Title = Songs[NowPlaying].NiceTitle;
            updater.Update();
            thisSongPlayed = false;

            mainPage.titleBox = Songs[NowPlaying].NiceTitle;

            mainPage.listView.SelectedIndex = NowPlaying;
            mainPage.listView.ScrollIntoView(mainPage.listView.Items[NowPlaying], Windows.UI.Xaml.Controls.ScrollIntoViewAlignment.Leading);
            localStorage["Playlist" + Name] = this.ToString();
        }

        public async void Loaded()//zodra liedje geladen is
        {
            if (firstLoad)
            {
                firstLoad = false;
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    media.Position = StartTime;
                    media.Pause();
                });
            }
            double duration = media.NaturalDuration.TimeSpan.TotalSeconds;
            seekBar.Maximum = duration * 10;
        }

        public async void Play(int index = -1)
        {
            if (index != -1)
            {
                NowPlaying = index;
                audio = await StorageFile.GetFileFromPathAsync(Songs[index].Path);

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
                        media.Play();
                    });
                }
            }
            else
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    mainPage.media.Play();
                });
            }

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
            if (NowPlaying == Songs.Count - 1)
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
                Play(Songs.Count - 1);
            }
            else
            {
                Play(NowPlaying - 1);
            }
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
}
