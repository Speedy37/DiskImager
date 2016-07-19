using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            long[] remainings = new long[50]; // soften the remaining ms on 5 sec
            long idx = 0;
            return new Progress<CloneProgression>(p => {
                clone_progression_graph.AddValue(
                    (double)p.written / p.total, 
                    p.stepBytesPerSeconds, 
                    String.Format(Properties.Resources.SpeedFormat, HumanSizeConverter.HumanSize(p.stepBytesPerSeconds))
                    );
                remainings[idx++ % remainings.Length] = (1000 * (p.total - p.written)) / p.stepBytesPerSeconds;
                clone_progression_written.Text = String.Format(Properties.Resources.ProgressWrittenFormat, HumanSizeConverter.HumanSize(p.written), HumanSizeConverter.HumanSize(p.total));
                clone_progression_time_elapsed.Text = String.Format(Properties.Resources.ProgressTimeElapsedFormat, DurationConverter.ToString(p.elapsed));
                if (idx >= remainings.Length)
                {
                    long r = Convert.ToInt64(remainings.Average());
                    clone_progression_time_remaining.Text = String.Format(Properties.Resources.ProgressTimeRemainingFormat, DurationConverter.ToString(r));
                }
            });
        }

        private CancellationToken token()
        {
            cancellationTokenSource = new CancellationTokenSource();
            return cancellationTokenSource.Token;
        }

        private delegate bool RunAction(Progress<CloneProgression> progress, CancellationToken token);
        private void changeVisibility(bool cloning)
        {
            clone_cancel.Visibility = cloning ? Visibility.Visible : Visibility.Collapsed;
            grid_progression.Visibility = cloning ? Visibility.Visible : Visibility.Collapsed;
            do_clone.Visibility = !cloning ? Visibility.Visible : Visibility.Collapsed;
            grid_selection.Visibility = !cloning ? Visibility.Visible : Visibility.Collapsed;
        }
        private async void run(RunAction action)
        {
            var progress = progression();
            var cancellationToken = token();
            changeVisibility(true);
            Exception exception = null;
            bool result = await (Task.Run(() =>
            {
                try
                {
                    return action(progress, cancellationToken);
                }
                catch (Exception e)
                {
                    exception = e;
                    return false;
                }
            }, cancellationToken));
            if (exception != null)
            {
                MessageBox.Show(
                    String.Format(Properties.Resources.CloneErrorFormat, exception.ToString()),
                    Properties.Resources.Clone,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(
                    result ? Properties.Resources.CloneSuccess : Properties.Resources.CloneAborted,
                    Properties.Resources.Clone,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            DiskDrive.Shared.Refresh();
            changeVisibility(false);
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

        private void do_refresh_Click(object sender, RoutedEventArgs e)
        {
            DiskDrive.Shared.Refresh();
        }

        private void do_clone_Click(object sender, RoutedEventArgs e)
        {
            var src = input.Source;
            var dst = output.Source;
            if (src == null || dst == null || src == dst)
            {
                MessageBox.Show(
                    src == null 
                        ? Properties.Resources.NoSourceSelected : (src != dst 
                        ? Properties.Resources.NoDestinationSelected 
                        : Properties.Resources.SameSourceAndDestination),
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
                if (!accept)
                    return;
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

            clone_progression_src.Text = String.Format(Properties.Resources.ProgressSourceFormat, src.ReadDescription);
            clone_progression_dst.Text = String.Format(Properties.Resources.ProgressDestinationFormat, dst.WriteDescription);
            clone_progression_written.Text = String.Format(Properties.Resources.ProgressWrittenFormat, HumanSizeConverter.HumanSize(0), HumanSizeConverter.HumanSize(src.ReadSize));
            clone_progression_time_elapsed.Text = String.Format(Properties.Resources.ProgressTimeElapsedFormat, DurationConverter.ToString(0));
            clone_progression_time_remaining.Text = String.Format(Properties.Resources.ProgressTimeRemainingFormat, Properties.Resources.ProgressTimeUndefined);
            clone_progression_graph.Reset();
            run((progress, cancellationToken) =>
            {
                using (var writeStream = dst.WriteStream())
                using (var readStream = src.ReadStream())
                    return CloneTask.clone(readStream, writeStream, srcSize, cancellationToken, progress);
            });
        }

        private void clone_cancel_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource?.Cancel();
        }
    }

}
