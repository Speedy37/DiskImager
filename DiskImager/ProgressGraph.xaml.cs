using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DiskImager
{
    using OxyPlot;
    using OxyPlot.Annotations;
    using OxyPlot.Axes;
    using OxyPlot.Series;

    /// <summary>
    /// Represents the view-model for the main window.
    /// </summary>
    public class MainViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel" /> class.
        /// </summary>
        public MainViewModel()
        {
        }

        /// <summary>
        /// Gets the plot model.
        /// </summary>
        public PlotModel Model { get; private set; }
    }

    internal class MyAxis : LinearAxis
    {
        internal MyAxis()
        {
            TickStyle = TickStyle.None;
            IsPanEnabled = false;
            IsZoomEnabled = false;
            MinorGridlineStyle = LineStyle.Solid;
            MinorGridlineThickness = 1.0;
            MinorGridlineColor = OxyColor.FromRgb(205, 239, 211);
            Minimum = 0.0;
            AxisDistance = 0;
            AxisTickToLabelDistance = 0;
        }

        public override void GetTickValues(
            out IList<double> majorLabelValues, out IList<double> majorTickValues, out IList<double> minorTickValues)
        {
            base.GetTickValues(out majorLabelValues, out majorTickValues, out minorTickValues);
            majorLabelValues = majorTickValues = new List<double>();
        }
    }

    /// <summary>
    /// Interaction logic for ProgressGraph.xaml
    /// </summary>
    public partial class ProgressGraph : UserControl
    {
        private RectangleAnnotation back;
        private AreaSeries values;
        private LineAnnotation line;
        private MyAxis xAxis;

        public ProgressGraph()
        {
            InitializeComponent();
            
            // Create the plot model
            var tmp = new PlotModel { };

            tmp.PlotAreaBorderColor = OxyColor.FromRgb(230, 230, 230);
            tmp.PlotAreaBorderThickness = new OxyThickness(1.0);
            tmp.PlotMargins = new OxyThickness(0.0);
            tmp.IsLegendVisible = false;
            tmp.Padding = new OxyThickness(0.0, 0.0, 1.0, 1.0);
            
            values = new AreaSeries();
            values.LineStyle = LineStyle.None;
            values.Fill = OxyColor.FromRgb(6, 176, 37);
            values.MarkerType = MarkerType.None;

            line = new LineAnnotation();
            line.Type = LineAnnotationType.Horizontal;
            line.Color = OxyColor.FromRgb(0, 0, 0);
            line.LineStyle = LineStyle.Solid;
            line.TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom;
            line.Y = 0;
            
            xAxis = new MyAxis();
            xAxis.Position = AxisPosition.Left;
            
            var yAxis = new MyAxis();
            yAxis.Position = AxisPosition.Bottom;
            yAxis.Maximum = 1.0;
            yAxis.MinorStep = 0.1;

            back = new RectangleAnnotation();
            back.Fill = OxyColor.FromArgb(125, 86, 201, 48);
            back.Layer = AnnotationLayer.BelowSeries;
            back.MaximumX = 0.0;

            tmp.Axes.Add(xAxis);
            tmp.Axes.Add(yAxis);
            tmp.Series.Add(values);
            tmp.Annotations.Add(back);
            tmp.Annotations.Add(line);
            
            this.Model = tmp;
            DataContext = this;
            Reset();
        }

        public void Reset()
        {
            values.Points.Clear();
            line.Y = 0.0;
            line.Text = "";
            back.MaximumX = 0.0;
            xAxis.Maximum = 1;
            xAxis.MinorStep = 0.2;
            this.Model.InvalidatePlot(true);
        }

        public void AddValue(double progress, double value, string label)
        {
            values.Points.Add(new DataPoint(progress, value));
            line.Text = label;
            line.Y = value;
            back.MaximumX = progress;
            double top = value * 5.0 / 3.0;
            if (xAxis.Maximum < top)
            {
                xAxis.Maximum = top;
                xAxis.MinorStep = top / 5;
            }
            this.Model.InvalidatePlot(true);
        }

        /// <summary>
        /// Gets the plot model.
        /// </summary>
        public PlotModel Model { get; private set; }
    }
}
