using System;

namespace MediaManager {

    public enum MediaFileType : int {

        Unknown = 0,
        Music = 1,
        Video = 2,
        Image = 3

    }

    public static class MediaFileTypeUtility {

        #region constant

        public static readonly string[] SupportedMusicFileExtensions = new string[] {
            "wav",
            "aac",
            "mp3",
            "aiff",
            "pcm",
            "wma",
            "flac",
            "alac"
        };

        public static readonly string[] SupportedVideoFileExtensions = new string[] {
            "mp4",
            "mov",
            "wmv",
            "flv",
            "avi"
        };

        public static readonly string[] SupportedImageFileExtensions = new string[] {
            "png",
            "jpg",
            "jpeg",
            "gif"
        };

        #endregion

        #region logic

        public static MediaFileType FromExtension(string extension) {

            if (string.IsNullOrWhiteSpace(extension) || extension.Length == 0) return MediaFileType.Unknown;
            if (extension[0] == '.') extension = extension[1..];

            foreach (string e in SupportedMusicFileExtensions) { if (extension.Equals(e)) return MediaFileType.Music; }
            foreach (string e in SupportedVideoFileExtensions) { if (extension.Equals(e)) return MediaFileType.Video; }
            foreach (string e in SupportedImageFileExtensions) { if (extension.Equals(e)) return MediaFileType.Image; }
            return MediaFileType.Unknown;

        }

        #endregion

    }

}
