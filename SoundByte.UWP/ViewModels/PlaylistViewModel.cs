﻿using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.UI.StartScreen;
using Windows.UI.Xaml.Controls;
using SoundByte.Core;
using SoundByte.Core.Items.Playlist;
using SoundByte.Core.Items.Track;
using SoundByte.Core.Services;
using SoundByte.Core.Sources;
using SoundByte.UWP.Converters;
using SoundByte.UWP.Helpers;
using SoundByte.UWP.Services;

namespace SoundByte.UWP.ViewModels
{
    public class PlaylistViewModel : BaseViewModel
    {
        public PlaylistViewModel()
        {
            Tracks = new ObservableCollection<BaseTrack>();
        }

        public async Task SetupView(BasePlaylist newPlaylist)
        {
            // Check if the models saved playlist is null
            if (newPlaylist != null && (Playlist == null || Playlist.PlaylistId != newPlaylist.PlaylistId))
            {
                // Show the loading ring
                await App.SetLoadingAsync(true);

                // Set the playlist
                Playlist = newPlaylist;
                // Clear any existing tracks
                Tracks.Clear();

                // Get the resource loader
                var resources = ResourceLoader.GetForCurrentView();

                // Check if the tile is pinned
                if (TileHelper.IsTilePinned("Playlist_" + Playlist.PlaylistId))
                {
                    PinButtonIcon = "\uE77A";
                    PinButtonText = resources.GetString("AppBarUI_Unpin_Raw");
                }
                else
                {
                    PinButtonIcon = "\uE718";
                    PinButtonText = resources.GetString("AppBarUI_Pin_Raw");
                }

                if ((await SoundByteService.Current.ExistsAsync(ServiceType.SoundCloud, $"/e1/me/playlist_likes/{Playlist.PlaylistId}")).Response)
                    LikeButtonText = "Unlike Playlist";
                else
                    LikeButtonText = "Like Playlist";

                try
                {
                    // Get the playlist tracks
                    var playlistTracks =
                        (await SoundByteService.Current.GetAsync<SoundCloudPlaylist>(ServiceType.SoundCloud, "/playlists/" + Playlist.PlaylistId)).Response.Tracks;
                    playlistTracks.ForEach(x => Tracks.Add(x.ToBaseTrack()));
                }
                catch (Exception)
                {
                    await NavigationService.Current.CallMessageDialogAsync("Could not load tracks",
                        "Something went wrong when trying to load the tracks for this playlist, please make sure you are connected to the internet and then go back, and click on this playlist again.");
                }

                // Hide the loading ring
                await App.SetLoadingAsync(false);
            }
        }

        #region Private Variables

        // The playlist object
        private BasePlaylist _playlist;

        // List of tracks on the UI
        private ObservableCollection<BaseTrack> _tracks;

        // Icon for the pin button
        private string _pinButtonIcon = "\uE718";

        // Text for the pin button
        private string _pinButtonText;

        // Icon for the pin button
        private string _likeButtonIcon = "\uE718";

        // Text for the pin button
        private string _likeButtonText;

        #endregion

        #region Model

        /// <summary>
        ///     Gets or sets a list of tracks in the playlist
        /// </summary>
        public ObservableCollection<BaseTrack> Tracks
        {
            get => _tracks;
            set
            {
                if (value == _tracks) return;

                _tracks = value;
                UpdateProperty();
            }
        }

        /// <summary>
        ///     Gets or sets the current playlist object
        /// </summary>
        public BasePlaylist Playlist
        {
            get => _playlist;
            set
            {
                if (value == _playlist) return;

                _playlist = value;
                UpdateProperty();
            }
        }

        public string PinButtonIcon
        {
            get => _pinButtonIcon;
            set
            {
                if (value != _pinButtonIcon)
                {
                    _pinButtonIcon = value;
                    UpdateProperty();
                }
            }
        }

        public string LikeButtonIcon
        {
            get => _likeButtonIcon;
            set
            {
                if (value != _likeButtonIcon)
                {
                    _likeButtonIcon = value;
                    UpdateProperty();
                }
            }
        }

        public string PinButtonText
        {
            get => _pinButtonText;
            set
            {
                if (value != _pinButtonText)
                {
                    _pinButtonText = value;
                    UpdateProperty();
                }
            }
        }

        public string LikeButtonText
        {
            get => _likeButtonText;
            set
            {
                if (value != _likeButtonText)
                {
                    _likeButtonText = value;
                    UpdateProperty();
                }
            }
        }

        /// <summary>
        ///     Pins or unpins a playlist from the start
        ///     menu / screen.
        /// </summary>
        public async void PinPlaylist()
        {
            // Show the loading ring
            await App.SetLoadingAsync(true);
            // Get the resource loader
            var resources = ResourceLoader.GetForCurrentView();
            // Check if the tile exists
            if (TileHelper.IsTilePinned("Playlist_" + Playlist.PlaylistId))
            {
                // Try remove the tile
                if (await TileHelper.RemoveTileAsync("Playlist_" + Playlist.PlaylistId))
                {
                    PinButtonIcon = "\uE718";
                    PinButtonText = resources.GetString("AppBarUI_Pin_Raw");
                }
                else
                {
                    PinButtonIcon = "\uE77A";
                    PinButtonText = resources.GetString("AppBarUI_Unpin_Raw");
                }
            }
            else
            {
                // Create the tile
                if (await TileHelper.CreateTileAsync("Playlist_" + Playlist.PlaylistId, Playlist.Title,
                    "soundbyte://core/playlist?id=" + Playlist.PlaylistId,
                    new Uri(Playlist.ThumbnailUrl), ForegroundText.Light))
                {
                    PinButtonIcon = "\uE77A";
                    PinButtonText = resources.GetString("AppBarUI_Unpin_Raw");
                }
                else
                {
                    PinButtonIcon = "\uE718";
                    PinButtonText = resources.GetString("AppBarUI_Pin_Raw");
                }
            }
            // Hide the loading ring
            await App.SetLoadingAsync(false);
        }

        /// <summary>
        ///     Shuffles the tracks in the playlist
        /// </summary>
        public async void ShuffleItemsAsync()
        {
            await ShuffleTracksListAsync(Tracks);
        }

        /// <summary>
        ///     Called when the user taps on a sound in the
        ///     Sounds tab
        /// </summary>
        public async void TrackClicked(object sender, ItemClickEventArgs e)
        {
            // Get the Click item
            var item = (BaseTrack) e.ClickedItem;

            var startPlayback = await PlaybackService.Instance.InitilizePlaylistAsync<DummyTrackSource>(Tracks);

            if (!startPlayback.Success)
            {
                await NavigationService.Current.CallMessageDialogAsync(startPlayback.Message,
                    "Error playing playlist.");
                return;
            }

            await PlaybackService.Instance.StartTrackAsync(item);
        }

        /// <summary>
        ///     Starts playing the playlist
        /// </summary>
        public async void NavigatePlay()
        {
            var startPlayback =
                await PlaybackService.Instance.InitilizePlaylistAsync<DummyTrackSource>(Tracks);

            if (!startPlayback.Success)
            {
                await NavigationService.Current.CallMessageDialogAsync(startPlayback.Message,
                    "Error playing playlist.");
                return;
            }

            await PlaybackService.Instance.StartTrackAsync();
        }

        #endregion
    }
}