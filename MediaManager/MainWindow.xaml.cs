//#define REBUILD_TREE_ON_EXPAND
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;

using System.Xml;
using System.Xml.Linq;

using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

using System.Collections.Generic;

using System.Text.RegularExpressions;

using Microsoft.WindowsAPICodePack.Dialogs;

namespace MediaManager {

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        #region constant

        private const string NewPlaylistName = "New Playlist";

        private const string PlaylistsTreeViewItemName = "PlaylistsTreeViewItem";

        #endregion

        #region variable

        private readonly string root;

        private readonly MediaPlayer mediaPlayer;

        private readonly LinkedList<MediaDirectory> scopes;

        private readonly LinkedList<Playlist> playlists;

        private readonly TreeView hierarchy;

        private readonly TextBlock contentName;

        private readonly TextBlock contentComment;

        private readonly TextBlock contentCurrentTime;

        private readonly TextBlock contentMaximumTime;

        private readonly Slider contentProgress;

        private readonly Slider contentVolume;

        private readonly Image contentImage;

        private readonly DispatcherTimer updateTimer;

        private MediaFile selectedFile;

        private MediaDirectory selectedDirectory;

        private Playlist selectedPlaylist;

        private double mediaTargetTime;

        private readonly ContextMenu hierarchyScopesContextMenu;

        private readonly ContextMenu hierarchyScopeContextMenu;

        private readonly ContextMenu hierarchyDirectoryContextMenu;

        private readonly ContextMenu hierarchyFileContextMenu;

        private readonly ContextMenu hierarchyPlaylistsContextMenu;

        private readonly ContextMenu hierarchyPlaylistContextMenu;

        private readonly ContextMenu hierarchyPlaylistItemContextMenu;

        private TreeViewItem scopesTreeViewItem;

        private readonly InputBox input;

        private readonly LinkedList<string> expandedHierarchyItems;

        /// <summary>
        /// Tracks if a playlist is favoured over a directory for skipping tracks.
        /// </summary>
        private bool inPlaylist = false;

        #endregion

        #region constructor

        public MainWindow() {

            InitializeComponent();

            string root = Environment.CurrentDirectory.Replace('\\', '/'); // get the path to the root directory
            this.root = root.Length > 0 && root[^1] == '/' ? root : (root + '/');

            scopes = new LinkedList<MediaDirectory>(); //new MediaDirectory(libraryPath, null);
            playlists = new LinkedList<Playlist>();

            // create media player:
            mediaPlayer = new MediaPlayer(
                (MediaElement)FindName("MediaPlayer"),
                OnMediaStarted,
                OnMediaStopped,
                OnMediaChanged
            );

            hierarchy = (TreeView)FindName("LibraryTreeView");
            contentProgress = (Slider)FindName("ContentProgress");
            contentName = (TextBlock)FindName("ContentName");
            contentComment = (TextBlock)FindName("ContentComment");
            contentCurrentTime = (TextBlock)FindName("ContentCurrentTime");
            contentMaximumTime = (TextBlock)FindName("ContentMaximumTime");
            contentVolume = (Slider)FindName("Volume");
            contentImage = (Image)FindName("ContentImage");

            hierarchyScopesContextMenu = (ContextMenu)Resources["HierarchyScopesContextMenu"];
            Debug.Assert(hierarchyScopesContextMenu != null);

            hierarchyScopeContextMenu = (ContextMenu)Resources["HierarchyScopeContextMenu"];
            Debug.Assert(hierarchyScopeContextMenu != null);

            hierarchyDirectoryContextMenu = (ContextMenu)Resources["HierarchyDirectoryContextMenu"];
            Debug.Assert(hierarchyDirectoryContextMenu != null);

            hierarchyFileContextMenu = (ContextMenu)Resources["HierarchyFileContextMenu"];
            Debug.Assert(hierarchyFileContextMenu != null);

            hierarchyPlaylistsContextMenu = (ContextMenu)Resources["HierarchyPlaylistsContextMenu"];
            Debug.Assert(hierarchyPlaylistsContextMenu != null);

            hierarchyPlaylistContextMenu = (ContextMenu)Resources["HierarchyPlaylistContextMenu"];
            Debug.Assert(hierarchyPlaylistContextMenu != null);

            hierarchyPlaylistItemContextMenu = (ContextMenu)Resources["HierarchyPlaylistItemContextMenu"];
            Debug.Assert(hierarchyPlaylistItemContextMenu != null);

            selectedFile = null;
            selectedDirectory = null;
            selectedPlaylist = null;

            scopesTreeViewItem = null;
            expandedHierarchyItems = new LinkedList<string>();

            input = new InputBox((Grid)FindName("InputBox"));

            mediaTargetTime = -1.0;

            updateTimer = new DispatcherTimer();
            updateTimer.Tick += new EventHandler(UpdateTimerElapsed);
            updateTimer.Interval = TimeSpan.FromSeconds(0.1f);

            contentProgress.Value = 0.0f;
            contentVolume.Value = 1.0f;

            LoadConfiguration();
            UpdateHierarchy();

        }

        #endregion

        #region logic

