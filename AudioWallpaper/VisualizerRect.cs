using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AudioWallpaper
{
    internal class VisualizerRect
    {
        public Rectangle rectangle { get; set; }
        public int index = 0;
        public double val;


        public VisualizerRect(int index)
        {
            this.index = index;
            rectangle = new Rectangle();
            rectangle.Fill = new SolidColorBrush(Colors.White);
            rectangle.Height = 8;
            MainWindow.instance.Visualizer.Children.Add(rectangle);

            respondToResize();
            animTick();
        }

        public void respondToResize()
        {
            double w = MainWindow.instance.Visualizer.ActualWidth;
            double h = MainWindow.instance.Visualizer.ActualHeight;
            rectangle.Width = w / MainWindow.instance.detail / 2;
            Canvas.SetLeft(rectangle, (w / MainWindow.instance.detail) * index);
            Canvas.SetTop(rectangle, h - 40 - val);
        }

        public void animTick()
        {
            double h = MainWindow.instance.Visualizer.ActualHeight;
            rectangle.Height = val + 8;
            Canvas.SetTop(rectangle, h - 40 - val);
        }
    }
}
