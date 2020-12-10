using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Diagnostics;

namespace MediaManager {

    public sealed class MediaPlayer {

        #region delegate

        public delegate void MediaStartedCallback(in MediaFile media);

        public delegate void MediaStoppedCallback(in MediaFile media, in bool success);

        public delegate void MediaChangedCallback(in MediaFile oldMedia, in MediaFile newMedia);

        #endregion

        #region variable

        public readonly MediaElement mediaElement;

        private readonly MediaStartedCallback mediaStartedCallback;

        private readonly MediaStoppedCallback mediaStoppedCallback;

        private readonly MediaChangedCallback mediaChangedCallback;

        private MediaFile lastMediaFile;

        #endregion

        #region property

        /// <summary>
        /// Volume between 0.0 and 1.0 that the media should be played at.
        /// </summary>
        public double Volume {
            get => mediaElement.Volume;
            set {
                mediaElement.IsMuted = false;
                mediaElement.Volume = value;
            }
        }

        /// <summary>
        /// Value between 0.0 and 1.0 denoting how far through the media the MediaPlayer is.
        /// </summary>
        public double NormalizedProgress {

            get {

                Duration d = mediaElement.NaturalDuration;
                if (!d.HasTimeSpan) return 0.0;

                TimeSpan position = mediaElement.Position;
                TimeSpan duration = d.TimeSpan;

                double denominator = duration.TotalMilliseconds;
                if (denominator == 0.0) return 1.0;

                double progress = position.TotalMilliseconds / denominator; // get the progress through the media
                return progress < 1.0 ? progress : 1.0; // clamp below 1.0

            }

            set {

                if (value < 0.0 || value > 1.0) throw new ArgumentOutOfRangeException("value");

                Duration duration = mediaElement.NaturalDuration;
                if (!duration.HasTimeSpan) return;

                mediaElement.Position = TimeSpan.FromMilliseconds(value * duration.TimeSpan.TotalMilliseconds);

            }

        }

        /// <summary>
        /// Number of seconds into the current media.
        /// </summary>
        public double Progress {

            get => mediaElement.Position.TotalSeconds;
            set => mediaElement.Position = TimeSpan.FromSeconds(value);

        }

        /// <summary>
        /// Number of seconds that the current media is.
        /// </summary>
        public double Duration {

            get {

                Duration duration = mediaElement.NaturalDuration;
                return duration.HasTimeSpan ? duration.TimeSpan.TotalSeconds : 0.0;

            }

        }

        public bool IsPlaying { get; private set; } = false;

        /// <summary>
        /// Media currently loaded into the MediaPlayer.
        /// </summary>
        public MediaFile CurrentMedia { get; private set; } = null;

        #endregion

        #region constructor

        public MediaPlayer(
            in MediaElement mediaElement,
            in MediaStartedCallback mediaStartedCallback = null,
            in MediaStoppedCallback mediaStoppedCallback = null,
            in MediaChangedCallback mediaChangedCallback = null
        ) {

            this.mediaElement = mediaElement ?? throw new ArgumentNullException("mediaElement");
            //mediaElement.Source = null;
            //mediaElement.Visibility = Visibility.Collapsed;

            this.mediaStartedCallback = mediaStartedCallback;
            this.mediaStoppedCallback = mediaStoppedCallback;
            this.mediaChangedCallback = mediaChangedCallback;

            //mediaElement.MediaOpened += OnMediaOpened;
            mediaElement.Loaded += OnMediaLoaded;
            mediaElement.MediaEnded += OnMediaEnded;
            mediaElement.SourceUpdated += OnMediaChanged;
            mediaElement.MediaFailed += OnMediaFailed;
            lastMediaFile = null;

        }

        #endregion

        #region logic

        #region OnMediaLoaded

        private void OnMediaLoaded(object sender, RoutedEventArgs e) {

            mediaElement.Play();
            if (!IsPlaying) mediaElement.Pause();

        }

        #endregion

        #region OnMediaOpened

        private void OnMediaStarted() => mediaStartedCallback?.Invoke(CurrentMedia);

        #endregion

        #region OnMediaEnded

        private void OnMediaEnded(object sender, EventArgs e) {

            if (mediaStoppedCallback != null) {

                try {

                    mediaStoppedCallback(CurrentMedia, true);

                } finally {

                    CurrentMedia = null;

                }

            } else CurrentMedia = null;

        }

        #endregion

        #region OnMediaChanged

        private void OnMediaChanged(object sender, DataTransferEventArgs e) => mediaChangedCallback?.Invoke(lastMediaFile, CurrentMedia);

        #endregion

        #region OnMediaFailed

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e) {

            Trace.WriteLine(e.ErrorException.Message);
            mediaElement.Stop();
            mediaElement.Source = null;

            if (mediaStoppedCallback != null) {

                try {
                    mediaStoppedCallback(CurrentMedia, false);
                } finally {
                    CurrentMedia = null;
                }

            } else CurrentMedia = null;

        }

        #endregion

        #region Render

        public void RenderPreview(in Uri uri) {

            if (uri == null) throw new ArgumentNullException("path");

            if (mediaElement.Source != null || IsPlaying) Stop();

            mediaElement.Source = uri;
            mediaElement.Clock = null;
            mediaElement.Position = TimeSpan.FromSeconds(0.0);
            mediaElement.IsMuted = true;
            IsPlaying = false;

        }

        #endregion

        #region Play

        public void Play() {

            if (mediaElement.Source != null) {
                mediaElement.Play();
                IsPlaying = true;
                OnMediaStarted();
            }

        }

        public void Play(in MediaFile mediaFile) {

            if (mediaFile == null) throw new ArgumentNullException("mediaFile");

            if (mediaFile == CurrentMedia) { // the target media file is the same as the one playing currently

                mediaElement.Play();
                IsPlaying = true;
                return;

            }

            if (mediaElement.Source != null) {
                mediaElement.Stop(); // stop previous media
                mediaElement.Source = null; // remove source
            }

            lastMediaFile = CurrentMedia;
            CurrentMedia = mediaFile;

            mediaElement.IsMuted = false;
            mediaElement.Source = mediaFile.location;

            mediaElement.Clock = null;
            mediaElement.Position = TimeSpan.FromSeconds(0.0);

            mediaElement.Play();
            IsPlaying = true;

            OnMediaStarted();

        }

        #endregion

        #region Pause

        public void Pause() {

            if (mediaElement.CanPause) {
                mediaElement.Pause();
                IsPlaying = false;
            }

        }

        #endregion

        #region Stop

        public void Stop() {

            if (mediaElement.Source != null) {
                mediaElement.Stop();
                mediaElement.Source = null;
                IsPlaying = false;
            }

        }

        #endregion

        #endregion

    }

}