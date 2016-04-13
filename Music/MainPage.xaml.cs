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
        public ListView songsList
        {
            get
            {
                return SongsList;
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
                    manager.Play();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    manager.Pause();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    manager.Next();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    manager.Previous();
                    break;
                default:
                    break;
            }
        }

        public LocalStorage localStorage = new LocalStorage(false);
        MusicManager manager;
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
                        manager.StartTime = mediaElement.Position;
                    }
                    if (manager != null)
                    {
                        localStorage["Music"] = manager.ToString();
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
            manager.Loaded();
        }

        private async void Startup()
        {

            await localStorage.Initialize();

            if (localStorage["Music"] == null)
            {
                //first startup, er staan nog geen liedjes in de file
                StorageFolder folder = await AddFolder();
                manager = new MusicManager(await GetSongsFromFolder(folder), this);
                manager.MusicFolders.Add(folder.Path);
                localStorage["Music"] = manager.ToString();
            }
            else
            {
                manager = GetManager();
            }

            foreach (Song song in manager.Playlists[manager.CurrentList].Songs)
            {
                TextBlock tb = new TextBlock();
                tb.Text = song.NiceTitle;
                tb.FontSize = 18;
                SongsList.Items.Add(tb);
            }

            manager.SetSongInfo();
        }

        private MusicManager GetManager()
        {
            List<Playlist> playlists = new List<Playlist>();
            List<Song> songList = new List<Song>();
            JToken playlistJson = JObject.Parse(localStorage["Music"]);
            int nowPlaying = (int)playlistJson["NowPlaying"];
            int currentList = (int)playlistJson["CurrentList"];
            string sort = (string)playlistJson["Sort"];
            bool ascending = (bool)playlistJson["Ascending"];

            string time = (string)playlistJson["StartTime"];
            string[] hms = time.Split(':');

            int h;
            int.TryParse(hms[0], out h);
            int m;
            int.TryParse(hms[1], out m);
            double s;
            double.TryParse(hms[2], out s);

            TimeSpan startTime = new TimeSpan(0, h, m, (int)s, (int)((s % 1) * 1000));
            JToken playlistsJson = playlistJson["Playlists"];
            foreach (JToken playlist in playlistsJson)
            {
                songList.Clear();
                string name = (string)playlist["Name"];
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
                playlists.Add(new Playlist(name, songList));
            }
            return new MusicManager(songList, this, playlists, currentList, sort, ascending, nowPlaying, startTime.Ticks);
        }

        private async Task<StorageFolder> AddFolder()
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

                return folder;
            }
            else
            {
                return null;
            }
        }

        private async Task<List<Song>> GetSongsFromFolder(StorageFolder folder)
        {
            await exploreFolder(folder);
            return foundSongs;
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
                        Windows.Storage.FileProperties.BasicProperties properties = await item.GetBasicPropertiesAsync();
                        foundSongs.Add(new Song(item.Name, item.Path, properties.DateModified.ToUnixTimeMilliseconds()));
                        if (foundSongs.GroupBy(Song => Song.NiceTitle).ToList().Count != foundSongs.Count)
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
                manager.StartTime = new TimeSpan(0);
            }

            int index = SongsList.SelectedIndex;
            manager.Play(index);
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
                        manager.Next();
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
                manager.Play();
            }
            else
            {
                manager.Pause();
            }
        }

        private void NextButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            NextButton.Source = NextHoverImage.Source;
        }

        private void NextButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            NextButton.Source = NextImage.Source;
        }

        private void PrevButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            PrevButton.Source = PrevHoverImage.Source;
        }

        private void PrevButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            PrevButton.Source = PrevImage.Source;
        }

        private void NextButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            manager.Next();
        }

        private void PrevButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            manager.Previous();
        }

        private void RepeatButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            RepeatButton.Source = RepeatHoverImage.Source;
        }

        private void RepeatButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            RepeatButton.Source = RepeatImage.Source;
        }

        private void ShuffleButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ShuffleButton.Source = ShuffleHoverImage.Source;
        }

        private void ShuffleButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ShuffleButton.Source = ShuffleImage.Source;
        }

        private void RepeatButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            manager.ToggleRepeat();
        }


        private void ShuffleButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            manager.ToggleShuffle();
        }
    }
}