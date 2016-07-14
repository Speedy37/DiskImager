using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DiskImager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource cancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            DiskDrive.Shared.ListenForDriveChanges();
        }

        protected override void OnClosed(EventArgs e) 
        {
            DiskDrive.Shared.StopListening();
        }

        private Progress<CloneProgression> progression()
        {
            return new Progress<CloneProgression>(p => {
                clone_progression_value.Value = p.clonedBytes * 1000 / p.totalBytes;

                clone_progression_text.Text = String.Format(Properties.Resources.ProgressionFormat, 
                    ((double)p.clonedBytes * 100) / p.totalBytes,
                    HumanSizeConverter.HumanSize(p.bytesPerSeconds),
                    HumanSizeConverter.HumanSize(p.clonedBytes),
                    HumanSizeConverter.HumanSize(p.totalBytes));
            });
        }

        private CancellationToken token()
        {
            cancellationTokenSource = new CancellationTokenSource();
            return cancellationTokenSource.Token;
        }

        private delegate bool RunAction(Progress<CloneProgression> progress, CancellationToken token);
        private async void run(RunAction action)
        {
            var progress = progression();
            var cancellationToken = token();
            clone_cancel.Visibility = Visibility.Visible;
            do_clone.Visibility = Visibility.Hidden;
            int result = await (Task.Run(() =>
            {
                try
                {
                    return action(progress, cancellationToken) ? 1 : 0;
                }
                catch
                {
                    return 2;
                }
            }, cancellationToken));
            if (result == 2)
            {
                clone_progression_value.Value = 1000;
                clone_progression_text.Text = Properties.Resources.CloneError;
            }
            clone_cancel.Visibility = Visibility.Hidden;
            do_clone.Visibility = Visibility.Visible;
        }

        private void do_exchange_Click(object sender, RoutedEventArgs e)
        {
            var src = input.Source;
            var dst = output.Source;
            var srcType = input.SourceType;
            var dstType = output.SourceType;
            input.SourceType = dstType;
            output.SourceType = srcType;
            input.Source = dst;
            output.Source = src;
        }

        private void do_clone_Click(object sender, RoutedEventArgs e)
        {
            var src = input.Source;
            var dst = output.Source;
            if (src == null || dst == null)
            {
                MessageBox.Show(
                    src == null ? Properties.Resources.NoSourceSelected : Properties.Resources.NoDestinationSelected,
                    Properties.Resources.Clone,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var accept = true;
            var srcSize = src.ReadSize;
            var dstSize = dst.WriteSize;
            if (srcSize > dstSize)
            {
                accept = MessageBox.Show(
                String.Format(Properties.Resources.CloneWarningSizeFormat,
                    HumanSizeConverter.HumanSize(srcSize),
                    HumanSizeConverter.HumanSize(dstSize)),
                Properties.Resources.Clone,
                MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No) == MessageBoxResult.Yes;
            }
            accept = MessageBox.Show(
                String.Format(Properties.Resources.CloneWarningFormat,
                    src.ReadDescription,
                    dst.WriteDescription),
                Properties.Resources.Clone,
                MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No) == MessageBoxResult.Yes;
            if (!accept)
                return;
            run((progress, cancellationToken) =>
            {
                using (var readStream = src.ReadStream())
                using (var writeStream = dst.WriteStream())
                    return CloneTask.clone(readStream, writeStream, srcSize, progress, cancellationToken);
            });
        }

        private void clone_cancel_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource?.Cancel();
        }
    }

}
