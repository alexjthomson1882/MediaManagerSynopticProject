using System;
using System.Diagnostics;
using System.IO;

namespace MediaManager {

    public sealed class MediaDirectory : IIdentifiable {

        #region variable

        public readonly string name;

        public readonly string location;

        public readonly MediaDirectory parent;

        private MediaDirectory[] directories;

        private MediaFile[] files;

        private MediaSortMode mediaSortMode;

        private bool reverse;

        #endregion

        #region property

        public MediaFile this[int index] => files.Length == 0 ? null : files[reverse ? files.Length - 1 - index : index];

        public int this[in MediaFile mediaFile] {

            get {

                for (int i = 0; i < files.Length; i++) {
                    if (files[i] == mediaFile) return i;
                }
                return -1;

            }

        }

        public int Length => files.Length;

        public MediaDirectory[] Directories => directories;

        public MediaFile[] Files => files;

        public MediaSortMode SortingMode {

            get => mediaSortMode;
            set {

                if (mediaSortMode == value) return;

                mediaSortMode = value;
                RescanFiles();

            }

        }

        #endregion

        #region constructor

        public MediaDirectory(in string location, in MediaDirectory parent) {

            if (location == null) throw new ArgumentNullException("location");
            if (!Directory.Exists(location)) throw new DirectoryNotFoundException(location);
            this.location = location;

            name = Path.GetFileName(location); // find the name of the directory
            this.parent = parent;

            directories = new MediaDirectory[0];
            files = new MediaFile[0];

            mediaSortMode = MediaSortMode.None;
            reverse = false;

            //Rescan();

        }

        #endregion

        #region logic

        #region GetGUID

        public string GetGUID() => location.GetGUID();

        #endregion

        #region FindByLocation

        /// <summary>
        /// Finds a media file by location.
        /// </summary>
        /// <param name="location">Absolute location of media.</param>
        /// <param name="recursive">When true, all sub-directories are searched.</param>
        /// <returns>Found MediaFile instance or null if none is found.</returns>
        public MediaFile FindByLocation(in string location, in bool recursive = false) {

            if (location == null) throw new ArgumentNullException("path");
            return LocationSearch(location.ToLower(), recursive);

        }

        #endregion

        #region LocationSearch

        /// <summary>
        /// Finds a media file that matches a path.
        /// This doesnt check if path is not null and doesn't convert path to lowercase, it assumes it already matches both criteria.
        /// </summary>
        private MediaFile LocationSearch(in string path, in bool recursive) {

            MediaFile file;
            for (int i = 0; i < files.Length; i++) {

                file = files[i];
                if (path.Equals(file.location.AbsolutePath.ToLower())) return file;

            }

            if (recursive) {

                for (int i = 0; i < directories.Length; i++) {

                    file = directories[i].LocationSearch(path, recursive);
                    if (file != null) return file;

                }

            }

            return null; // failed to find the file

        }

        #endregion

        #region FindByGUID

        public MediaFile FindByGUID(in string guid, in bool recursive = false) {

            if (guid == null) throw new ArgumentNullException("guid");
            return GUIDSearch(guid, recursive);

        }

        #endregion

        #region GUIDSearch

        private MediaFile GUIDSearch(in string guid, in bool recursive) {

            MediaFile file;
            for (int i = 0; i < files.Length; i++) {

                file = files[i];
                if (guid.Equals(file.GetGUID())) return file;

            }

            if (recursive) {

                for (int i = 0; i < directories.Length; i++) {

                    file = directories[i].GUIDSearch(guid, recursive);
                    if (file != null) return file;

                }

            }

            return null;

        }

        #endregion

        #region Rescan

        /// <summary>
        /// Rescans the directory for changes.
        /// </summary>
        public void Rescan(bool recursive = true) {

            try {

                RescanDirectories(recursive);
                RescanFiles();

            } catch (Exception exception) {

                Trace.WriteLine(exception.Message);

            }

        }

        #endregion

        #region RescanDirectories