        #region SaveConfiguration

        private void SaveConfiguration() {

            using XmlWriter xmlWriter = XmlWriter.Create(
                root + "config.xml",
                new XmlWriterSettings() {
                    Indent = true
                }
            );

            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("MediaManager");

            #region hierarchy state

            xmlWriter.WriteStartElement("Hierarchy");

            foreach (string item in expandedHierarchyItems) {

                xmlWriter.WriteStartElement("ExpandedItem");
                xmlWriter.WriteString(item);
                xmlWriter.WriteEndElement();

            }

            xmlWriter.WriteEndElement();

            #endregion

            #region scopes
            xmlWriter.WriteStartElement("Scopes");

            LinkedListNode<MediaDirectory> scopeNode = scopes.First;
            while (scopeNode != null) {

                MediaDirectory scope = scopeNode.Value;
                if (scope != null) {

                    xmlWriter.WriteStartElement("Scope");
                    xmlWriter.WriteString(scope.location);
                    xmlWriter.WriteEndElement();

                }
                scopeNode = scopeNode.Next;

            }

            xmlWriter.WriteEndElement();
            #endregion

            #region meta-data
            xmlWriter.WriteStartElement("MetaData");

            LinkedListNode<Playlist> playlistNode = playlists.First; // reuse playlist node since only media in playlists should have their metadata saved
            while (playlistNode != null) {

                Playlist playlist = playlistNode.Value;
                if (playlist != null) {

                    for (int i = 0; i < playlist.Length; i++) {

                        MediaFile mediaFile = playlist[i];
                        if (mediaFile != null) {

                            xmlWriter.WriteStartElement("Media");
                            xmlWriter.WriteAttributeString("GUID", mediaFile.GetGUID());

                            if (!string.IsNullOrEmpty(mediaFile.comment)) {
                                xmlWriter.WriteStartElement("Comment");
                                xmlWriter.WriteString(mediaFile.comment);
                                xmlWriter.WriteEndElement();
                            }

                            if (mediaFile.categories != null && mediaFile.categories.Length > 0) {
                                xmlWriter.WriteStartElement("Categories");
                                xmlWriter.WriteString(mediaFile.CategoriesToString());
                                xmlWriter.WriteEndElement();
                            }

                            if (!string.IsNullOrEmpty(mediaFile.image)) {
                                xmlWriter.WriteStartElement("Image");
                                xmlWriter.WriteString(mediaFile.image);
                                xmlWriter.WriteEndElement();
                            }

                            xmlWriter.WriteEndElement();

                        }

                    }

                }

                playlistNode = playlistNode.Next;

            }

            xmlWriter.WriteEndElement();

            #endregion

            #region playlists
            xmlWriter.WriteStartElement("Playlists");

            playlistNode = playlists.First;
            while (playlistNode != null) {

                Playlist playlist = playlistNode.Value;
                if (playlist != null) {

                    xmlWriter.WriteStartElement("Playlist");
                    xmlWriter.WriteAttributeString("Name", playlist.name);
                    xmlWriter.WriteAttributeString("SortMode", playlist.sortMode.ToString());
                    xmlWriter.WriteAttributeString("Reverse", playlist.reverse ? "True" : "False");
                    for (int i = 0; i < playlist.Length; i++) {

                        MediaFile mediaFile = playlist[i];
                        if (mediaFile != null) {

                            xmlWriter.WriteStartElement("Media");
                            xmlWriter.WriteAttributeString("GUID", mediaFile.GetGUID());
                            xmlWriter.WriteEndElement();

                        }

                    }
                    xmlWriter.WriteEndElement();

                }

                playlistNode = playlistNode.Next;

            }

            xmlWriter.WriteEndElement();
            #endregion

            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();

        }

        #endregion

        #region LoadConfiguration

