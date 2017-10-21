﻿using System;
using System.Collections.Generic;
using Android.Support.V7.Widget;
using Android.Views;
using SoundByte.Android.ViewHolders;
using SoundByte.Core.Items.Track;
using Uri = Android.Net.Uri;

namespace SoundByte.Android.Adapters
{
    public class TrackAdapter : RecyclerView.Adapter
    {
        // Event handler for item clicks:
        public event EventHandler<int> ItemClick;

        public List<BaseTrack> mBaseTrack;

        public TrackAdapter(List<BaseTrack> baseTrack)
        {
            mBaseTrack = baseTrack;
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            // Inflate the CardView for the photo:
            var itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.TrackItemView, parent, false);

            // Create a ViewHolder to find and hold these view references, and 
            // register OnClick with the view holder:
            return new TrackViewHolder(itemView, OnClick);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var vh = (TrackViewHolder)holder;

            // Set the ImageView and TextView in this ViewHolder's CardView 
            // from this position in the photo album:
            vh.Image.SetImageURI(Uri.Parse(mBaseTrack[position].ArtworkUrl));
            vh.Caption.Text = mBaseTrack[position].Title;
        }

        public override int ItemCount => mBaseTrack.Count;

        void OnClick(int position)
        {
            ItemClick?.Invoke(this, position);
        }
    }
}