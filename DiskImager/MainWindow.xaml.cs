using Microsoft.Win32;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

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
                clone_progression_text.Text = "Error while cloning disk";
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
                    src == null ? "No source selected" : "No destination selected",
                    "Clone",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var accept = true;
            var srcSize = src.ReadSize;
            var dstSize = dst.WriteSize;
            if (srcSize > dstSize)
            {
                accept = MessageBox.Show(
                String.Format("Source size ({0}) is bigger than destination size ({1})\n"
                    + "The cloning operation is likely to fail\n"
                    + "Do you want to continue anyway?",
                    HumanSizeConverter.HumanSize(srcSize),
                    HumanSizeConverter.HumanSize(dstSize)), 
                "Clone",
                MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No) == MessageBoxResult.Yes;
            }
            accept = MessageBox.Show(
                String.Format("This utility will now clone data\n\n"
                    + "from: {0}\n"
                    + "to: {1}.\n\n"
                    + "This operation is not reversible and will replace ALL DATA of the selected destination.\n"
                    + "Do you confirm this is what you want?",
                    src.ReadDescription,
                    dst.WriteDescription),
                "Clone",
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
