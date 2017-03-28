using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Newtonsoft.Json.Linq;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Music
{
    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        public readonly SystemMediaTransportControls controls;
        private readonly CoreDispatcher dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

        private readonly List<Song> foundSongs = new List<Song>();
        public bool DontUpdate;

        private bool firstLoad = true;

        public LocalStorage LocalStorage = new LocalStorage(false);
        private MusicManager manager;


        public bool SeekDown;


        public MainPage()
        {
            InitializeComponent();

            var ff = new FontFamily("Segoe");
            var fw = new FontWeight {Weight = 100};
            titleTextBox.FontFamily = ff;
            titleTextBox.FontWeight = fw;
            SongsList.FontWeight = fw;
            TimeTextBlock.FontFamily = ff;
            TimeTextBlock.FontWeight = fw;


            new Clock(CurrentTime);


            SeekBar.AddHandler(PointerPressedEvent,
                new PointerEventHandler(SeekBar_MouseDown), true);

            SeekBar.AddHandler(PointerReleasedEvent,
                new PointerEventHandler(SeekBar_MouseUp), true);


            Window.Current.VisibilityChanged += async (ss, ee) =>
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (mediaElement.CanSeek && mediaElement != null && mediaElement.Position.TotalSeconds != 0)
                        manager.StartTime = mediaElement.Position;
                    if (manager != null)
                        LocalStorage["Music"] = manager.ToString();
                    LocalStorage.Save();
                });
            };

            mediaElement.MediaOpened += LoadedMedia;

            Startup();

            Application.Current.Resources["SystemControlHighlightListAccentLowBrush"] =
                new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            Application.Current.Resources["SystemControlHighlightListAccentMediumBrush"] =
                new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));

            controls = SystemMediaTransportControls.GetForCurrentView();
            controls.ButtonPressed += Controls_ButtonPressed;

            controls.IsPlayEnabled = true;
            controls.IsPauseEnabled = true;
            controls.IsNextEnabled = true;
            controls.IsPreviousEnabled = true;
        }

        public string timeTextBlock
        {
            get { return TimeTextBlock.Text; }
            set { TimeTextBlock.Text = value; }
        }

        public MediaElement Media => mediaElement;

        public ListView songsList => SongsList;

        public string TitleBox
        {
            get { return titleTextBox.Text; }
            set { titleTextBox.Text = value; }
        }

        public Image repeatButton
        {
            get { return RepeatButton; }
            set { RepeatButton = value; }
        }

        public Image shuffleButton
        {
            get { return ShuffleButton; }
            set { ShuffleButton = value; }
        }

        public Image albumImage
        {
            get { return AlbumImage; }
            set { AlbumImage = value; }
        }

        public Slider volumeSlider
        {
            get { return VolumeSlider; }
            set { VolumeSlider = value; }
        }

        public TextBlock currentTime
        {
            get { return CurrentTime; }
            set { CurrentTime = value; }
        }

        public TextBlock songInfoBlock
        {
            get { return SongInfoBlock; }
            set { SongInfoBlock = value; }
        }

        public Slider seekBar => SeekBar;

        public ListView listView => SongsList;

        private void Controls_ButtonPressed(SystemMediaTransportControls sender,
            SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            if (args.Button == SystemMediaTransportControlsButton.Play)
                manager.Play();
            else if (args.Button == SystemMediaTransportControlsButton.Pause)
                manager.Pause();
            else if (args.Button == SystemMediaTransportControlsButton.Next)
                manager.Next();
            else if (args.Button == SystemMediaTransportControlsButton.Previous)
                manager.Previous();
        }

        private void LoadedMedia(object sender, RoutedEventArgs e)
        {
            manager.Loaded();
        }

        private async void Startup()
        {
            await LocalStorage.Initialize();

            if (LocalStorage["Music"] == null)
            {
                //first startup, er staan nog geen liedjes in de file
                var folder = await AddFolder();
                manager = new MusicManager(await GetSongsFromFolder(folder), this, new List<string> {folder.Path});
                manager.MusicFolders.Add(folder.Path);
                LocalStorage["Music"] = manager.ToString();
            }
            else
            {
                manager = GetManager();
                UpdateSongs();
            }


            foreach (var song in manager.Playlists[manager.CurrentList].Songs)
            {
                var tb = new TextBlock
                {
                    Text = song.NiceTitle,
                    FontSize = 18
                };
                SongsList.Items?.Add(tb);
            }

            manager.SetSongInfo();
        }

        private async void UpdateSongs()
        {
            var toSearch = manager.MusicFolders.Distinct();
            var newSongs = new List<Song>();
            foreach (var directory in toSearch)
                newSongs.AddRange(await GetSongsFromFolder(await StorageFolder.GetFolderFromPathAsync(directory)));
            newSongs = newSongs.Distinct().OrderBy(song => song.NiceTitle).ToList();
            var oldSongs = manager.Playlists[0].Songs;

            var songEquals = new SongEqualityComparer();
            var removeSongs = oldSongs.Except(newSongs, songEquals).ToList();

            var comp = Comparer<Song>.Create(MusicManager.CompareByTitle);

            var toRemove = new List<int>();
            for (var i = oldSongs.Count - 1; i >= 0; i--)
            {
                var zelfdeSong = newSongs.BinarySearch(oldSongs[i], comp);
                if (zelfdeSong >= 0)
                    toRemove.Add(zelfdeSong);
            }
            toRemove.Sort();
            toRemove.Reverse();
            foreach (var t in toRemove)
                newSongs.RemoveAt(t);
            if (newSongs.Count > 0)
                manager.AddSongs(newSongs);
            if (removeSongs.Count > 0)
                manager.RemoveSongs(removeSongs);
        }

        private MusicManager GetManager()
        {
            var playlists = new List<Playlist>();
            var songList = new List<Song>();
            JToken playlistJson = JObject.Parse(LocalStorage["Music"]);
            var nowPlaying = (int) playlistJson["NowPlaying"];
            var currentList = (int) playlistJson["CurrentList"];
            var sort = (string) playlistJson["SortBy"];
            var ascending = (bool) playlistJson["Ascending"];
            var shuffle = (bool) playlistJson["Shuffle"];
            var repeat = (bool) playlistJson["Repeat"];
            var volume = (int) playlistJson["Volume"];

            var folderJson = playlistJson["MusicFolders"];
            var folders = folderJson.Select(folder => (string) folder).ToList();

            var time = (string) playlistJson["StartTime"];
            var hms = time.Split(':');

            int.TryParse(hms[0], out int h);
            int.TryParse(hms[1], out int m);
            double.TryParse(hms[2], out double s);

            var startTime = new TimeSpan(0, h, m, (int) s, (int) (s % 1 * 1000));
            if (startTime.TotalSeconds < 1)
                startTime = new TimeSpan(0);
            var playlistsJson = playlistJson["Playlists"];
            foreach (var playlist in playlistsJson)
            {
                songList.Clear();
                var name = (string) playlist["Name"];
                var songsJson = playlist["Songs"];
                songList.AddRange(from song in songsJson
                    let fullTitle = (string) song["FullTitle"]
                    let path = (string) song["Path"]
                    let dateAdded = (long) song["DateModified"]
                    let niceTitle = (string) song["NiceTitle"]
                    let playCount = (int) song["PlayCount"]
                    select new Song(fullTitle, path, dateAdded, playCount));
                playlists.Add(new Playlist(name, songList));
            }
            return new MusicManager(songList, this, folders, playlists, currentList, sort, ascending, nowPlaying,
                startTime.Ticks, shuffle, repeat, volume);
        }

        private static async Task<StorageFolder> AddFolder()
        {
            var folderPicker = new FolderPicker {SuggestedStartLocation = PickerLocationId.MusicLibrary};
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null) return null;
            // Application now has read/write access to all contents in the picked folder
            // (including other sub-folder contents)
            StorageApplicationPermissions.
                FutureAccessList.AddOrReplace("PickedFolderToken", folder);

            return folder;
        }

        private async Task<List<Song>> GetSongsFromFolder(StorageFolder folder)
        {
            await ExploreFolder(folder);
            return foundSongs;
        }

        private async Task ExploreFolder(IStorageFolder folder)
        {
            var items = await folder.GetItemsAsync();
            foreach (var item in items)
                if (item is StorageFolder)
                {
                    await ExploreFolder(item as StorageFolder);
                }
                else
                {
                    var song = (StorageFile) item;
                    if (song.FileType != ".mp3" && song.FileType != ".m4a") continue;
                    var properties = await item.GetBasicPropertiesAsync();
                    foundSongs.Add(new Song(item.Name, item.Path, properties.DateModified.ToUnixTimeMilliseconds()));
                    if (foundSongs.GroupBy(s =>
                    {
                        if (s == null) throw new ArgumentNullException(nameof(s));
                        return s.NiceTitle;
                    }).ToList().Count != foundSongs.Count)
                        foundSongs.RemoveAt(foundSongs.Count - 1);
                }
        }

        private void ToggleControls()
        {
            if (Controls.Visibility == Visibility.Visible)
            {
                Controls.Visibility = Visibility.Collapsed;
                SongsList.Visibility = Visibility.Collapsed;
                SeekBar.Visibility = Visibility.Collapsed;
                SongInfo.Visibility = Visibility.Collapsed;
                SortBar.Visibility = Visibility.Collapsed;
                CurrentTime.Opacity = .5;
            }
            else
            {
                Controls.Visibility = Visibility.Visible;
                SongsList.Visibility = Visibility.Visible;
                SeekBar.Visibility = Visibility.Visible;
                SongInfo.Visibility = Visibility.Visible;
                SortBar.Visibility = Visibility.Visible;
                CurrentTime.Opacity = .9;
            }
        }

        private void SongsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!DontUpdate)
            {
                var index = SongsList.SelectedIndex;
                if (firstLoad)
                {
                    firstLoad = false;
                    manager.LoadSong(index);
                }
                else
                {
                    manager.StartTime = new TimeSpan(0);
                    manager.Play(index);
                }
            }
            else
            {
                DontUpdate = false;
            }
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
                    var position = mediaElement.Position.TotalSeconds;
                    var duration = mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                    if (Math.Abs(position - duration) < 1)
                        manager.Next();
                    break;
                case MediaElementState.Stopped:
                    controls.PlaybackStatus = MediaPlaybackStatus.Stopped;
                    break;
                case MediaElementState.Closed:
                    controls.PlaybackStatus = MediaPlaybackStatus.Closed;
                    break;
            }
        }

        private void SeekBar_MouseUp(object sender, PointerRoutedEventArgs e)
        {
            SeekDown = false;
            var newTime = new TimeSpan(0, 0, (int) SeekBar.Value / 10);
            mediaElement.Position = newTime;
        }

        private void SeekBar_MouseDown(object sender, PointerRoutedEventArgs e)
        {
            SeekDown = true;
        }

        private void SeekBar_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!SeekDown) return;
            var newTime = new TimeSpan(0, 0, (int) e.NewValue / 10);
            mediaElement.Position = newTime;
        }

        private void PlayButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (mediaElement.CurrentState == MediaElementState.Paused)
                manager.Play();
            else
                manager.Pause();
        }

        private void NextButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            NextButton.Source = NextHoverImage.Source;
            Window.Current.CoreWindow.PointerCursor =
                new CoreCursor(CoreCursorType.Hand, 1);
        }

        private void NextButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            NextButton.Source = NextImage.Source;
            Window.Current.CoreWindow.PointerCursor =
                new CoreCursor(CoreCursorType.Arrow, 1);
        }

        private void PrevButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            PrevButton.Source = PrevHoverImage.Source;
            Window.Current.CoreWindow.PointerCursor =
                new CoreCursor(CoreCursorType.Hand, 1);
        }

        private void PrevButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            PrevButton.Source = PrevImage.Source;
            Window.Current.CoreWindow.PointerCursor =
                new CoreCursor(CoreCursorType.Arrow, 1);
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
            Window.Current.CoreWindow.PointerCursor =
                new CoreCursor(CoreCursorType.Hand, 1);
        }

        private void RepeatButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            RepeatButton.Source = RepeatImage.Source;
            Window.Current.CoreWindow.PointerCursor =
                new CoreCursor(CoreCursorType.Arrow, 1);
        }

        private void ShuffleButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ShuffleButton.Source = ShuffleHoverImage.Source;
            Window.Current.CoreWindow.PointerCursor =
                new CoreCursor(CoreCursorType.Hand, 1);
        }

        private void ShuffleButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ShuffleButton.Source = ShuffleImage.Source;
            Window.Current.CoreWindow.PointerCursor =
                new CoreCursor(CoreCursorType.Arrow, 1);
        }

        private void RepeatButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            manager.ToggleRepeat();
        }


        private void ShuffleButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            manager.ToggleShuffle();
        }

        private void PlayButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor =
                new CoreCursor(CoreCursorType.Hand, 1);
        }

        private void PlayButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor =
                new CoreCursor(CoreCursorType.Arrow, 1);
        }

        private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            manager?.SetVolume((int) e.NewValue);
        }

        private void BackgroundPanel_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var view = ApplicationView.GetForCurrentView();
            if (view.IsFullScreenMode)
                view.ExitFullScreenMode();
            else
                view.TryEnterFullScreenMode();
            ToggleControls();
        }


        private void VolumeSlider_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint((Slider) sender);
            var delta = properties.Properties.MouseWheelDelta;
            manager.SetVolume((int) (mediaElement.Volume * 100) + delta / 40, true);
        }


        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Space:
                    if (mediaElement.CurrentState == MediaElementState.Playing)
                        manager.Pause();
                    else
                        manager.Play();
                    break;
                case VirtualKey.Right:
                    manager.Next();
                    break;
                case VirtualKey.Left:
                    manager.Previous();
                    break;
                case VirtualKey.Up:
                    manager.SetVolume((int) (mediaElement.Volume * 100) + 15, true);
                    break;
                case VirtualKey.Down:
                    manager.SetVolume((int) (mediaElement.Volume * 100) - 15, true);
                    break;
                case VirtualKey.Insert:
                    ToggleControls();
                    break;
            }
        }

        private void SortSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            manager?.SortSongs(SortSelect.SelectedIndex);
        }
    }
}