        private void LoadConfiguration() {

            string location = this.root + "config.xml";
            if (!File.Exists(location)) return; // no configuration exists

            XElement root = XDocument.Parse(File.ReadAllText(location)).Root;
            if (!root.Name.LocalName.Equals("MediaManager")) return; // root node name incorrect

            try {

                #region hierarchy state

                string[] expandedItems = (from item in root.Element("Hierarchy").Elements("ExpandedItem") select item.Value).ToArray();
                foreach (string item in expandedItems) expandedHierarchyItems.AddLast(item);

                #endregion

                #region scopes

                MediaDirectory[] scopeBuffer = (
                    from scope in root.Element("Scopes").Elements("Scope")
                    select new MediaDirectory(scope.Value, null)
                ).ToArray();

                scopes.Clear();
                for (int i = 0; i < scopeBuffer.Length; i++) {

                    MediaDirectory scope = scopeBuffer[i];
                    if (scope != null) {

                        scope.Rescan(true);
                        scopes.AddLast(scope);

                    }

                }

                #endregion

                #region media file metadata

                (string, string, string[], string)[] metaDataBuffer = (
                    from metaData in root.Element("MetaData").Elements("Media")
                    let guid = metaData.Attribute("GUID")
                    let comment = metaData.Element("Comment")
                    let categories = metaData.Element("Categories")
                    let image = metaData.Element("Image")
                    select (
                        guid?.Value,
                        comment?.Value,
                        categories?.Value.Split(','),
                        image?.Value
                    )
                ).ToArray();

                for (int i = 0; i < metaDataBuffer.Length; i++) {

                    (string guid, string comment, string[] categories, string image) metaData = metaDataBuffer[i];
                    MediaFile mediaFile = FindMedia(metaData.guid);
                    if (mediaFile != null) {

                        mediaFile.comment = metaData.comment;

                        string[] categoryBuffer = metaData.categories;
                        if (categoryBuffer != null && categoryBuffer.Length > 0 && !string.IsNullOrWhiteSpace(categoryBuffer[0])) {

                            Category[] categories = new Category[categoryBuffer.Length];
                            for (int j = 0; j < categories.Length; j++) categories[j] = Category.GetCategory(categoryBuffer[j].Trim());
                            mediaFile.categories = categories;

                        }

                        mediaFile.image = metaData.image;

                    }

                }

                #endregion

                #region playlists

                Playlist[] playlistBuffer = (
                    from playlist in root.Element("Playlists").Elements("Playlist")
                    let name = playlist.Attribute("Name")
                    let sortMode = playlist.Attribute("SortMode")
                    let reverse = playlist.Attribute("Reverse")
                    select new Playlist(
                        name?.Value,
                        (MediaSortMode)Enum.Parse(typeof(MediaSortMode), sortMode?.Value),
                        reverse != null && reverse.Value.Equals("true", StringComparison.CurrentCultureIgnoreCase),
                        (
                            from media in playlist.Elements("Media")
                            let guid = media.Attribute("GUID")
                            select guid != null ? FindMedia(guid.Value) : null
                        ).ToArray()
                    )
                ).ToArray();

                playlists.Clear();
                for (int i = 0; i < playlistBuffer.Length; i++) {

                    Playlist playlist = playlistBuffer[i];
                    if (playlist != null) playlists.AddLast(playlist);

                }

                #endregion

            } catch (Exception exception) {

                Trace.WriteLine(exception.Message);

            }

        }

        #endregion

        #region FindMedia

        private MediaFile FindMedia(in string guid) {

            if (guid == null) throw new ArgumentNullException("guid");

            MediaFile mediaFile;
            foreach (MediaDirectory scope in scopes) {

                mediaFile = scope.FindByGUID(guid, true);
                if (mediaFile != null) return mediaFile;

            }

            return null;

        }

        #endregion

        #region UpdateTimerElapsed

        private void UpdateTimerElapsed(object sender, EventArgs e) {

            contentVolume.Value = mediaPlayer.Volume;

            #region timing

            if (mediaTargetTime != -1.0) { // update progress
                mediaPlayer.NormalizedProgress = mediaTargetTime;
                mediaTargetTime = -1.0;
            } else { // progress doesn't need updating
                SetProgress(mediaPlayer.NormalizedProgress); // move to progress of media
            }

            UpdateTime();

            #endregion

        }

        #endregion

        #region OnMediaStarted

        private void OnMediaStarted(in MediaFile media) {

            UpdateMedia(media);

        }

        #endregion

        #region OnMediaStopped

        private void OnMediaStopped(in MediaFile media, in bool success) {

            SetProgress(mediaPlayer.NormalizedProgress);
            UpdateMedia(media);
            Next();

        }

        #endregion

        #region OnMediaChanged

        private void OnMediaChanged(in MediaFile oldMedia, in MediaFile newMedia) {

            UpdateMedia(newMedia);

        }

        #endregion

        #region SetProgress

        private void SetProgress(in double value) {

            contentProgress.Value = value; // set the new progress
            mediaTargetTime = -1.0f; // reset target time (since its changed by the callback from the above property being changed)

        }

        #endregion

        #region UpdateHierarchy

        private void UpdateHierarchy() {

            hierarchy.Items.Clear(); // clear all items
            //scopes.Rescan(); // rescan

            #region scopes

            scopesTreeViewItem = new TreeViewItem() {
                Header = "Scopes",
                ContextMenu = hierarchyScopesContextMenu
            };
            BuildScopesElements(scopesTreeViewItem.Items);
            hierarchy.Items.Add(scopesTreeViewItem);

            RegisterExpandableHierarchyItem(scopesTreeViewItem);

            #endregion

            #region playlists

            TreeViewItem playlistTreeViewItem = new TreeViewItem() {
                Name = PlaylistsTreeViewItemName,
                Header = "Playlists",
                ContextMenu = hierarchyPlaylistsContextMenu
            };
            BuildPlaylistElements(playlistTreeViewItem.Items);
            hierarchy.Items.Add(playlistTreeViewItem);

            RegisterExpandableHierarchyItem(playlistTreeViewItem);

            #endregion

        }

        #endregion

        #region RegisterExpandableHierarchyItem

        private void RegisterExpandableHierarchyItem(in TreeViewItem treeViewItem) {

            if (treeViewItem == null) throw new ArgumentNullException("treeViewItem");

            treeViewItem.IsExpanded = CheckExpanded(treeViewItem);
            treeViewItem.Expanded += RegisterHierarchyItemExpanded;
            treeViewItem.Collapsed += RegisterHierarchyItemCollapsed;

        }

