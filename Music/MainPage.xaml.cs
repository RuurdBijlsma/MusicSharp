using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Core;
using Windows.Media;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Windows.Storage;
using Windows.System.UserProfile;
using System.Threading.Tasks;
using System.Threading;
using Windows.System;
using Windows.UI.ViewManagement;


// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Music
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MediaElement media
        {
            get
            {
                return mediaElement;
            }
        }
        public string titleBox
        {
            get
            {
                return (string)titleTextBox.Text;
            }
            set
            {
                titleTextBox.Text = value;
            }
        }
        public Slider seekBar { get { return SeekBar; } }
        public ListView listView { get { return SongsList; } }

        void Controls_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    currentList.Play();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    currentList.Pause();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    currentList.Next();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    currentList.Previous();
                    break;
                default:
                    break;
            }
        }

        public LocalStorage localStorage = new LocalStorage(false);
        Playlist songs;
        Playlist currentList;
        private CoreDispatcher dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

        public SystemMediaTransportControls controls;


        public MainPage()
        {
            this.InitializeComponent();
            FontFamily ff = new FontFamily("Segoe");
            Windows.UI.Text.FontWeight fw = new Windows.UI.Text.FontWeight();
            fw.Weight = 100;
            titleTextBox.FontFamily = ff;
            titleTextBox.FontWeight = fw;
            SongsList.FontWeight = fw;

            SeekBar.AddHandler(PointerPressedEvent,
            new PointerEventHandler(SeekBar_MouseDown), true);

            SeekBar.AddHandler(PointerReleasedEvent,
            new PointerEventHandler(SeekBar_MouseUp), true);

            Window.Current.VisibilityChanged += async (ss, ee) =>
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (mediaElement.CanSeek && mediaElement != null && mediaElement.Position.TotalSeconds != 0)
                    {
                        currentList.StartTime = mediaElement.Position;
                        localStorage["Playlist" + currentList.Name] = currentList.ToString();
                    }
                    localStorage.Save();
                });
            };

            mediaElement.MediaOpened += LoadedMedia;

            Startup();

            Application.Current.Resources["SystemControlHighlightListAccentLowBrush"] = new SolidColorBrush(Windows.UI.Color.FromArgb(50, 255, 255, 255));
            Application.Current.Resources["SystemControlHighlightListAccentMediumBrush"] = new SolidColorBrush(Windows.UI.Color.FromArgb(100, 255, 255, 255));

            controls = SystemMediaTransportControls.GetForCurrentView();
            controls.ButtonPressed += Controls_ButtonPressed;

            controls.IsPlayEnabled = true;
            controls.IsPauseEnabled = true;
            controls.IsNextEnabled = true;
            controls.IsPreviousEnabled = true;
        }

        private void LoadedMedia(object sender, RoutedEventArgs e)
        {
            currentList.Loaded();
        }

        private async void Startup()
        {

            await localStorage.Initialize();

            if (localStorage["PlaylistAll"] == null)
            {
                //first startup, er staan nog geen liedjes in de file
                localStorage["PlaylistAll"] = "";
                songs = new Playlist("All", await AddDirectory(), this);
                localStorage["PlaylistAll"] = songs.ToString();
            }
            else
            {
                songs = GetPlaylist("All");
            }
            currentList = songs;

            foreach (Song song in songs.Songs)
            {
                TextBlock tb = new TextBlock();
                tb.Text = song.NiceTitle;
                tb.FontSize = 18;
                SongsList.Items.Add(tb);
            }

            currentList.SetSongInfo();
        }

        private Playlist GetPlaylist(string name)
        {
            List<Song> songList = new List<Song>();
            JToken playlistJson = JObject.Parse(localStorage["Playlist" + name]);
            int nowPlaying = (int)playlistJson["NowPlaying"];

            string time = (string)playlistJson["StartTime"];
            string[] hms = time.Split(':');

            int h;
            int.TryParse(hms[0], out h);
            int m;
            int.TryParse(hms[1], out m);
            double s;
            double.TryParse(hms[2], out s);

            TimeSpan currentTime = new TimeSpan(0, h, m, (int)s, (int)((s % 1) * 1000));
            JToken songsJson = playlistJson["Songs"];
            foreach (JToken song in songsJson)
            {
                string fullTitle = (string)song["FullTitle"];
                string path = (string)song["Path"];
                long dateAdded = (long)song["DateAdded"];
                string niceTitle = (string)song["NiceTitle"];
                int playCount = (int)song["PlayCount"];
                songList.Add(new Song(fullTitle, path, dateAdded, playCount, niceTitle));
            }
            return new Playlist("All", songList, this, nowPlaying, currentTime.Ticks);
        }

        private async Task<List<Song>> AddDirectory()
        {
            Windows.Storage.Pickers.FolderPicker folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.MusicLibrary;
            folderPicker.FileTypeFilter.Add("*");

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                // Application now has read/write access to all contents in the picked folder
                // (including other sub-folder contents)
                Windows.Storage.AccessCache.StorageApplicationPermissions.
                FutureAccessList.AddOrReplace("PickedFolderToken", folder);

                localStorage["folders"] += "<:>" + folder.Path;
                await exploreFolder(folder);
                return foundSongs;
            }
            else
            {
                return null;
            }
        }

        List<Song> foundSongs = new List<Song>();

        private async Task exploreFolder(StorageFolder folder)
        {
            IReadOnlyList<IStorageItem> items = await folder.GetItemsAsync();
            foreach (IStorageItem item in items)
            {
                if (item is StorageFolder)
                {
                    await exploreFolder((StorageFolder)item);
                }
                else
                {
                    StorageFile song = (StorageFile)item;
                    if (song.FileType == ".mp3" || song.FileType == ".m4a")
                    {
                        foundSongs.Add(new Song(item.Name, item.Path, item.DateCreated.ToUnixTimeMilliseconds()));
                        if(foundSongs.GroupBy(Song => Song.NiceTitle).ToList().Count != foundSongs.Count)
                        {
                            //duplicate found
                            foundSongs.RemoveAt(foundSongs.Count - 1);
                        }
                    }
                }
            }
        }

        bool firstLoad = true;
        private void SongsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (firstLoad)
            {
                firstLoad = false;
            }
            else
            {
                currentList.StartTime = new TimeSpan(0);
            }

            int index = SongsList.SelectedIndex;
            currentList.Play(index);
        }

        private void MediaElement_CurrentStateChanged(object sender, RoutedEventArgs e)
        {

            switch (mediaElement.CurrentState)
            {
                case MediaElementState.Playing:
                    controls.PlaybackStatus = MediaPlaybackStatus.Playing;
                    PlayButton.Source = PauseImage.Source;
                    break;
                case MediaElementState.Paused:
                    controls.PlaybackStatus = MediaPlaybackStatus.Paused;
                    PlayButton.Source = PlayImage.Source;
                    double position = mediaElement.Position.TotalSeconds;
                    double duration = mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                    if (Math.Abs(position - duration) < 1)
                    {
                        currentList.Next();
                    }
                    break;
                case MediaElementState.Stopped:
                    controls.PlaybackStatus = MediaPlaybackStatus.Stopped;
                    break;
                case MediaElementState.Closed:
                    controls.PlaybackStatus = MediaPlaybackStatus.Closed;
                    break;
                default:
                    break;
            }
        }



        private void Grid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            ApplicationView view = ApplicationView.GetForCurrentView();
            if (view.IsFullScreenMode)
            {
                view.ExitFullScreenMode();
            }
            else
            {
                bool succeeded = view.TryEnterFullScreenMode();
            }
        }

        public bool seekDown = false;
        private void SeekBar_MouseUp(object sender, PointerRoutedEventArgs e)
        {
            seekDown = false;
            TimeSpan newTime = new TimeSpan(0, 0, (int)SeekBar.Value / 10);
            mediaElement.Position = newTime;
        }
        private void SeekBar_MouseDown(object sender, PointerRoutedEventArgs e)
        {
            seekDown = true;
        }
        private void SeekBar_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (seekDown)
            {
                TimeSpan newTime = new TimeSpan(0, 0, (int)e.NewValue / 10);
                mediaElement.Position = newTime;
            }
        }

        private void PlayButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (mediaElement.CurrentState == MediaElementState.Paused)
            {
                currentList.Play();
            }
            else
            {
                currentList.Pause();
            }
        }
    }
}