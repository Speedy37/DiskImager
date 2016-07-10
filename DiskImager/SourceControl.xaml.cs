using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using System.IO;
using System.Windows;
using System.ComponentModel;

namespace DiskImager
{
    public enum ESourceType
    {
        None,
        Drive,
        File
    }

    public enum ESourceSide
    {
        ReadSource,
        WriteSource
    }

    public interface ISource
    {
        ESourceType SourceType { get; }
        string ReadDescription { get; }
        string WriteDescription { get; }
        long ReadSize { get; }
        Stream ReadStream();
        long WriteSize { get; }
        Stream WriteStream();
    }
    
    public partial class SourceControl : UserControl
    {
        private static List<ESourceType> sources = new List<ESourceType>() {
            ESourceType.Drive,
            ESourceType.File
        };

        private static List<DependencyProperty> SourcesProperties = new List<DependencyProperty>()
        {
            DependencyProperty.Register(
                "DriveSource", typeof(ISource), typeof(SourceControl),
                new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSubSourceValueChanged))),
            DependencyProperty.Register(
                "FileSource", typeof(ISource), typeof(SourceControl),
                new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSubSourceValueChanged)))
        };

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(
                "Source", typeof(ISource), typeof(SourceControl),
                new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSourceValueChanged)));

        public static readonly DependencyProperty SourceTypeProperty =
            DependencyProperty.Register(
                "SourceType", typeof(ESourceType), typeof(SourceControl),
                new FrameworkPropertyMetadata(ESourceType.None, new PropertyChangedCallback(OnSourceTypeValueChanged)));
        
        public static readonly DependencyProperty SourceSideProperty =
            DependencyProperty.Register(
                "SourceSide", typeof(ESourceSide), typeof(SourceControl),
                new FrameworkPropertyMetadata(ESourceSide.ReadSource, new PropertyChangedCallback(OnSourceSideValueChanged)));

        public static readonly DependencyProperty CaptionProperty =
            DependencyProperty.Register(
                "Caption", typeof(string), typeof(DriveSelection),
                new FrameworkPropertyMetadata(null));

        private static void OnSubSourceValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            SourceControl control = (SourceControl)obj;
            var idx = sources.IndexOf(control.SourceType);
            if (idx != -1 && SourcesProperties[idx] == args.Property)
                control.Source = (ISource) args.NewValue;
        }

        private static void OnSourceValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            SourceControl control = (SourceControl)obj;
            if (control.Source != null)
                control.SourceType = control.Source.SourceType;
            var idx = sources.IndexOf(control.SourceType);
            if (idx != -1)
                control.SetValue(SourcesProperties[idx], control.Source);
        }

        private static void OnSourceTypeValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            SourceControl control = (SourceControl)obj;
            var type = control.SourceType;
            var isDrive = type == ESourceType.Drive;
            int i = 0;
            foreach (ESourceType sType in sources)
            {
                control.BindSource(i++).Visibility = type == sType ? Visibility.Visible : Visibility.Hidden;
            }
            var idx = sources.IndexOf(control.SourceType);
            if (idx != -1)
                control.Source = (ISource) control.GetValue(SourcesProperties[idx]);
        }

        private static void OnSourceSideValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            SourceControl control = (SourceControl)obj;
            control.image.SourceSide = control.SourceSide;
        }

        public SourceControl()
        {
            InitializeComponent();
            DataContext = this;
            int i = 0;
            foreach (DependencyProperty prop in SourcesProperties)
            {
                Binding binding = new Binding("Source");
                binding.Source = BindSource(i++);
                binding.Mode = BindingMode.TwoWay;
                SetBinding(prop, binding);
            }
        }

        private ContentControl BindSource(int idx)
        {
            return idx == 0 ? (ContentControl)drive : (ContentControl)image;
        }

        public ISource Source
        {
            get { return (ISource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public string Caption
        {
            get { return (string)GetValue(CaptionProperty); }
            set { SetValue(CaptionProperty, value); }
        }

        public ESourceType SourceType
        {
            get { return (ESourceType)GetValue(SourceTypeProperty); }
            set { SetValue(SourceTypeProperty, value); }
        }

        public ESourceSide SourceSide
        {
            get { return (ESourceSide)GetValue(SourceSideProperty); }
            set { SetValue(SourceSideProperty, value); }
        }

        public List<ESourceType> Sources {
            get { return sources; }
        }
    }
}