        #endregion

        #region CheckExpanded

        private bool CheckExpanded(in TreeViewItem treeViewItem) => expandedHierarchyItems.Contains(GetTreeViewGUID(treeViewItem));

        #endregion

        #region GetTreeViewGUID

        private string GetTreeViewGUID(TreeViewItem treeViewItem) {

            if (treeViewItem == null) throw new ArgumentNullException("treeViewItem");

            int guid = 0;

            while (treeViewItem != null) {

                guid = unchecked(guid + treeViewItem.Header.ToString().GetIntGUID());
                treeViewItem = treeViewItem.Parent as TreeViewItem;

            }

            return Convert.ToString(guid, 16);

        }

        #endregion

        #region RegisterHierarchyItemExpanded

        private void RegisterHierarchyItemExpanded(object sender, RoutedEventArgs e) {

            if (sender is TreeViewItem treeViewItem) {

                string guid = GetTreeViewGUID(treeViewItem);
                if (!expandedHierarchyItems.Contains(guid)) expandedHierarchyItems.AddLast(guid);

            }

        }

        #endregion

        #region RegisterHierarchyItemCollapsed

        private void RegisterHierarchyItemCollapsed(object sender, RoutedEventArgs e) {

            if (sender is TreeViewItem treeViewItem) expandedHierarchyItems.Remove(GetTreeViewGUID(treeViewItem));

        }

        #endregion

        #region GetScopeHeader

        private static string GetScopeHeader(in MediaDirectory scope, in int maxCharacters = 16) {

            if (scope == null) throw new ArgumentNullException("scope");
            if (maxCharacters <= 3) throw new ArgumentOutOfRangeException("maxCharacters");

            string name = scope.location;
            return name.Length > maxCharacters ? "..." + name[(name.Length - maxCharacters + 3)..] : name;

        }

        #endregion

        #region BuildScopesElements

        private void BuildScopesElements(in ItemCollection items) {

            items.Clear();
            LinkedListNode<MediaDirectory> node = scopes.First;
            while (node != null) {

                MediaDirectory directory = node.Value;
                if (directory != null) {

                    directory.Rescan(true);
                    BuildScopeElement(items, directory);

                }

                node = node.Next;

            }

        }

        #endregion

        #region BuildScopeElement

        private void BuildScopeElement(in ItemCollection items, in MediaDirectory directory) {

            //if (items == null) throw new ArgumentNullException("items");
            //if (directory == null) throw new ArgumentNullException("directory");

            TreeViewItem treeViewItem = new TreeViewItem() {
                Header = GetScopeHeader(directory),
                Tag = directory,
                ContextMenu = hierarchyScopeContextMenu
            };
            BuildHierarchyElements(treeViewItem.Items, directory);
            items.Add(treeViewItem);

            RegisterExpandableHierarchyItem(treeViewItem);

        }

        #endregion

        #region BuildPlaylistElements

        private void BuildPlaylistElements(in ItemCollection items) {

            LinkedListNode<Playlist> node = playlists.First;
            while (node != null) {

                Playlist playlist = node.Value;
                if (playlist != null) {

                    TreeViewItem treeViewItem = new TreeViewItem() {
                        Header = playlist.name,
                        Tag = playlist,
                        ContextMenu = hierarchyPlaylistContextMenu
                    };
                    treeViewItem.Selected += OnPlaylistSelected;
                    BuildPlaylistItems(treeViewItem.Items, playlist);
                    items.Add(treeViewItem);

                    RegisterExpandableHierarchyItem(treeViewItem);

                }

                node = node.Next;

            }

        }

        #endregion

        #region BuildPlaylistItems

        private void BuildPlaylistItems(in ItemCollection items, in Playlist playlist) {

            for (int i = 0; i < playlist.Length; i++) {

                MediaFile mediaFile = playlist[i];
                if (mediaFile != null) {

                    TreeViewItem treeViewItem = new TreeViewItem() {
                        Header = mediaFile.name,
                        Tag = mediaFile,
                        ContextMenu = hierarchyPlaylistItemContextMenu
                    };
                    treeViewItem.Selected += OnFileSelected;
                    treeViewItem.Selected += OnPlaylistItemSelected;
                    items.Add(treeViewItem);


                }

            }

        }

        #endregion

        #region BuildHierarchyElements

        private void BuildHierarchyElements(in ItemCollection items, in MediaDirectory directory) {

            if (items == null) throw new ArgumentNullException("items");
            if (directory == null) throw new ArgumentNullException("directory");

            MediaDirectory[] directories = directory.Directories;
            for (int i = 0; i < directories.Length; i++) BuildDirectoryHierarchyElement(items, directories[i]);

            MediaFile[] files = directory.Files;

            MediaFile file;
            for (int i = 0; i < files.Length; i++) {
                if ((file = files[i]).type != MediaFileType.Unknown) BuildFileHierarchyElement(items, file);
            }

        }

        #endregion

        #region BuildDirectoryHierarchyElement

