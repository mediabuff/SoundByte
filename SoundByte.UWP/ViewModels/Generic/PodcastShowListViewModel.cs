﻿using SoundByte.Core.Items.Podcast;
using SoundByte.Core.Sources;
using SoundByte.UWP.Helpers;

namespace SoundByte.UWP.ViewModels.Generic
{
    public class PodcastShowListViewModel : BaseViewModel
    {
        #region Bindings
        /// <summary>
        /// The title to be displayed on the page.
        /// </summary>
        public string Title
        {
            get => _title;
            set
            {
                if (value == _title)
                    return;

                _title = value;
                UpdateProperty();
            }
        }
        private string _title;

        /// <summary>
        /// Subtitle to be displayed on the page.
        /// </summary>
        public string SubTitle
        {
            get => _subTitle;
            set
            {
                if (value == _subTitle)
                    return;

                _subTitle = value;
                UpdateProperty();
            }
        }
        private string _subTitle;


        /// <summary>
        /// The podcast model to show on this page
        /// </summary>
        public SoundByteCollection<ISource<BasePodcast>, BasePodcast> Model
        {
            get => _model;
            set
            {
                if (value == _model)
                    return;

                _model = value;
                UpdateProperty();
            }
        }
        private SoundByteCollection<ISource<BasePodcast>, BasePodcast> _model;
        #endregion

        #region Methods
        /// <summary>
        /// Setup the view model for use
        /// </summary>
        /// <param name="data">The podcast model to use in this view</param>
        public void Init(PodcastShowViewModelHolder data)
        {
            Model = new SoundByteCollection<ISource<BasePodcast>, BasePodcast>(data.PodcastSource);
            Title = data.Title;
            SubTitle = data.Subtitle;
        }
        #endregion

        public class PodcastShowViewModelHolder
        {
            public ISource<BasePodcast> PodcastSource { get; set; }
            public string Title { get; set; }
            public string Subtitle { get; set; }
        }
    }

}
