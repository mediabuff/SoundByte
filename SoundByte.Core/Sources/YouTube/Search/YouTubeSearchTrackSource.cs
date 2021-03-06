﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SoundByte.Core.Items.Track;
using SoundByte.Core.Items.YouTube;
using SoundByte.Core.Services;

namespace SoundByte.Core.Sources.YouTube.Search
{
    [UsedImplicitly]
    public class YouTubeSearchTrackSource : ISource<BaseTrack>
    {
        /// <summary>
        /// What we should search for
        /// </summary>
        public string SearchQuery { get; set; }

        public Dictionary<string, object> GetParameters()
        {
            return new Dictionary<string, object>
            {
                { "q", SearchQuery }
            };
        }

        public void ApplyParameters(Dictionary<string, object> data)
        {
            data.TryGetValue("q", out var query);
            SearchQuery = query.ToString();
        }

        public async Task<SourceResponse<BaseTrack>> GetItemsAsync(int count, string token,
            CancellationTokenSource cancellationToken = default(CancellationTokenSource))
        {
            // Call the YouTube API and get the items
            var tracks = await SoundByteService.Current.GetAsync<YouTubeVideoHolder>(ServiceType.YouTube, "search",
                new Dictionary<string, string>
                {
                    {"part", "id"},
                    {"maxResults", count.ToString()},
                    {"pageToken", token},
                    {"type", "video"},
                    {"q", WebUtility.UrlEncode(SearchQuery)}
                }, cancellationToken).ConfigureAwait(false);

            // If there are no tracks
            if (!tracks.Response.Tracks.Any())
            {
                return new SourceResponse<BaseTrack>(null, null, false, "No results found", "Could not find any results for '" + SearchQuery + "'");
            }

            // We now need to get the content details (ugh)
            var youTubeIdList = string.Join(",", tracks.Response.Tracks.Select(m => m.Id.VideoId));

            var extendedTracks = await SoundByteService.Current.GetAsync<YouTubeVideoHolder>(ServiceType.YouTube, "videos",
                new Dictionary<string, string>
                {
                    {"part", "snippet,contentDetails"},
                    { "id", youTubeIdList }
                }, cancellationToken).ConfigureAwait(false);

            // Convert YouTube specific tracks to base tracks
            var baseTracks = new List<BaseTrack>();
            foreach (var track in extendedTracks.Response.Tracks)
            {
                if (track.Id.Kind == "youtube#video")
                {
                    baseTracks.Add(track.ToBaseTrack());
                }
            }

            // Return the items
            return new SourceResponse<BaseTrack>(baseTracks, tracks.Response.NextList);
        }
    }
}
