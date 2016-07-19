using Microsoft.Win32;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DiskImager.Tasks;
using System.Security.Cryptography;

namespace DiskImager
{
    public partial class ImageSelection : UserControl
    {
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(
                "Source", typeof(ISource), typeof(ImageSelection),
                new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSourceValueChanged)));

        public ImageSelection()
        {
            InitializeComponent();
            checksum_layout.Visibility = Visibility.Collapsed;
            this.DataContext = this;
        }

        private static void OnSourceValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            ImageSelection control = (ImageSelection)obj;
            IDiskImage image = control.Source is IDiskImage ? (IDiskImage)control.Source : null;
            control.image_path.Text = image?.Path;
            control.image_type.SelectedValue = image?.Type;
            control.image_info.Text = control.SourceSide == ESourceSide.ReadSource
                ? image?.ReadDescription
                : image?.WriteDescription;
        }

        private void image_browse_Click(object sender, RoutedEventArgs e)
        {
            string filters = String.Join("", Types.ConvertAll(t => t.Description + "|*" + String.Join(";*", t.Extensions) + "|"));
            string exts = String.Join(";", Types.ConvertAll(t => "*" + String.Join(";*", t.Extensions)));
            string filter = filters + Properties.Resources.AllImages +"|" + exts + "|" + Properties.Resources.AllFiles + " (*.*)|*.*";

            FileDialog dialog = SourceSide == ESourceSide.ReadSource 
                ? (FileDialog)new OpenFileDialog() 
                : (FileDialog)new SaveFileDialog();
            dialog.Filter = filter;
            dialog.FilterIndex = Types.Count + 1;
            if (dialog.ShowDialog() == true)
            {
                image_path.Text = dialog.FileName;
            }
        }

        private void image_path_TextChanged(object sender, TextChangedEventArgs ev)
        {
            var path = image_path.Text;
            var type = Types.Find(t => Array.Find(t.Extensions, e => path.EndsWith(e)) != null);
            if (type != null && type != image_type.SelectedItem)
                image_type.SelectedItem = type;
            else
                image_type_SelectionChanged(null, null);
        }

        private void image_type_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var type = (IDiskImageType)image_type.SelectedItem;
            if (type != null)
            {
                var source = image_path.Text.Length > 0 ? type.LoadImageAt(image_path.Text) : null;
                computeVisibility(source != null && SourceSide == ESourceSide.ReadSource);
                Source = source;
            }
        }

        public List<IDiskImageType> Types { get { return DiskImageTypes.types; } }

        public ISource Source
        {
            get { return (ISource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }
        
        public ESourceSide SourceSide
        {
            get;
            set;
        }


        private CancellationTokenSource cancellationTokenSource;

        private void computeVisibility(bool canComputeChecksum)
        {
            checksum_layout.Visibility = canComputeChecksum ? Visibility.Visible : Visibility.Collapsed;
            if (canComputeChecksum)
            {
                checksum_cancel_Click(null, null);
                changeVisibility(false, false);
            }
        }

        private void changeVisibility(bool computed, bool inprogress)
        {
            checksum_calculate.Visibility = !inprogress ? Visibility.Visible : Visibility.Collapsed;
            checksum_cancel.Visibility = inprogress ? Visibility.Visible : Visibility.Collapsed;
            checksum_progress.Visibility = !computed || inprogress ? Visibility.Visible : Visibility.Collapsed;
            checksum_result.Visibility = computed ? Visibility.Visible : Visibility.Collapsed;
            checksum_progress.Value = 0;
        }

        private Progress<CloneProgression> progression()
        {
            return new Progress<CloneProgression>(p =>
            {
                checksum_progress.Value = 100 * p.written / p.total;
            });
        }

        private CancellationToken token()
        {
            checksum_cancel_Click(null, null);
            cancellationTokenSource = new CancellationTokenSource();
            return cancellationTokenSource.Token;
        }

        private void checksum_cancel_Click(object sender, RoutedEventArgs e)
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource = null;
            }
        }

        private async void checksum_calculate_Click(object _obj, RoutedEventArgs _args)
        {
            var progress = progression();
            var cancellationToken = token();
            changeVisibility(false, true);
            Exception exception = null;
            var hashName = (string)((ComboBoxItem)checksum_type.SelectedValue).Content;
            var src = Source;
            var result = await(Task.Run(() =>
            {
                try
                {
                    HashAlgorithm hash = HashAlgorithm.Create(hashName);
                    return ChecksumTask.checksum(src.ReadStream(), src.ReadSize, hash, cancellationToken, progress);
                }
                catch (Exception e)
                {
                    exception = e;
                    return null;
                }
            }, cancellationToken));
            if (exception != null)
            {
                MessageBox.Show(
                    String.Format(Properties.Resources.ChecksumErrorFormat, exception.ToString()),
                    Properties.Resources.Checksum,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            if (result != null)
            {
                checksum_result.Text = result;
            }
            changeVisibility(result != null, false);
        }
    }
}
