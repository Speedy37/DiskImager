using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace DiskImager
{
    public class HumanSizeConverter : IValueConverter
    {
        static string[] sizes = Properties.Resources.Units.Split(',');
        public static string HumanSize(double len)
        {
            int order = 0;
            while (len >= 1024 && order + 1 < sizes.Length)
            {
                order++;
                len = len / 1024;
            }
            return String.Format("{0:0.00} {1}", len, sizes[order]);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return HumanSize((ulong)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DurationConverter : IValueConverter
    {
        static string[] sizes = null;
        static long[] transitions = null;
        public static string ToString(long duration, int precision = 2, int minimum = 1)
        {
            if (sizes == null)
            {
                var units = Properties.Resources.UnitsTimeShort.Split(',');
                sizes = new string[units.Length / 2 + 1];
                transitions = new long[units.Length / 2];
                for (int i = units.Length, j = 0; i > 0; j++)
                {
                    if (j % 2 == 0)
                        sizes[j / 2] = units[--i];
                    else
                        transitions[(j - 1) / 2] = System.Convert.ToInt64(units[--i]);
                }
            }

            int order = 0;
            List<string> parts = new List<string>();
            while (order + 1 < sizes.Length && duration >= transitions[order])
            {
                if (order >= minimum)
                    parts.Add(String.Format("{0}{1}", duration % transitions[order], sizes[order]));
                duration = duration / transitions[order];
                order++;
            }
            string ret = String.Format("{0}{1}", duration, sizes[order]);
            precision = Math.Min(parts.Count, precision - 1);
            for (order = 0; order < precision; ++order)
                ret += " " + parts[parts.Count - 1 - order];
            return ret;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DurationConverter.ToString((long)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DriveDescription : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DiskDrive)
            {
                DiskDrive drive = (DiskDrive)value;
                return String.Format("{0} ({1})", drive.Model, HumanSizeConverter.HumanSize(drive.Size));
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SourceDescription : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ESourceType)
            {
                ESourceType type = (ESourceType)value;
                return type.ToString();
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Enum.Parse(typeof(ESourceType), (string) value);
        }
    }
}