        private void BuildDirectoryHierarchyElement(in ItemCollection items, in MediaDirectory directory) {

            TreeViewItem treeViewItem = new TreeViewItem() {
                Header = directory.name,
                Tag = directory,
                ContextMenu = hierarchyDirectoryContextMenu
            };
            treeViewItem.Selected += OnDirectorySelected;
#if REBUILD_TREE_ON_EXPAND
            treeViewItem.Expanded += OnDirectoryExpanded;
#endif
            BuildHierarchyElements(treeViewItem.Items, directory);
            items.Add(treeViewItem);

            RegisterExpandableHierarchyItem(treeViewItem);

        }

        #endregion

        #region BuildFileHierarchyElement

        private void BuildFileHierarchyElement(in ItemCollection items, in MediaFile file) {

            TreeViewItem treeViewItem = new TreeViewItem() {
                Header = file.name,
                Tag = file,
                ContextMenu = hierarchyFileContextMenu
            };
            treeViewItem.Selected += OnFileSelected;

            items.Add(treeViewItem);

        }

        #endregion

        #region OnDirectoryExpanded
#if REBUILD_TREE_ON_EXPAND
        private void OnDirectoryExpanded(object sender, RoutedEventArgs e) {

            TreeViewItem treeViewItem = (TreeViewItem)sender;

            MediaDirectory directory = (MediaDirectory)treeViewItem.Tag;
            directory.Rescan(true);

            ItemCollection items = treeViewItem.Items;
            items.Clear();

            BuildHierarchyElements(items, directory);

        }
#endif
        #endregion

        #region OnDirectorySelected

        private void OnDirectorySelected(object sender, RoutedEventArgs e) => SelectDirectory((MediaDirectory)((TreeViewItem)sender).Tag);

        #endregion

        #region SelectDirectory

        /// <summary>
        /// Selects a directory as the current selected directory.
        /// </summary>
        /// <param name="directory"></param>
        private void SelectDirectory(in MediaDirectory directory) {

            selectedDirectory = directory ?? throw new ArgumentNullException("directory");
            inPlaylist = false;

        }

        #endregion

        #region SelectPlaylist

        private void SelectPlaylist(in Playlist playlist) {

            selectedPlaylist = playlist ?? throw new ArgumentNullException("playlist");
            inPlaylist = true;

        }

        #endregion

        #region OnFileSelected

        private void OnFileSelected(object sender, RoutedEventArgs e) => SelectFile((MediaFile)((TreeViewItem)sender).Tag);

        #endregion

        #region OnPlaylistSelected

        private void OnPlaylistSelected(object sender, RoutedEventArgs e) => SelectPlaylist((Playlist)((TreeViewItem)sender).Tag);

        #endregion

        #region OnPlaylistItemSelected

        private void OnPlaylistItemSelected(object sender, RoutedEventArgs e) => SelectPlaylist((Playlist)((TreeViewItem)((TreeViewItem)sender).Parent).Tag);

        #endregion

        #region SelectFile

        /// <summary>
        /// Selects a media file.
        /// </summary>
        /// <param name="file">Media file to select.</param>
        private void SelectFile(in MediaFile file) {

            selectedFile = file ?? throw new ArgumentNullException("file");
            SelectDirectory(file.parent); // select the parent directory

            UpdateMedia(file);
            mediaPlayer.Stop();
            SetPlayPauseButtonContent(true);

            mediaPlayer.RenderPreview(file.image != null ? new Uri(file.image) : file.location);

        }

        /// <summary>
        /// Selects a media file from a media directory.
        /// </summary>
        /// <param name="directory">Media directory to select a file from.</param>
        private void SelectFile(in MediaDirectory directory) {

            if (directory == null) throw new ArgumentNullException("directory");
            if (directory.Files.Length > 0) selectedFile = directory.Files[0];
            selectedFile = null; // no file found, in the future add a recursive option to find a file in the child directories

            UpdateMedia(selectedFile);
            mediaPlayer.Stop();
            SetPlayPauseButtonContent(true);

        }

        #endregion

        #region Window_Loaded

        private void Window_Loaded(object sender, RoutedEventArgs e) {

            updateTimer.Start();

        }

        #endregion

        #region Window_Closed

        private void Window_Closed(object sender, EventArgs e) {

            SaveConfiguration();

        }

        #endregion

        #region GetCurrentFile

        /// <summary>
        /// Gets the current file that should be playing.
        /// </summary>
        private MediaFile GetCurrentFile() {

            if (selectedFile == null && selectedDirectory != null) SelectFile(selectedDirectory);
            return selectedFile;

        }

        #endregion

        #region UpdateMedia

        private void UpdateMedia(MediaFile media) {

            if (media == null) {

                contentName.Text = string.Empty;
                contentComment.Text = string.Empty;
                contentProgress.Value = 0.0f;
                contentImage.Source = null;

            } else {

                contentName.Text = media.name;
                contentComment.Text = media.comment ?? string.Empty;
                contentProgress.Value = mediaPlayer.NormalizedProgress;

                if (media.image != null) {

                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(media.image, UriKind.Absolute);
                    image.EndInit();
                    contentImage.Source = image;

                } else contentImage.Source = null;

            }

            contentVolume.Value = mediaPlayer.Volume;

            UpdateTime();

        }

