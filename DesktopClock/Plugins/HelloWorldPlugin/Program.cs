using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopClock.Components;
using DesktopClock.Contracts;
using DesktopClock.Models;
using DesktopClock.Services;

namespace HelloWorldPlugin
{
    public class HelloWorldPlugin : IPlugin
    {
        private PluginHost? _host;

        public string Id => "hello_world";
        public string Name => "Hello World Plugin";
        public string Version => "1.0.0";
        public string Description => "Simple example plugin showing the plugin system works. Adds a Hello World component.";

        public void Load(PluginHost host)
        {
            _host = host;
            _host.Log("Hello World Plugin loaded");

            var helloWorldControl = new HelloWorldControl();
            _host.RegisterComponent(Id, helloWorldControl);
            _host.Log("Hello World component registered");
        }

        public void Unload()
        {
            _host?.Log("Hello World Plugin unloaded");
        }
    }

    public class HelloWorldControl : UserControl
    {
        private readonly DispatcherTimer _timer = new();
        private DateTime _startTime = DateTime.Now;

        public HelloWorldControl()
        {
            Width = double.NaN;
            Height = double.NaN;
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            
            // FIX: Use variable for CornerRadius (not property)
            var containerCornerRadius = new CornerRadius(8);
            CornerRadius = containerCornerRadius;
            
            Padding = new Thickness(12);
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
            BorderThickness = new Thickness(1);

            var stackPanel = new StackPanel();
            var title = new TextBlock
            {
                Text = "Hello World!",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            var message = new TextBlock
            {
                Text = "This plugin was dynamically loaded by the plugin system.\nClick and drag me to reposition!",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.WordWrap
            };

            stackPanel.Children.Add(title);
            stackPanel.Children.Add(message);
            Content = stackPanel;

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _startTime;
            var status = new TextBlock
            {
                Text = "Status: Running...\n(Uptime: " + (int)elapsed.TotalSeconds + " seconds)",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };
            ((StackPanel)Content).Children.Add(status);
        }
    }
}