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
using System.Net.Http;
using System.Text;
using Google.Apis.Customsearch;
using System.Net;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Music
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public string timeTextBlock
        {
            get { return TimeTextBlock.Text; }
            set { TimeTextBlock.Text = value; }
        }
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
        public Slider volumeSlider
        {
            get { return VolumeSlider; }
            set { VolumeSlider = value; }
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

        private class SearchObject
        {
            public string q { get; set; }
            public string key { get; set; }
            public string cx { get; set; }
            public string searchType { get; set; }
            public string imgSize { get; set; }
            public int num { get; set; }
            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        public MainPage()
        {
            this.InitializeComponent();

            FontFamily ff = new FontFamily("Segoe");
            Windows.UI.Text.FontWeight fw = new Windows.UI.Text.FontWeight();
            fw.Weight = 100;
            titleTextBox.FontFamily = ff;
            titleTextBox.FontWeight = fw;
            SongsList.FontWeight = fw;
            TimeTextBlock.FontFamily = ff;
            TimeTextBlock.FontWeight = fw;



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

            //string url = @"https://www.googleapis.com/customsearch/v1?key=AIzaSyDrSn8h3ZnHe_zg-FkVGuHUBNYAhJ31Nqw&cx=000001731481601506413:s6vjwyrugku&searchType=image&imgSize=large&num=1&q=" + searchTerm;

            //HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            //HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync();

            //string result;
            //using (StreamReader sr = new StreamReader(response.GetResponseStream()))
            //{
            //    result = sr.ReadToEnd();
            //}

            string result = "{\n \"kind\": \"customsearch#search\",\n \"url\": {\n  \"type\": \"application/json\",\n  \"template\": \"https://www.googleapis.com/customsearch/v1?q={searchTerms}&num={count?}&start={startIndex?}&lr={language?}&safe={safe?}&cx={cx?}&cref={cref?}&sort={sort?}&filter={filter?}&gl={gl?}&cr={cr?}&googlehost={googleHost?}&c2coff={disableCnTwTranslation?}&hq={hq?}&hl={hl?}&siteSearch={siteSearch?}&siteSearchFilter={siteSearchFilter?}&exactTerms={exactTerms?}&excludeTerms={excludeTerms?}&linkSite={linkSite?}&orTerms={orTerms?}&relatedSite={relatedSite?}&dateRestrict={dateRestrict?}&lowRange={lowRange?}&highRange={highRange?}&searchType={searchType}&fileType={fileType?}&rights={rights?}&imgSize={imgSize?}&imgType={imgType?}&imgColorType={imgColorType?}&imgDominantColor={imgDominantColor?}&alt=json\"\n },\n \"queries\": {\n  \"nextPage\": [\n   {\n    \"title\": \"Google Custom Search - cat\",\n    \"totalResults\": \"2300000000\",\n    \"searchTerms\": \"cat\",\n    \"count\": 1,\n    \"startIndex\": 2,\n    \"inputEncoding\": \"utf8\",\n    \"outputEncoding\": \"utf8\",\n    \"safe\": \"off\",\n    \"cx\": \"000001731481601506413:s6vjwyrugku\",\n    \"searchType\": \"image\",\n    \"imgSize\": \"large\"\n   }\n  ],\n  \"request\": [\n   {\n    \"title\": \"Google Custom Search - cat\",\n    \"totalResults\": \"2300000000\",\n    \"searchTerms\": \"cat\",\n    \"count\": 1,\n    \"startIndex\": 1,\n    \"inputEncoding\": \"utf8\",\n    \"outputEncoding\": \"utf8\",\n    \"safe\": \"off\",\n    \"cx\": \"000001731481601506413:s6vjwyrugku\",\n    \"searchType\": \"image\",\n    \"imgSize\": \"large\"\n   }\n  ]\n },\n \"context\": {\n  \"title\": \"musictest\"\n },\n \"searchInformation\": {\n  \"searchTime\": 0.218049,\n  \"formattedSearchTime\": \"0.22\",\n  \"totalResults\": \"2300000000\",\n  \"formattedTotalResults\": \"2,300,000,000\"\n },\n \"items\": [\n  {\n   \"kind\": \"customsearch#result\",\n   \"title\": \"The Special Grooming Needs of a Senior Cat - Petfinder\",\n   \"htmlTitle\": \"The Special Grooming Needs of a Senior \\u003cb\\u003eCat\\u003c/b\\u003e - Petfinder\",\n   \"link\": \"https://www.petfinder.com/wp-content/uploads/2012/11/140272627-grooming-needs-senior-cat-632x475.jpg\",\n   \"displayLink\": \"www.petfinder.com\",\n   \"snippet\": \"Needs of a Senior Cat\",\n   \"htmlSnippet\": \"Needs of a Senior \\u003cb\\u003eCat\\u003c/b\\u003e\",\n   \"mime\": \"image/jpeg\",\n   \"image\": {\n    \"contextLink\": \"https://www.petfinder.com/cats/cat-grooming/grooming-needs-senior-cat/\",\n    \"height\": 475,\n    \"width\": 632,\n    \"byteSize\": 72413,\n    \"thumbnailLink\": \"https://encrypted-tbn2.gstatic.com/images?q=tbn:ANd9GcTshAJ7eAbNXVHGwLjzP_lpPRPUoD6zd--nVGL-uZWICu2hE_biLFG2M_I\",\n    \"thumbnailHeight\": 103,\n    \"thumbnailWidth\": 137\n   }\n  }\n ]\n}\n";

            string image = (string)JObject.Parse(result)["items"][0]["link"];

            string url = image;
            


            Stream originalStream = await new HttpClient().GetStreamAsync(image);
            MemoryStream memStream = new MemoryStream();
            await originalStream.CopyToAsync(memStream);
            memStream.Position = 0;

            BitmapImage bitmap = new BitmapImage();
            
            await bitmap.SetSourceAsync(memStream.AsRandomAccessStream());

            AlbumImage.Source = bitmap;
            

            await localStorage.Initialize();

            if (localStorage["Music"] == null)
            {
                //first startup, er staan nog geen liedjes in de file
                StorageFolder folder = await AddFolder();
                manager = new MusicManager(await GetSongsFromFolder(folder), this, new List<string>() { folder.Path });
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
            List<string> folders = new List<string>();
            JToken playlistJson = JObject.Parse(localStorage["Music"]);
            int nowPlaying = (int)playlistJson["NowPlaying"];
            int currentList = (int)playlistJson["CurrentList"];
            string sort = (string)playlistJson["SortBy"];
            bool ascending = (bool)playlistJson["Ascending"];
            bool shuffle = (bool)playlistJson["Shuffle"];
            bool repeat = (bool)playlistJson["Repeat"];
            int volume = (int)playlistJson["Volume"];

            JToken folderJson = playlistJson["MusicFolders"];
            foreach (JToken folder in folderJson)
            {
                folders.Add((string)folder);
            }

            string time = (string)playlistJson["StartTime"];
            string[] hms = time.Split(':');

            int h;
            int.TryParse(hms[0], out h);
            int m;
            int.TryParse(hms[1], out m);
            double s;
            double.TryParse(hms[2], out s);

            TimeSpan startTime = new TimeSpan(0, h, m, (int)s, (int)((s % 1) * 1000));
            if (startTime.TotalSeconds < 1)
            {
                startTime = new TimeSpan(0);
            }
            JToken playlistsJson = playlistJson["Playlists"];
            foreach (JToken playlist in playlistsJson)
            {
                songList.Clear();
                string name = (string)playlist["Name"];
                JToken songsJson = playlist["Songs"];
                foreach (JToken song in songsJson)
                {
                    string fullTitle = (string)song["FullTitle"];
                    string path = (string)song["Path"];
                    long dateAdded = (long)song["DateModified"];
                    string niceTitle = (string)song["NiceTitle"];
                    int playCount = (int)song["PlayCount"];
                    songList.Add(new Song(fullTitle, path, dateAdded, playCount, niceTitle));
                }
                playlists.Add(new Playlist(name, songList));
            }
            return new MusicManager(songList, this, folders, playlists, currentList, sort, ascending, nowPlaying, startTime.Ticks, shuffle, repeat, volume);
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

        private void ToggleControls()
        {
            if (Controls.Visibility == Visibility.Visible)
            {
                Controls.Visibility = Visibility.Collapsed;
                SongsList.Visibility = Visibility.Collapsed;
                SeekBar.Visibility = Visibility.Collapsed;
                SongInfo.Visibility = Visibility.Collapsed;
            }
            else
            {
                Controls.Visibility = Visibility.Visible;
                SongsList.Visibility = Visibility.Visible;
                SeekBar.Visibility = Visibility.Visible;
                SongInfo.Visibility = Visibility.Visible;
            }
        }

        bool firstLoad = true;
        private void SongsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = SongsList.SelectedIndex;
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
            if (manager != null)
            {
                manager.SetVolume((int)e.NewValue);
            }
        }

        private void BackgroundPanel_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
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


        private void VolumeSlider_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            Windows.UI.Input.PointerPoint properties = e.GetCurrentPoint((Slider)sender);
            int delta = properties.Properties.MouseWheelDelta;
            manager.SetVolume((int)(mediaElement.Volume * 100) + delta / 20, true);
        }

        private void BackgroundPanel_KeyDown(object sender, KeyRoutedEventArgs e)
        {
        }

        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Space:
                    if (mediaElement.CurrentState == MediaElementState.Playing)
                    {
                        manager.Pause();
                    }
                    else
                    {
                        manager.Play();
                    }
                    break;
                case VirtualKey.Right:
                    manager.Next();
                    break;
                case VirtualKey.Left:
                    manager.Previous();
                    break;
                case VirtualKey.Up:
                    manager.SetVolume((int)(mediaElement.Volume * 100) + 5, true);
                    break;
                case VirtualKey.Down:
                    manager.SetVolume((int)(mediaElement.Volume * 100) - 5, true);
                    break;
                case VirtualKey.Insert:
                    ToggleControls();
                    break;
                default:
                    break;
            }
        }
    }
}