        #endregion

        #region UpdateTime

        private void UpdateTime() {

            contentCurrentTime.Text = FormatTime(mediaPlayer.Progress);
            contentMaximumTime.Text = FormatTime(mediaPlayer.Duration);

        }

        #endregion

        #region FormatTime

        private static string FormatTime(in double time) {

            int mins = (int)Math.Floor(time / 60.0);
            int seconds = (int)Math.Floor(time - (mins * 60));
            return string.Format("{0:00}:{1:00}", mins, seconds);

        }

        #endregion

        #region PlayPauseButton_Click

        /// <summary>
        /// Called when the play pause button is pressed.
        /// </summary>
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e) {

            selectedFile = GetCurrentFile();
            if (selectedFile == null) { // no media found
                SetPlayPauseButtonContent(true); // play
                return; // stop here
            }

            if (mediaPlayer.IsPlaying && mediaPlayer.CurrentMedia == selectedFile) { // playing, therefore pause

                mediaPlayer.Pause();
                SetPlayPauseButtonContent(true); // pause
                return;

            }

            SetPlayPauseButtonContent(false); // play
            mediaPlayer.Play(selectedFile);
            UpdateMedia(selectedFile);

        }

        #endregion

        #region SetPlayPauseButtonContent

        private void SetPlayPauseButtonContent(in bool state) {

            TextBlock textBlock = (TextBlock)FindName("PlayPauseButtonContent");
            if (state) {

                textBlock.Text = "\u25B6";
                textBlock.Margin = new Thickness(3.0, 0.0, 0.0, 1.0);
                textBlock.FontSize = 16.0;

            } else {

                textBlock.Text = "\u23F8";
                textBlock.Margin = new Thickness(1.0, 0.0, 0.0, 4.0);
                textBlock.FontSize = 19.0;

            }

        }

        #endregion

        #region BackButton_Click

        /// <summary>
        /// Called when the back button is pressed.
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e) => Back();

        #endregion

        #region Back

        private void Back() {

            if (selectedFile == null) return; // cannot move to next track if there is no selected file

            if (inPlaylist && selectedPlaylist != null) { // next in playlist

                int index = selectedPlaylist[selectedFile] - 1;
                if (index == -2) index = 0;
                else if (index < 0) index = selectedPlaylist.Length - 1;
                selectedFile = selectedPlaylist[index];

            } else if (selectedDirectory != null) { // next in directory

                int index = selectedDirectory[selectedFile] - 1;
                if (index == -2) index = 0;
                else if (index < 0) index = selectedDirectory.Length - 1;
                selectedFile = selectedDirectory[index];

            }

            if (selectedFile != null) {

                mediaPlayer.Play(selectedFile);
                UpdateMedia(selectedFile);

            } else mediaPlayer.Stop();

        }

        #endregion

        #region NextButton_Click

        /// <summary>
        /// Called when the next button is pressed.
        /// </summary>
        private void NextButton_Click(object sender, RoutedEventArgs e) => Next();

        #endregion

        #region Next

        private void Next() {

            if (selectedFile == null) return; // cannot move to next track if there is no selected file

            if (inPlaylist && selectedPlaylist != null) { // next in playlist

                int index = selectedPlaylist[selectedFile] + 1;
                if (index >= selectedPlaylist.Length) index = 0;
                selectedFile = selectedPlaylist[index];

            } else if (selectedDirectory != null) { // next in directory

                int index = selectedDirectory[selectedFile] + 1;
                if (index >= selectedDirectory.Length) index = 0;
                selectedFile = selectedDirectory[index];

            }

            if (selectedFile != null) {

                mediaPlayer.Play(selectedFile);
                UpdateMedia(selectedFile);

            } else mediaPlayer.Stop();

        }

        #endregion

        #region Volume_ValueChanged

        private void Volume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {

            if (mediaPlayer == null) return;
            mediaPlayer.Volume = e.NewValue;

        }

        #endregion

        #region ContentProgress_ValueChanged

        private void ContentProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {

            if (mediaPlayer == null) return;
            mediaTargetTime = e.NewValue;
            //UpdateTime();

        }

        #endregion

        #region Hierarchy_PreviewMouseWheel

        private void Hierarchy_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {

            ScrollViewer scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;

        }

        #endregion

        #region Hierarchy_Reload

        private void Hierarchy_Reload(object sender, RoutedEventArgs e) => UpdateHierarchy();

        #endregion

        #region Hierarchy_AddMediaToPlaylist

        private void Hierarchy_AddMediaToPlaylist(object sender, RoutedEventArgs e) {

            if (selectedPlaylist == null) return;

            TreeViewItem treeViewItem = GetContextMenuTarget<TreeViewItem>(sender);
            if (treeViewItem == null) return;

            MediaFile mediaFile = (MediaFile)treeViewItem.Tag;
            if (mediaFile == null) return;
            
            selectedPlaylist.Add(mediaFile);
            SaveConfiguration();
            ReloadPlaylists();

        }

        #endregion
        
        #region Hierarchy_EditMediaCategories

        private void Hierarchy_EditMediaCategories(object sender, RoutedEventArgs e) {

            TreeViewItem treeViewItem = GetContextMenuTarget<TreeViewItem>(sender);
            if (treeViewItem == null) return;

            if (treeViewItem.Tag is MediaFile mediaFile) {

                input.AskQuestion(
                    OnUpdateMediaCategories,
                    "Categories",
                    mediaFile.CategoriesToString(),
                    mediaFile,
                    true
                );

            }

        }

        #endregion

        #region OnUpdateMediaCategories

        private void OnUpdateMediaCategories(in string value, in bool cancelled, in object data) {

            if (cancelled) return;

            if (data is MediaFile mediaFile) {

                string[] elements = value.Split(',');
                if (elements.Length == 0 || string.IsNullOrWhiteSpace(elements[0])) return; // no categories entered
                Category[] categories = new Category[elements.Length];
                for (int i = 0; i < elements.Length; i++) categories[i] = Category.GetCategory(elements[i].Trim());
                mediaFile.categories = categories;
                SaveConfiguration();

            }

        }

        #endregion

        #region Hierarchy_EditMediaComment

        private void Hierarchy_EditMediaComment(object sender, RoutedEventArgs e) {

            TreeViewItem treeViewItem = GetContextMenuTarget<TreeViewItem>(sender);
            if (treeViewItem == null) return;

            if (treeViewItem.Tag is MediaFile mediaFile) {

                input.AskQuestion(
                    OnUpdateMediaComment,
                    "Comment",
                    mediaFile.comment,
                    mediaFile,
                    true
                );

            }

        }

        #endregion

        #region OnUpdateMediaComment

        private void OnUpdateMediaComment(in string value, in bool cancelled, in object data) {

            if (cancelled) return;

            if (data is MediaFile mediaFile) {
                mediaFile.comment = value;
                SaveConfiguration();
                if (selectedFile == mediaFile) UpdateMedia(selectedFile);
            }

        }

        #endregion

        #region Hierarchy_ReloadPlaylists

        private void Hierarchy_ReloadPlaylists(object sender, RoutedEventArgs e) => ReloadPlaylists();

        #endregion

        #region ReloadPlaylists

        private void ReloadPlaylists() {

            TreeViewItem treeViewItem = (TreeViewItem)FindName(PlaylistsTreeViewItemName);
            if (treeViewItem != null) {
                treeViewItem.Items.Clear();
                BuildPlaylistElements(treeViewItem.Items);
            } else {
                UpdateHierarchy();
            }

        }

        #endregion

        #region Hierarchy_CreatePlaylist

        private void Hierarchy_CreatePlaylist(object sender, RoutedEventArgs e) {

            Playlist playlist = new Playlist(NewPlaylistName);
            playlists.AddLast(playlist);
            SaveConfiguration();
            ReloadPlaylists();
            selectedPlaylist = playlist;

        }

        #endregion

        #region Hierarchy_RenamePlaylist

        private void Hierarchy_RenamePlaylist(object sender, RoutedEventArgs e) {

            TreeViewItem treeViewItem = GetContextMenuTarget<TreeViewItem>(sender);
            if (treeViewItem == null) return;

            if (treeViewItem.Tag is Playlist playlist) {

                input.AskQuestion(
                    OnRenamePlaylist,
                    "Playlist Name",
                    playlist.name,
                    new object[] { playlist, treeViewItem },
                    true
                );

            }

        }

        #endregion

        #region OnRenamePlaylist

        private void OnRenamePlaylist(in string value, in bool cancelled, in object data) {

            if (cancelled) return;

            if (data is object[] args && args.Length == 2 && args[0] is Playlist playlist && args[1] is TreeViewItem treeViewItem) {

                string name = value.Trim(); // get the target name

                // validate name:
                if (!Regex.IsMatch(name, @"[a-zA-Z_][a-zA-Z0-9_\- ]*")) return; // invalid name
                foreach (Playlist p in playlists) { if (playlist != p && name.Equals(p.name)) return; } // check for duplicates (don't allow duplicates)

                playlist.name = name;
                treeViewItem.Header = name;
                selectedPlaylist = playlist;
                SaveConfiguration();

            }

        }

        #endregion

        #region GetContextMenuTarget

        private static T GetContextMenuTarget<T>(in object sender) where T : UIElement
            => sender is MenuItem menuItem && menuItem.CommandParameter is ContextMenu contextMenu
                ? contextMenu.PlacementTarget as T
                : null;

        #endregion

        #region Hierarchy_ReloadScope

        private void Hierarchy_ReloadScope(object sender, RoutedEventArgs e) {

            TreeViewItem treeViewItem = GetContextMenuTarget<TreeViewItem>(sender);
            if (treeViewItem == null) return;

            if (treeViewItem.Tag is MediaDirectory mediaDirectory) {

                mediaDirectory.Rescan();
                treeViewItem.Items.Clear();
                BuildHierarchyElements(treeViewItem.Items, mediaDirectory);

            }

        }

        #endregion

        #region Hierarchy_RemoveScope

        private void Hierarchy_RemoveScope(object sender, RoutedEventArgs e) {

            TreeViewItem treeViewItem = GetContextMenuTarget<TreeViewItem>(sender);
            if (treeViewItem == null) return;

            if (treeViewItem.Tag is MediaDirectory mediaDirectory) {

                scopes.Remove(mediaDirectory);
                SaveConfiguration();
                ((TreeViewItem)treeViewItem.Parent).Items.Remove(treeViewItem);

            }

        }

        #endregion

        #region Hierarchy_RemovePlaylist

        private void Hierarchy_RemovePlaylist(object sender, RoutedEventArgs e) {

            TreeViewItem treeViewItem = GetContextMenuTarget<TreeViewItem>(sender);
            if (treeViewItem == null) return;

            Playlist playlist = (Playlist)treeViewItem.Tag;
            if (playlist == null) return;

            playlists.Remove(playlist);
            SaveConfiguration();
            ((TreeViewItem)treeViewItem.Parent).Items.Remove(treeViewItem); // remove from the playlists tree view

        }

        #endregion

        #region Hierarchy_AddScope

        private void Hierarchy_AddScope(object sender, RoutedEventArgs e) {

            CommonOpenFileDialog commonOpenFileDialog = new CommonOpenFileDialog() {
                InitialDirectory = root,
                EnsureValidNames = true,
                IsFolderPicker = true
            };

            if (commonOpenFileDialog.ShowDialog() == CommonFileDialogResult.Ok) {

                string scope = commonOpenFileDialog.FileName.Replace('\\', '/');

                #region existance check

                LinkedListNode<MediaDirectory> node = scopes.First;
                while (node != null) {

                    MediaDirectory directory = node.Value;
                    if (directory != null && scope.Equals(directory.location)) { // already exists

                        SelectDirectory(directory);
                        return;

                    }

                    node = node.Next;

                }

                #endregion

                MediaDirectory newScope = new MediaDirectory(scope, null);
                scopes.AddLast(newScope);
                
                SaveConfiguration();
                SelectDirectory(newScope);

                newScope.Rescan(true);
                BuildScopeElement(scopesTreeViewItem.Items, newScope);

            }

        }

        #endregion

        #region Hierarchy_ReloadPlaylist

        private void Hierarchy_ReloadPlaylist(object sender, RoutedEventArgs e) {

            TreeViewItem treeViewItem = GetContextMenuTarget<TreeViewItem>(sender);
            if (treeViewItem == null) return;

            if (treeViewItem.Tag is Playlist playlist) {

                treeViewItem.Items.Clear();
                BuildPlaylistItems(treeViewItem.Items, playlist);

            }

        }

        #endregion

        #region Hierarchy_ReloadDirectory

        private void Hierarchy_ReloadDirectory(object sender, RoutedEventArgs e) {

            TreeViewItem treeViewItem = GetContextMenuTarget<TreeViewItem>(sender);
            if (treeViewItem == null) return;

            if (treeViewItem.Tag is MediaDirectory directory) {

                treeViewItem.Items.Clear();
                BuildHierarchyElements(treeViewItem.Items, directory);

            }

        }

        #endregion

        #region Hierarchy_RemoveFromPlaylist

        private void Hierarchy_RemoveFromPlaylist(object sender, RoutedEventArgs e) {

            TreeViewItem treeViewItem = GetContextMenuTarget<TreeViewItem>(sender);
            if (treeViewItem == null) return;

            if (treeViewItem.Tag is MediaFile file && treeViewItem.Parent is TreeViewItem parentTreeViewItem && parentTreeViewItem.Tag is Playlist playlist) {

                playlist.Remove(file);
                SaveConfiguration();
                ItemCollection items = parentTreeViewItem.Items;
                items.Clear();
                BuildPlaylistItems(items, playlist);

            }

        }

        #endregion

        #region Hierarchy_BindMediaImage

        private void Hierarchy_BindMediaImage(object sender, RoutedEventArgs e) {

            TreeViewItem treeViewItem = GetContextMenuTarget<TreeViewItem>(sender);
            if (treeViewItem == null) return;

            if (treeViewItem.Tag is MediaFile file) {

                CommonOpenFileDialog commonOpenFileDialog = new CommonOpenFileDialog() { // open file explorer
                    InitialDirectory = Path.GetDirectoryName(file.location.AbsolutePath),
                    EnsureValidNames = true,
                    IsFolderPicker = false // select files
                };

                if (commonOpenFileDialog.ShowDialog() == CommonFileDialogResult.Ok) { // a file was selected
                    
                    string image = commonOpenFileDialog.FileName.Replace('\\', '/'); // format path to file
                    if (!File.Exists(image)) return; // check the file exists

                    file.image = image; // assign the image (from the formatted path)
                    SaveConfiguration(); // save the configuration of the media manager since it has changed
                    if (selectedFile == file) UpdateMedia(selectedFile); // if the file that was modified is the selected file update the UI

                }

            }

        }

        #endregion

        #endregion

    }

}
