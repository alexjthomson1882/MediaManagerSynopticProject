using System;

namespace MediaManager {

    public sealed class Playlist {

        #region variable

        public string name;

        public MediaSortMode sortMode;

        public bool reverse;

        private MediaFile[] mediaFiles;

        #endregion

        #region property

        public int Length => mediaFiles.Length;

        public MediaFile this[int index] => mediaFiles[index];

        /// <summary>
        /// Finds the index of a media file by reference.
        /// </summary>
        /// <param name="mediaFile">Media file to find.</param>
        /// <returns>Index of the media file if found, otherwise -1.</returns>
        public int this[MediaFile mediaFile] {

            get {

                if (mediaFile == null) throw new ArgumentNullException("mediaFile");
                for (int i = 0; i < mediaFiles.Length; i++) {

                    if (mediaFile == mediaFiles[i]) return i;

                }

                return -1;

            }

        }

        #endregion

        #region constructor

        public Playlist(in string name, in MediaSortMode sortMode = MediaSortMode.Name, in bool reverse = false, in MediaFile[] mediaFiles = null) {

            this.name = name ?? throw new ArgumentNullException("name");
            this.sortMode = sortMode;
            this.reverse = reverse;
            //this.mediaFiles = mediaFiles ?? new MediaFile[0];

            #region media files

            if (mediaFiles != null) { // mediaFiles argument provided, check for null references and remove from array

                int nullCount = 0;
                for (int i = 0; i < mediaFiles.Length; i++) {
                    if (mediaFiles[i] == null) nullCount++;
                }

                if (nullCount == 0) this.mediaFiles = mediaFiles;
                else {

                    this.mediaFiles = new MediaFile[mediaFiles.Length - nullCount];
                    nullCount = 0;
                    MediaFile mediaFile;
                    for (int i = 0; i < mediaFiles.Length; i++) {

                        mediaFile = mediaFiles[i];
                        if (mediaFile != null) this.mediaFiles[nullCount++] = mediaFiles[i];

                    }

                }

            } else this.mediaFiles = new MediaFile[0];

            #endregion

        }

        #endregion

        #region Add

        public void Add(in MediaFile mediaFile) {

            if (mediaFile == null) throw new ArgumentNullException("mediaFile");
            int index = this[mediaFile];
            if (index != -1) return; // already added
            int length = mediaFiles.Length;
            MediaFile[] newMediaFiles = new MediaFile[length + 1];
            Array.Copy(mediaFiles, newMediaFiles, length);
            newMediaFiles[length] = mediaFile;
            mediaFiles = newMediaFiles;

        }

        #endregion

        #region Remove

        public void Remove(in MediaFile mediaFile) {

            if (mediaFile == null) throw new ArgumentNullException("mediaFile");

            int index;
            while ((index = this[mediaFile]) != -1) {

                int length = mediaFiles.Length;
                MediaFile[] newMediaFiles = new MediaFile[length - 1];
                if (index > 0) Array.Copy(mediaFiles, newMediaFiles, index);
                if (index < length) Array.Copy(mediaFiles, index + 1, newMediaFiles, index, length - index - 1);
                mediaFiles = newMediaFiles;

            }

        }

        #endregion

    }

}
