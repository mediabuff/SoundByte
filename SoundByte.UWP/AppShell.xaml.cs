﻿/* |----------------------------------------------------------------|
 * | Copyright (c) 2017, Grid Entertainment                         |
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
using System.Numerics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.VoiceCommands;
using Windows.Globalization;
using Windows.Services.Store;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Services.Store.Engagement;
using Microsoft.Toolkit.Uwp.Helpers;
using NotificationsExtensions;
using NotificationsExtensions.Toasts;
using SoundByte.Core;
using SoundByte.Core.Items.Playlist;
using SoundByte.Core.Items.Track;
using SoundByte.Core.Items.User;
using SoundByte.Core.Services;
using SoundByte.UWP.Dialogs;
using SoundByte.UWP.Helpers;
using SoundByte.UWP.Services;
using SoundByte.UWP.Models;
using SoundByte.UWP.Views;
using SoundByte.UWP.Views.Application;
using SoundByte.UWP.Views.General;
using SoundByte.UWP.Views.Me;
using SoundByte.UWP.Views.Search;

namespace SoundByte.UWP
{
    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AppShell
    {
        public AppShell(string path)
        {
            // Init the XAML
            InitializeComponent();

            // Set the accent color
            TitlebarHelper.UpdateTitlebarStyle();

            // When the page is loaded (after the following and xaml init)
            // we can perform the async work
            Loaded += async (sender, args) => await PerformAsyncWork(path);

            // Unload events
            Unloaded += (sender, args) => Dispose();

            var titleBar = CoreApplication.GetCurrentView().TitleBar;
            titleBar.LayoutMetricsChanged += (s, e) =>
            {
                AppTitle.Margin = new Thickness(CoreApplication.GetCurrentView().TitleBar.SystemOverlayLeftInset + 12, 8, 0, 0);
            };

            // This is a dirty to show the now playing
            // bar when a track is played. This method
            // updates the required layout for the now
            // playing bar.
            PlaybackService.Instance.OnCurrentTrackChanged += InstanceOnOnCurrentTrackChanged;

            // Create a shell frame shadow for mobile and desktop
            if (DeviceHelper.IsDesktop || DeviceHelper.IsMobile)
                ShellFrame.CreateElementShadow(new Vector3(0, 0, 0), 30, new Color {A = 62, R = 0, G = 0, B = 0},
                    ShellFrameShadow);

            // Events for Xbox
            if (DeviceHelper.IsXbox)
            {
                // Pane is hidden by default
              //  MainSplitView.IsPaneOpen = false;
             //   MainSplitView.DisplayMode = SplitViewDisplayMode.CompactOverlay;
             //   MainSplitView.Margin = new Thickness();
             //   MainSplitView.LightDismissOverlayMode = LightDismissOverlayMode.On;

                // Center all navigation icons
             //   NavbarScrollViewer.VerticalAlignment = VerticalAlignment.Center;

                // Show background blur image
                XboxOnlyGrid.Visibility = Visibility.Visible;
                ShellFrame.Background = new SolidColorBrush(Colors.Transparent);

                // Make xbox selection easy to see
                Application.Current.Resources["CircleButtonStyle"] =
                    Application.Current.Resources["XboxCircleButtonStyle"];
            }

            // Events for Mobile
            if (DeviceHelper.IsMobile)
            {
                // Amoled Magic
                RootGrid.Background = new SolidColorBrush(Application.Current.RequestedTheme == ApplicationTheme.Dark
                    ? Colors.Black
                    : Colors.White);
            }

            if (string.IsNullOrEmpty(path))
                RootFrame.Navigate(typeof(HomeView));
        }

        private async void InstanceOnOnCurrentTrackChanged(BaseTrack newTrack)
        {
            await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
            {
                if (!DeviceHelper.IsDesktop ||
                    RootFrame.CurrentSourcePageType == typeof(NowPlayingView))
                    HideNowPlayingBar();
                else
                    ShowNowPlayingBar();
            });
        }

        /// <summary>
        ///     Used to access the playback service from the UI
        /// </summary>
        public PlaybackService Service => PlaybackService.Instance;

        public void Dispose()
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            PlaybackService.Instance.OnCurrentTrackChanged -= InstanceOnOnCurrentTrackChanged;
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (App.OverrideBackEvent)
            {
                e.Handled = true;
            }
            else
            {
                if (RootFrame.SourcePageType == typeof(BlankView))
                {
                    RootFrame.BackStack.Clear();
                    RootFrame.Navigate(typeof(HomeView));
                    e.Handled = true;
                }
                else
                {
                    if (RootFrame.CanGoBack)
                    {
                        RootFrame.GoBack();
                        e.Handled = true;
                    }
                    else
                    {
                        RootFrame.Navigate(typeof(HomeView));
                        e.Handled = true;
                    }
                }
            }
        }

        private async Task PerformAsyncWork(string path)
        {
            // Set the app language
            ApplicationLanguages.PrimaryLanguageOverride =
                string.IsNullOrEmpty(SettingsService.Instance.CurrentAppLanguage)
                    ? ApplicationLanguages.Languages[0]
                    : SettingsService.Instance.CurrentAppLanguage;

            // Set the on back requested event
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;

            // Navigate to the first page
            await HandleProtocolAsync(path);

            // Run on the background thread
            await Task.Run(async () =>
            {
                try
                {
                    // Get the store and check for app updates
                    var updates = await StoreContext.GetDefault().GetAppAndOptionalStorePackageUpdatesAsync();

                    // If we have updates navigate to the update page where we
                    // ask the user if they would like to update or not (depending
                    // if the update is mandatory or not).
                    if (updates.Count > 0)
                    {
                        await NavigationService.Current.CallDialogAsync<PendingUpdateDialog>();
                    }

                    // Test Version and tell user app upgraded
                    HandleNewAppVersion();

                    // Clear the unread badge
                    BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();

                    // Handle donation logic
                    await MonitizeService.Instance.InitProductInfoAsync();

                    // Register notifications
                    var engagementManager = StoreServicesEngagementManager.GetDefault();
                    await engagementManager.RegisterNotificationChannelAsync();

                    // Install Cortana Voice Commands
                    var vcdStorageFile = await Package.Current.InstalledLocation.GetFileAsync(@"SoundByteCommands.xml");
                    await VoiceCommandDefinitionManager.InstallCommandDefinitionsFromStorageFileAsync(vcdStorageFile);
                }
                catch
                {
                    // Ignore
                }
            });
        }

        private void HandleNewAppVersion()
        {
            var currentAppVersionString = Package.Current.Id.Version.Major + "." + Package.Current.Id.Version.Minor +
                                          "." + Package.Current.Id.Version.Build;

            // Get stored app version (this will stay the same when app is updated)
            var storedAppVersionString = SettingsService.Instance.AppStoredVersion;

            // Save the new app version
            SettingsService.Instance.AppStoredVersion = currentAppVersionString;

            // If the stored version is null, set the temp to 0, and the version to the actual version
            if (!string.IsNullOrEmpty(storedAppVersionString))
            {
                // Convert the current app version
                var currentAppVersion = new Version(currentAppVersionString);
                // Convert the stored app version
                var storedAppVersion = new Version(storedAppVersionString);

                if (currentAppVersion <= storedAppVersion)
                    return;
            }

            var clickText = "Tap here to read what's new.";
            if (DeviceHelper.IsDesktop)
                clickText = "Click here to read what's new.";
            if (DeviceHelper.IsXbox)
                clickText = "Hold down the Xbox button to read what's new.";

            // Generate a notification
            var toastContent = new ToastContent
            {
                Visual = new ToastVisual
                {
                    BindingGeneric = new ToastBindingGeneric
                    {
                        Children =
                        {
                            new AdaptiveText
                            {
                                Text = "SoundByte"
                            },

                            new AdaptiveText
                            {
                                Text = string.IsNullOrEmpty(storedAppVersionString)
                                    ? "Thank you for downloading SoundByte!"
                                    : $"SoundByte was updated to version {currentAppVersionString}. {clickText}"
                            }
                        }
                    }           
                },
                ActivationType = ToastActivationType.Protocol,
                Launch = "soundbyte://core/changelog"
            };

            // Show the notification
            var toast = new ToastNotification(toastContent.GetXml()) {ExpirationTime = DateTime.Now.AddMinutes(30)};
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        #region Protocol

        public async Task HandleProtocolAsync(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    if (path == "playUserLikes" || path == "shufflePlayUserLikes")
                    {
                        if (SoundByteV3Service.Current.IsServiceConnected(ServiceType.SoundCloud))
                        {
                            // Navigate to the now playing screen
                            RootFrame.Navigate(typeof(NowPlayingView));

                            // Get and load the user liked items
                            var userLikes = new LikeModel(SoundByteV3Service.Current.GetConnectedUser(ServiceType.SoundCloud));

                            while (userLikes.HasMoreItems)
                                await userLikes.LoadMoreItemsAsync(500);

                            // Play the list of items
                            await PlaybackService.Instance.StartModelMediaPlaybackAsync(userLikes, path == "shufflePlayUserLikes");

                            return;
                        }
                    }

                    if (path == "playUserStream")
                    {
                        if (SoundByteV3Service.Current.IsServiceConnected(ServiceType.SoundCloud))
                        {
                            // Navigate to the now playing screen
                            RootFrame.Navigate(typeof(NowPlayingView));

                            // Get and load the user stream items
                            var userStream = new StreamModel();

                            // Counter so we don't get an insane amount of items
                            var i = 0;

                            // Grab all the users stream / 5 items
                            while (userStream.HasMoreItems && i <= 5)
                            {
                                i++;
                                await userStream.LoadMoreItemsAsync(500);
                            }

                            // Play the list of items
                         //   await PlaybackService.Instance.StartPlaylistMediaPlaybackAsync(
                          //      userStream.Where(x => x.Track != null).Select(x => x.Track).ToList(), path);

                            return;
                        }
                    }

                    var parser = DeepLinkParser.Create(path);

                    var section = parser.Root.Split('/')[0].ToLower();
                    var page = parser.Root.Split('/')[1].ToLower();

                    App.IsLoading = true;
                    if (section == "core")
                    {
                        switch (page)
                        {
                            case "track":

                                BaseTrack track = null;

                                switch (parser["service"])
                                {
                                    case "soundcloud":
                                        track = (await SoundByteV3Service.Current.GetAsync<SoundCloudTrack>(ServiceType.SoundCloud, $"/tracks/{parser["id"]}")).ToBaseTrack();
                                        break;
                                    case "youtube":
                                        break;
                                    case "fanburst":
                                        track = (await SoundByteV3Service.Current.GetAsync<FanburstTrack>(ServiceType.Fanburst, $"/videos/{parser["id"]}")).ToBaseTrack();
                                        break;
                                }

                                if (track != null)
                                {
                                    var startPlayback =
                                        await PlaybackService.Instance.StartPlaylistMediaPlaybackAsync(new List<BaseTrack> { track });

                                    if (!startPlayback.success)
                                        await new MessageDialog(startPlayback.message, "Error playing track.").ShowAsync();
                                }
                                break;
                            case "playlist":
                                var playlist =
                                    await SoundByteV3Service.Current.GetAsync<SoundCloudPlaylist>(ServiceType.SoundCloud, $"/playlists/{parser["id"]}");
                                App.NavigateTo(typeof(PlaylistView), playlist.ToBasePlaylist());
                                return;
                            case "user":
                                var user = await SoundByteV3Service.Current.GetAsync<SoundCloudUser>(ServiceType.SoundCloud, $"/users/{parser["id"]}");
                                App.NavigateTo(typeof(UserView), user.ToBaseUser());
                                return;
                            case "changelog":
                                App.NavigateTo(typeof(WhatsNewView));
                                return;
                        }
                    }       
                }
                catch (Exception)
                {
                    await new MessageDialog("The specified protocol is not correct. App will now launch as normal.")
                        .ShowAsync();
                }
                App.IsLoading = false;
            }

            RootFrame.Navigate(typeof(HomeView));
        }

        #endregion

        private void ShellFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // Update the side bar
            switch (((Frame) sender).SourcePageType.Name)
            {
                case "HomeView":
                    NavView.SelectedItem = NavigationItemHome;
                    break;
                case "ExploreView":
                    NavView.SelectedItem = NavigationItemExplore;
                    break;
                case "DonateView":
                    NavView.SelectedItem = NavigationItemDonations;
                    break;
                case "LikesView":
                    NavView.SelectedItem = NavigationItemLikes;
                    break;
                case "PlaylistsView":
                    NavView.SelectedItem = NavigationItemPlaylists;
                    break;
                case "HistoryView":
                    NavView.SelectedItem = NavigationItemHistory;
                    break;
                case "AccountView":
                    NavView.SelectedItem = NavigationItemAccounts;
                    break;
                case "AppInfoView":
                    NavView.SelectedItem = NavigationItemSettings;
                    break;
            }

            if (((Frame) sender).SourcePageType == typeof(HomeView) ||
                ((Frame) sender).SourcePageType == typeof(AppShell))
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Collapsed;
            else
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Visible;

            // Update the UI depending if we are logged in or not
            if (SoundByteV3Service.Current.IsServiceConnected(ServiceType.SoundCloud) ||
                SoundByteV3Service.Current.IsServiceConnected(ServiceType.Fanburst))
                ShowLoginContent();
            else
                ShowLogoutContent();

            if (DeviceHelper.IsDesktop)
                if (((Frame) sender).SourcePageType == typeof(NowPlayingView))
                {
                    NavView.IsPaneOpen = false;
                    NavView.IsPaneToggleButtonVisible = false;

                    HideNowPlayingBar();
                }
                else
                {
                    NavView.IsPaneOpen = true;
                    NavView.IsPaneToggleButtonVisible = true;

                    if (PlaybackService.Instance.CurrentTrack == null)
                        HideNowPlayingBar();
                    else
                        ShowNowPlayingBar();
                }

            RootFrame.Focus(FocusState.Programmatic);
        }

        private void HideNowPlayingBar()
        {
            NowPlaying.Visibility = Visibility.Collapsed;
            NavView.Margin = new Thickness { Bottom = 0 };
        }

        private void ShowNowPlayingBar()
        {
            NowPlaying.Visibility = Visibility.Visible;
            NavView.Margin = new Thickness { Bottom = 64 };
        }

        // Login and Logout events. This is used to display what pages
        // are visiable to the user.
        public void ShowLoginContent()
        {
            NavigationItemLikes.Visibility = Visibility.Visible;
            NavigationItemPlaylists.Visibility = Visibility.Visible;

            // Only show this tab if the users soundcloud account is connected
            NavigationItemHome.Visibility = SoundByteV3Service.Current.IsServiceConnected(ServiceType.SoundCloud) ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ShowLogoutContent()
        {
            NavigationItemLikes.Visibility = Visibility.Collapsed;
            NavigationItemPlaylists.Visibility = Visibility.Collapsed;
            NavigationItemHome.Visibility = Visibility.Collapsed;
        }


        #region Getters and Setters
        /// <summary>
        ///     Get the root frame, if no root frame exists,
        ///     we wait 150ms and call the getter again.
        /// </summary>
        public Frame RootFrame
        {
            get
            {
                if (ShellFrame != null) return ShellFrame;

                Task.Delay(TimeSpan.FromMilliseconds(150));

                return RootFrame;
            }
            set => ShellFrame = value;
        }

        #endregion

        private void NavView_OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            PerformNavigation(args.InvokedItem);
        }

        private void NavView_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var item = args.SelectedItem as NavigationViewItem;
            PerformNavigation(item?.Tag);
        }

        private void PerformNavigation(object item)
        {
            switch (item)
            {
                case "home":
                    RootFrame.Navigate(typeof(HomeView));
                    break;
                case "explore":
                    RootFrame.Navigate(typeof(ExploreView));
                    break;
                case "likes":
                    RootFrame.Navigate(typeof(LikesView));
                    break;
                case "playlists":
                    RootFrame.Navigate(typeof(PlaylistsView));
                    break;
                case "history":
                    RootFrame.Navigate(typeof(HistoryView));
                    break;
                case "donations":
                    RootFrame.Navigate(typeof(DonateView));
                    break;
                case "settings":
                    RootFrame.Navigate(typeof(AppInfoView));
                    break;
                case "accounts":
                    RootFrame.Navigate(typeof(AccountView));
                    break;

            }
        }

        private void SearchForItem(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            App.NavigateTo(typeof(SearchView), args.QueryText);
        }
    }
}