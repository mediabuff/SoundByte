﻿/* |----------------------------------------------------------------|
 * | Copyright (c) 2017 - 2018 Grid Entertainment                   |
 * | All Rights Reserved                                            |
 * |                                                                |
 * | This source code is to only be used for educational            |
 * | purposes. Distribution of SoundByte source code in             |
 * | any form outside this repository is forbidden. If you          |
 * | would like to contribute to the SoundByte source code, you     |
 * | are welcome.                                                   |
 * |----------------------------------------------------------------|
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using JetBrains.Annotations;
using Microsoft.Toolkit.Uwp.Helpers;
using SoundByte.Core;
using SoundByte.Core.Items.Track;
using SoundByte.Core.Items.User;
using SoundByte.Core.Services;
using SoundByte.UWP.Helpers;
using SoundByte.UWP.Services;

namespace SoundByte.UWP.ViewModels
{
    public class PlaybackViewModel : BaseViewModel
    {
        private CoreDispatcher _currentUiDispatcher;

        #region Getters and Setters
        /// <summary>
        /// The current playing track
        /// </summary>
        [CanBeNull]
        public BaseTrack CurrentTrack
        {
            get => _currentTrack;
            set
            {
                if (_currentTrack == value)
                    return;

                _currentTrack = value;
                UpdateProperty();
            }
        }
        private BaseTrack _currentTrack;

        /// <summary>
        ///     The amount of time spent listening to the track
        /// </summary>
        public string TimeListened
        {
            get => _timeListened;
            set
            {
                if (_timeListened == value)
                    return;

                _timeListened = value;
                UpdateProperty();
            }
        }
        private string _timeListened = "00:00";

        /// <summary>
        ///     The amount of time remaining
        /// </summary>
        public string TimeRemaining
        {
            get => _timeRemaining;
            set
            {
                if (_timeRemaining == value)
                    return;

                _timeRemaining = value;
                UpdateProperty();
            }
        }
        private string _timeRemaining = "-00:00";

        /// <summary>
        ///     The current slider value
        /// </summary>
        public double CurrentTimeValue
        {
            get => _currentTimeValue;
            set
            {
                _currentTimeValue = value;
                UpdateProperty();
            }
        }
        private double _currentTimeValue;

        /// <summary>
        ///     The max slider value
        /// </summary>
        public double MaxTimeValue
        {
            get => _maxTimeValue;
            private set
            {
                _maxTimeValue = value;
                UpdateProperty();
            }
        }
        private double _maxTimeValue = 100;

        /// <summary>
        ///     The current text for the volume icon
        /// </summary>
        public string VolumeIcon
        {
            get => _volumeIcon;
            private set
            {
                if (_volumeIcon == value)
                    return;

                _volumeIcon = value;
                UpdateProperty();
            }
        }
        private string _volumeIcon = "\uE767";

        /// <summary>
        ///     The content on the play_pause button
        /// </summary>
        public string PlayButtonContent
        {
            get => _playButtonContent;
            set
            {
                if (_playButtonContent == value)
                    return;

                _playButtonContent = value;
                UpdateProperty();
            }
        }
        private string _playButtonContent = "\uE769";

        /// <summary>
        ///     The current value of the volume slider
        /// </summary>
        public double MediaVolume
        {
            get => PlaybackV2Service.Instance.TrackVolume * 100;
            set
            {
                // Set the volume
                PlaybackV2Service.Instance.SetTrackVolume(value / 100);

                // Update the UI
                if ((int)value == 0)
                {
                    PlaybackV2Service.Instance.MuteTrack(true);
                    VolumeIcon = "\uE74F";
                }
                else if (value < 25)
                {
                    PlaybackV2Service.Instance.MuteTrack(false);
                    VolumeIcon = "\uE992";
                }
                else if (value < 50)
                {
                    PlaybackV2Service.Instance.MuteTrack(false);
                    VolumeIcon = "\uE993";
                }
                else if (value < 75)
                {
                    PlaybackV2Service.Instance.MuteTrack(false);
                    VolumeIcon = "\uE994";
                }
                else
                {
                    PlaybackV2Service.Instance.MuteTrack(false);
                    VolumeIcon = "\uE767";
                }

                UpdateProperty();
            }
        }

        /// <summary>
        ///     Are tracks shuffled
        /// </summary>
        public bool IsShuffleEnabled
        {
            get => PlaybackV2Service.Instance.IsPlaylistShuffled;
            set
            {
                // Set the new value and force the UI to update
                PlaybackV2Service.Instance.ShufflePlaylist(value);
                UpdateProperty();
            }
        }

        /// <summary>
        ///     Is the song going to repeat when finished
        /// </summary>
        public bool IsRepeatEnabled
        {
            get => PlaybackV2Service.Instance.IsTrackRepeating;
            set
            {
                PlaybackV2Service.Instance.RepeatTrack(value);
                UpdateProperty();
            }
        }

        #endregion

        #region Timers
        /// <summary>
        ///     This timer runs every 500ms to ensure that the current position,
        ///     time, remaining time, etc. variables are correct.
        /// </summary>
        private readonly DispatcherTimer _updateInformationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };

        /// <summary>
        ///     This timer has to run quite fast. It ensures that audio and video are
        ///     in sync for YouTube videos.
        /// </summary>
        private readonly DispatcherTimer _audioVideoSyncTimer = new DispatcherTimer
        {

            Interval = TimeSpan.FromMilliseconds(75)
        };
        #endregion

        #region Constructors
        public PlaybackViewModel() : this(CoreApplication.MainView.Dispatcher)
        {  }

        public PlaybackViewModel(CoreDispatcher uiDispatcher)
        {
            _currentUiDispatcher = uiDispatcher;

            // Bind the methods that we need
            PlaybackV2Service.Instance.OnStateChange += OnStateChange;
            PlaybackV2Service.Instance.OnTrackChange += OnTrackChange;

            // Bind timer methods
            _updateInformationTimer.Tick += UpdateInformation;
            _audioVideoSyncTimer.Tick += SyncAudioVideo;

            // Start the timers if ready
            // TODO: Move this into playing logic

            if (!_updateInformationTimer.IsEnabled)
                _updateInformationTimer.Start();


            if (!_audioVideoSyncTimer.IsEnabled)
                _audioVideoSyncTimer.Start();
        }
        #endregion

        #region Timer Methods
        /// <summary>
        ///     Syncs YouTube audio and video when needed
        /// </summary>
        private async void SyncAudioVideo(object sender, object e)
        {
            // Only run this method if there is a track and it's a 
            // youtube track
            if (CurrentTrack == null || CurrentTrack.ServiceType != ServiceType.YouTube)
                return;

            // Don't run in the backround
            if (DeviceHelper.IsBackground)
                return;

            // Only call the following if the player exists, is playing
            // and the time is greater then 0.
            if (PlaybackV2Service.Instance.CurrentPlaybackState != MediaPlaybackState.Playing
                || PlaybackV2Service.Instance.GetTrackPosition().Milliseconds <= 0)
                return;

            await _currentUiDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!(App.CurrentFrame?.FindName("VideoOverlay") is MediaElement overlay))
                    return;

                var difference = overlay.Position - PlaybackV2Service.Instance.GetTrackPosition();

                if (Math.Abs(difference.TotalMilliseconds) >= 1000)
                {
                    overlay.PlaybackRate = 1;
                    overlay.Position = PlaybackV2Service.Instance.GetTrackPosition();
                    System.Diagnostics.Debug.WriteLine("OUT OF SYNC: SKIPPING (>= 1000ms)");
                }
                else if (Math.Abs(difference.TotalMilliseconds) >= 500)
                {
                    overlay.PlaybackRate = difference.TotalMilliseconds > 0 ? 0.25 : 1.75;
                    System.Diagnostics.Debug.WriteLine("OUT OF SYNC: CHANGE PLAYBACK RATE (>= 500ms)");
                }
                else if (Math.Abs(difference.TotalMilliseconds) >= 250)
                {
                    overlay.PlaybackRate = difference.TotalMilliseconds > 0 ? 0.5 : 1.5;
                    System.Diagnostics.Debug.WriteLine("OUT OF SYNC: CHANGE PLAYBACK RATE (>= 250ms)");
                }
                else if (Math.Abs(difference.TotalMilliseconds) >= 100)
                {
                    overlay.PlaybackRate = difference.TotalMilliseconds > 0 ? 0.75 : 1.25;
                    System.Diagnostics.Debug.WriteLine("OUT OF SYNC: CHANGE PLAYBACK RATE (>= 100ms)");
                }
                else
                {
                    overlay.PlaybackRate = 1;
                }
            });
        }

        /// <summary>
        ///     Timer method that is run to make sure the UI is kept up to date
        /// </summary>
        private async void UpdateInformation(object sender, object e)
        {
            if (DeviceHelper.IsBackground)
                return;

            // Only call the following if the player exists, is playing
            // and the time is greater then 0.
            if (PlaybackV2Service.Instance.CurrentPlaybackState != MediaPlaybackState.Playing
                || PlaybackV2Service.Instance.GetTrackPosition().Milliseconds <= 0)
                return;

            if (CurrentTrack == null)
                return;

            await _currentUiDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (CurrentTrack.IsLive)
                {
                    // Set the current time value
                    CurrentTimeValue = 1;

                    // Set the time listened text
                    TimeListened = "LIVE";

                    // Set the time remaining text
                    TimeRemaining = "LIVE";

                    // Set the maximum value
                    MaxTimeValue = 1;
                }
                else
                {
                    // Set the current time value
                    CurrentTimeValue = PlaybackV2Service.Instance.GetTrackPosition().TotalSeconds;

                    // Get the remaining time for the track
                    var remainingTime = PlaybackV2Service.Instance.GetTrackDuration().Subtract(PlaybackV2Service.Instance.GetTrackPosition());

                    // Set the time listened text
                    TimeListened = NumberFormatHelper.FormatTimeString(PlaybackV2Service.Instance.GetTrackPosition().TotalMilliseconds);

                    // Set the time remaining text
                    TimeRemaining = "-" + NumberFormatHelper.FormatTimeString(remainingTime.TotalMilliseconds);

                    // Set the maximum value
                    MaxTimeValue = PlaybackV2Service.Instance.GetTrackDuration().TotalSeconds;
                }
            });
        }
        #endregion

        #region Track Control Methods
        /// <summary>
        ///     Toggle if the current track should repeat
        /// </summary>
        public void ToggleRepeat()
        {
            IsRepeatEnabled = !IsRepeatEnabled;
        }

        /// <summary>
        ///     Toggle if the current playlist is shuffled
        /// </summary>
        public void ToggleShuffle()
        {
            IsShuffleEnabled = !IsShuffleEnabled;
        }

        /// <summary>
        ///     Toggle if we should mute
        /// </summary>
        public void ToggleMute()
        {
            // Toggle mute
            PlaybackV2Service.Instance.MuteTrack(!PlaybackV2Service.Instance.IsTrackMuted);

            // Update the UI
            VolumeIcon = PlaybackV2Service.Instance.IsTrackMuted ? "\uE74F" : "\uE767";
        }
        #endregion

        #region Track Playback State
        /// <summary>
        ///     Toggles the state between the track playing
        ///     and not playing
        /// </summary>
        public void ChangePlaybackState()
        {
            // Get the current state of the track
            var currentState = PlaybackV2Service.Instance.CurrentPlaybackState;

            // If the track is currently paused
            if (currentState == MediaPlaybackState.Paused)
            {
                //               UpdateNormalTiles();
                // Play the track
                PlaybackV2Service.Instance.PlayTrack();
            }

            // If the track is currently playing
            if (currentState == MediaPlaybackState.Playing)
            {
                //              UpdatePausedTile();
                // Pause the track
                PlaybackV2Service.Instance.PauseTrack();
            }
        }

        /// <summary>
        ///     Go forward one track
        /// </summary>
        public void SkipNext()
        {
            PlaybackV2Service.Instance.NextTrack();
        }

        /// <summary>
        ///     Go backwards one track
        /// </summary>
        public void SkipPrevious()
        {
            PlaybackV2Service.Instance.PreviousTrack();
        }

        #endregion

        #region Methods
        public async void OnPlayingSliderChange()
        {
            if (DeviceHelper.IsBackground)
                return;

            if (CurrentTrack == null)
                return;

            if (CurrentTrack.IsLive)
                return;

            // Set the track position
            PlaybackV2Service.Instance.SetTrackPosition(TimeSpan.FromSeconds(CurrentTimeValue));

            await _currentUiDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!(App.CurrentFrame.FindName("VideoOverlay") is MediaElement overlay))
                    return;

                overlay.Position = TimeSpan.FromSeconds(CurrentTimeValue);
            });
        }

        /// <summary>
        ///     Called when the playback service loads a new track. Used
        ///     to update the required values for the UI.
        /// </summary>
        /// <param name="track"></param>
        private async void OnTrackChange(BaseTrack track)
        {
            // Do nothing if running in the background
            if (DeviceHelper.IsBackground)
                return;

            // Same track, no need to perform this logic
            if (track == CurrentTrack)
                return;

            // Run all this on the UI thread
            await _currentUiDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                // Set the new current track, updating the UI
                CurrentTrack = track;

                // Only run the sync timer when listening to a youtube music video
                if (track.ServiceType == ServiceType.YouTube)
                {
                    if (!_audioVideoSyncTimer.IsEnabled)
                        _audioVideoSyncTimer.Start();
                }
                else
                {
                    if (_audioVideoSyncTimer.IsEnabled)
                        _audioVideoSyncTimer.Stop();
                }

                if (!track.IsLive)
                {
                    TimeRemaining = "-" + NumberFormatHelper.FormatTimeString(track.Duration.TotalMilliseconds);
                    TimeListened = "00:00";
                    CurrentTimeValue = 0;
                    MaxTimeValue = track.Duration.TotalSeconds;
                }
                else
                {
                    TimeRemaining = "LIVE";
                    TimeListened = "LIVE";
                    CurrentTimeValue = 1;
                    MaxTimeValue = 1;
                }

                if (CurrentTrack?.ServiceType == ServiceType.SoundCloud)
                {
                    try
                    {
                        CurrentTrack.IsLiked = await SoundByteService.Current.ExistsAsync(ServiceType.SoundCloud,
                            "/me/favorites/" + CurrentTrack.Id);
                    }
                    catch
                    {
                        CurrentTrack.IsLiked = false;
                    }

                    try
                    {
                        CurrentTrack.User = (await SoundByteService.Current.GetAsync<SoundCloudUser>(ServiceType.SoundCloud, $"/users/{CurrentTrack.User.Id}")).ToBaseUser();
                    }
                    catch
                    {
                        // ignored
                    }
                }
            });
        }

        private async void OnStateChange(MediaPlaybackState mediaPlaybackState)
        {
            // Don't run in the background if on Xbox
            if (DeviceHelper.IsBackground && DeviceHelper.IsXbox)
                return;

            await _currentUiDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var overlay = App.CurrentFrame?.FindName("VideoOverlay") as MediaElement;

                switch (mediaPlaybackState)
                {
                    case MediaPlaybackState.Playing:
                        await App.SetLoadingAsync(false);
                        PlayButtonContent = "\uE769";
                        overlay?.Play();
                        break;
                    case MediaPlaybackState.Buffering:
                        await App.SetLoadingAsync(true);
                        break;
                    case MediaPlaybackState.None:
                        await App.SetLoadingAsync(false);
                        PlayButtonContent = "\uE768";
                        break;
                    case MediaPlaybackState.Paused:
                        await App.SetLoadingAsync(false);
                        PlayButtonContent = "\uE768";
                        overlay?.Pause();
                        break;
                    default:
                        await App.SetLoadingAsync(false);
                        PlayButtonContent = "\uE768";
                        overlay?.Play();
                        break;
                }
            });
        }
        #endregion

        public override void Dispose()
        {
            // Unbind the methods that we need
            PlaybackV2Service.Instance.OnStateChange -= OnStateChange;
            PlaybackV2Service.Instance.OnTrackChange -= OnTrackChange;

            // Unbind timer methods
            _updateInformationTimer.Tick -= UpdateInformation;
            _audioVideoSyncTimer.Tick -= SyncAudioVideo;

            base.Dispose();
        }
    }
}
