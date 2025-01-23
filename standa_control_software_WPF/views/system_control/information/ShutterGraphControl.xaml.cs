using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using System.Windows.Media;

namespace standa_control_software_WPF.views.system_control.information
{
    /// <summary>
    /// Interaction logic for PositionTrackingControl.xaml
    /// </summary>
    public partial class ShutterGraphControl : UserControl
    {
        public ShutterGraphControl()
        {
            InitializeComponent(); InitializeComponent();
            this.Unloaded += PositionTrackerView_Unloaded;
            this.Loaded += PositionTrackerView_Loaded;
        }

        private void PositionTrackerView_Loaded(object sender, RoutedEventArgs e)
        {
            var oxyColorDark = OxyColors.DarkGray;
            var oxyColorMid = OxyColors.Gray;
            var oxyColorLight = OxyColors.LightGray;
            var oxyColorWhite = OxyColors.White;
            if (Application.Current.Resources["MidCustomColorBrush"] is SolidColorBrush darkBrush)
            {
                var wpfColor = darkBrush.Color;
                oxyColorDark = OxyColor.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
            }
            if (Application.Current.Resources["DarkCustomColorBrush"] is SolidColorBrush midBrush)
            {
                var wpfColor = midBrush.Color;
                oxyColorMid = OxyColor.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
            }
            if (Application.Current.Resources["LightCustomColorBrush"] is SolidColorBrush lightBrush)
            {
                var wpfColor = lightBrush.Color;
                oxyColorLight = OxyColor.FromArgb(50, wpfColor.R, wpfColor.G, wpfColor.B);
            }
            if (Application.Current.Resources["FontColorBrush"] is SolidColorBrush whiteBrush)
            {
                var wpfColor = whiteBrush.Color;
                oxyColorWhite = OxyColor.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
            }

            if (PositionPlot.Model != null)
            {
                var plotModel = PositionPlot.Model;

                plotModel.PlotAreaBackground = OxyColors.Transparent;
                plotModel.PlotAreaBorderColor = oxyColorWhite;
                plotModel.Background = OxyColors.Transparent;
                plotModel.TextColor = oxyColorWhite;
                plotModel.Title = "";
                

                foreach (var axis in plotModel.Axes)
                {
                    axis.TextColor = oxyColorWhite;
                    axis.AxislineColor = oxyColorWhite;
                    axis.TicklineColor = oxyColorWhite;
                    axis.ExtraGridlineColor = oxyColorWhite;
                    axis.MajorGridlineColor = oxyColorLight;
                    axis.MinorGridlineColor = oxyColorWhite;
                    axis.MajorGridlineStyle = LineStyle.Solid;    
                }

                // Important: refresh the plot to apply changes
                plotModel.InvalidatePlot(true);
            }

        }

        private void PositionTrackerView_Unloaded(object sender, RoutedEventArgs e)
        {
            PositionPlot.Model = null;
        }
    }
}
