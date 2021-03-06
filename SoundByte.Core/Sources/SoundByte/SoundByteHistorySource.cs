﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SoundByte.Core.Helpers;
using SoundByte.Core.Items.SoundByte;
using SoundByte.Core.Items.Track;
using SoundByte.Core.Services;

namespace SoundByte.Core.Sources.SoundByte
{
    [UsedImplicitly]
    public class SoundByteHistorySource : ISource<BaseTrack>
    {
        public Dictionary<string, object> GetParameters()
        {
            return new Dictionary<string, object>();
        }

        public void ApplyParameters(Dictionary<string, object> data)
        {
            // Not used
        }

        public async Task<SourceResponse<BaseTrack>> GetItemsAsync(int count, string token,
            CancellationTokenSource cancellationToken = default(CancellationTokenSource))
        {
            // If the user has not connected their SoundByte account.
            if (!SoundByteService.Current.IsSoundByteAccountConnected)
            {
                return await Task.Run(() =>
                    new SourceResponse<BaseTrack>(null, null, false,
                        Resources.Resources.Sources_SoundByte_NoAccount_Title,
                        Resources.Resources.Sources_SoundByte_History_NoAccount_Description));
            }

            var history = await SoundByteService.Current.GetAsync<HistoryOutputModel>(ServiceType.SoundByte, "history", new Dictionary<string, string>
            {
                { "PageNumber", token },
                { "PageSize", "30" }
            }, cancellationToken).ConfigureAwait(false);

            if (!history.Response.Items.Any())
            {
                return new SourceResponse<BaseTrack>(null, null, false, "No History", "Start listening to songs to build up your history.");
            }

            var nextPage = history.Response.Links.FirstOrDefault(x => x.Rel == "next_page");

            if (nextPage == null)
                return new SourceResponse<BaseTrack>(history.Response.Items, "eol");

            var param = new QueryParameterCollection(nextPage.Href);
            var nextToken = param.FirstOrDefault(x => x.Key == "PageNumber").Value;

            return new SourceResponse<BaseTrack>(history.Response.Items, nextToken);
        }


        public class HistoryOutputModel
        {
            public PagingHeader Paging { get; set; }
            public List<LinkInfo> Links { get; set; }
            public List<BaseTrack> Items { get; set; }
        }
    }
}