        private void RescanDirectories(bool recursive) {

            string[] directoryPaths = Directory.GetDirectories(location); // get the paths of all the directories
            int targetDirectoryCount = directoryPaths.Length;

            bool[] existingDirectoryFoundFlags = new bool[directories.Length]; // tracks the directories that were found, this identifies directories that have been removed
            MediaDirectory[] directoryBuffer = new MediaDirectory[targetDirectoryCount]; // array to store the new (and old) media directories

            // process directoryPaths:
            MediaDirectory directory; // used for making temporary references
            for (int i = 0; i < targetDirectoryCount; i++) { // iterate each new directory

                string location = directoryPaths[i].Replace('\\', '/'); // format the path of the directory
                bool found = false; // track if the directory has been found (an instance with the same location already exists)

                for (int j = 0; j < directories.Length; j++) { // iterate each existing directory

                    directory = directories[j]; // create a reference to the current directory
                    if (location.Equals(directory.location)) { // directory instance already exists

                        // update:
                        directoryBuffer[i] = directory; // assign to directory buffer
                        if (recursive) directory.Rescan(true); // rescan recursivly

                        // mark as found:
                        existingDirectoryFoundFlags[j] = true;
                        found = true;

                        break; // stop here

                    }

                }

                if (!found) {
                    directory = new MediaDirectory(location, this); // create new media directory since an existing one wasn't found
                    directory.Rescan(recursive); // rescan recursivly
                    directoryBuffer[i] = directory;
                }

            }

            Array.Sort(directoryBuffer, (x, y) => string.Compare(x.name, y.name)); // sort in alphanumeric order

            // remove old directories:
            for (int i = 0; i < existingDirectoryFoundFlags.Length; i++) { // iterate existing directory found flags
                if (!existingDirectoryFoundFlags[i]) directories[i].Dispose(); // directory was not found and therefore needs to be removed
            }

            directories = directoryBuffer; // re-assign directories

        }

        #endregion

        #region RescanFiles

        private void RescanFiles() {

            string[] filePaths = Directory.GetFiles(location); // get all matching files
            int targetFileCount = filePaths.Length;

            bool[] existingFileFoundFlags = new bool[files.Length]; // tracks the files that were found, this identifies files that have been removed
            MediaFile[] fileBuffer = new MediaFile[targetFileCount]; // array to store the new (and old) media files

            // process filePaths:
            MediaFile file;
            for (int i = 0; i < targetFileCount; i++) {

                string location = filePaths[i].Replace('\\', '/'); // format the path of the file
                bool found = false; // track if the file has been found

                for (int j = 0; j < files.Length; j++) { // iterate existing files

                    file = files[j];
                    if (location.Equals(file.location)) {

                        fileBuffer[j] = file;
                        existingFileFoundFlags[j] = true;
                        found = true;
                        break;
                    
                    }

                }

                if (!found) fileBuffer[i] = new MediaFile(location, this);

            }

            switch (mediaSortMode) {

                case MediaSortMode.Name: {

                    Array.Sort(fileBuffer, (x, y) => reverse ? string.Compare(y.name, x.name) : string.Compare(x.name, y.name)); // sort in alphanumeric order
                    break;

                }

                case MediaSortMode.ModifiedDate: {

                    Array.Sort(
                        fileBuffer,
                        (x, y) => (int)(
                            reverse
                                ? y.fileInfo.LastWriteTimeUtc.ToFileTimeUtc() - x.fileInfo.LastWriteTimeUtc.ToFileTimeUtc()
                                : x.fileInfo.LastWriteTimeUtc.ToFileTimeUtc() - y.fileInfo.LastWriteTimeUtc.ToFileTimeUtc()
                        )
                    );
                    break;

                }

            }

            // remove old files:
            for (int i = 0; i < existingFileFoundFlags.Length; i++) { // iterate existing file found flags
                if (!existingFileFoundFlags[i]) files[i].Dispose(); // file was not found and therefore needs to be removed
            }

            files = fileBuffer; // re-assign files

        }

        #endregion

        #region Dispose

        /// <summary>
        /// Called when the directory is removed.
        /// </summary>
        private void Dispose() {

        }

        #endregion

        #endregion

    }

}