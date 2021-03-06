﻿using System;
using Windows.UI.Text;
using Windows.UI.Xaml.Data;

namespace SoundByte.UWP.Converters
{
    /// <summary>
    ///     Converts a bool to a font weight. false = 400, true = 600
    /// </summary>
    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var boolVal = value as bool?;

            if (boolVal.HasValue)
                return new FontWeight
                {
                    Weight = boolVal.Value ? (ushort) 600 : (ushort) 400
                };

            return new FontWeight {Weight = 400};
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var fontWeightVal = (FontWeight) value;

            switch (fontWeightVal.Weight)
            {
                case 400:
                    return false;
                case 600:
                    return true;
                default:
                    return false;
            }
        }
    }
}