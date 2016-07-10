using System;
using System.Globalization;
using System.Windows.Data;

namespace DiskImager
{
    public class HumanSizeConverter : IValueConverter
    {
        public static string HumanSize(double len)
        {
            int order = 0;
            string[] sizes = Properties.Resources.Units.Split(',');
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
