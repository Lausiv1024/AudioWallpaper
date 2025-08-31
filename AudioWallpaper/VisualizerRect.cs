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
        
        private double targetVal;
        private double currentVal;
        private double velocity;
        private double springStrength = 0.22;
        private double dampingFactor = 0.68;
        private double previousTargetVal = 0;
        private double attackMultiplier = 3.2;
        private double releaseMultiplier = 1.0;


        public VisualizerRect(int index)
        {
            this.index = index;
            rectangle = new Rectangle();
            rectangle.Fill = new SolidColorBrush(Colors.White);
            rectangle.Height = 8;
            MainWindow.instance.Visualizer.Children.Add(rectangle);

            currentVal = 0;
            targetVal = 0;
            velocity = 0;
            
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

        public void setTargetValue(double value)
        {
            targetVal = value;
        }
        
        public void animTick()
        {
            double force = (targetVal - currentVal) * springStrength;
            
            if (targetVal > currentVal)
            {
                double deltaTarget = targetVal - previousTargetVal;
                if (deltaTarget > 0)
                {
                    force *= attackMultiplier * (1.0 + Math.Min(deltaTarget / targetVal, 0.5));
                }
                else
                {
                    force *= attackMultiplier * 0.7;
                }
            }
            else
            {
                force *= releaseMultiplier;
            }
            
            velocity = velocity * dampingFactor + force;
            
            if (Math.Abs(velocity) > targetVal * 0.4 && targetVal > currentVal)
            {
                velocity = targetVal * 0.4;
            }
            
            currentVal += velocity;
            previousTargetVal = targetVal;
            
            if (currentVal < 0)
            {
                currentVal = 0;
                velocity = 0;
            }
            
            double fallSpeed = 0.9;
            if (targetVal < currentVal * 0.3)
            {
                currentVal *= (1 - fallSpeed);
            }
            
            val = currentVal;
            
            double h = MainWindow.instance.Visualizer.ActualHeight;
            rectangle.Height = val + 8;
            Canvas.SetTop(rectangle, h - 40 - val);
        }
    }
}
