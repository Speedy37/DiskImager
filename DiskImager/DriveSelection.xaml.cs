using PropertyChanged;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System;
using System.Collections.Specialized;
using System.Windows.Data;
using System.Globalization;

namespace DiskImager
{
    [ImplementPropertyChanged]
    public partial class DriveSelection : UserControl
    {
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(
                "Source", typeof(ISource), typeof(DriveSelection),
                new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnValueChanged)));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DriveSelection self = (DriveSelection)d;
            self.DriveListChanged();
        }

        public DriveSelection()
        {
            InitializeComponent();
            DataContext = this;
            DiskDrive.Shared.Changed += RefreshDriveList;
            RefreshDriveList();
        }

        private void RefreshDriveList()
        {
            if (!Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Dispatcher.Invoke(() => { RefreshDriveList(); });
                return;
            }
            Drives = DiskDrive.Shared.Drives.FindAll(x => IsExternalDrive(x));
            DriveListChanged();
        }

        private void DriveListChanged()
        {
            var drive = Source;
            if (drive != null)
                drive = Drives.Find(x => x.DeviceID == ((DiskDrive)drive).DeviceID);
            else
                drive = Drives.Find(x => true);
            Source = drive;
        }
        
        public bool IsExternalDrive(DiskDrive disk)
        {
            var type = disk.MediaType;
            var busType = disk.InterfaceType;
            return (type == DiskDrive.EMediaType.RemovableMedia && busType != DiskDrive.EInterfaceType.IDE) ||
                   (type == DiskDrive.EMediaType.External) ||
                   (type == DiskDrive.EMediaType.FixedHardDisk && busType == DiskDrive.EInterfaceType.USB);
        }

        public List<DiskDrive> Drives { get; set; }

        public ISource Source
        {
            get { return (ISource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }
    }
}
