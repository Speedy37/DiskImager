using Microsoft.Win32;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.IO;

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
                Source = image_path.Text.Length > 0 ? type.LoadImageAt(image_path.Text) : null;
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
    }
}
