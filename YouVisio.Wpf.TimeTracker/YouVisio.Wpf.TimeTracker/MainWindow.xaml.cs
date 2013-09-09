using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;

namespace YouVisio.Wpf.TimeTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly LinkedList<TimeSegment> _linkedList = new LinkedList<TimeSegment>();
        private readonly Timer _timer = new Timer(1000);
        private TimeSpan _prevSegment = new TimeSpan(0);

        public MainWindow()
        {
            InitializeComponent();

            _timer.Elapsed += Timer_Elapsed;

            Closing += MainWindow_Closing;
            
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_timer.Enabled) Stop();
            var d = DateTime.Now;
            var node = _linkedList.First;
            var allTime = new TimeSpan(0);
            var sb = new StringBuilder();
            while (node != null)
            {
                var segment = node.Value;
                if (sb.Length == 0)
                {
                    sb.AppendLine(segment.Start.Year + "-" + segment.Start.Month.ToPadString(2) + "-" + segment.Start.Day.ToPadString(2));
                }
                sb.AppendLine(segment.Start.ToString("HH:mm:ss") + " - " + segment.End.ToString("HH:mm:ss") + " (" + segment.Span.Hours + "h " + segment.Span.Minutes + "m " + segment.Span.Seconds + "s)");
                allTime = allTime.Add(segment.Span);
                node = node.Next;
            }
            if (allTime.TotalSeconds < 1) return;
            sb.AppendLine("Time: " + allTime.Hours + "h " + allTime.Minutes + "m " + allTime.Seconds + "s");
            var path = (Directory.Exists(@"C:\Source\YouVisio\Logs\")) ? @"C:\Source\YouVisio\Logs\" : Path.GetFullPath(@".\");
            File.WriteAllText(path + @"log." +
                d.Year + "-" + d.Month.ToPadString(2) + "-" + d.Day.ToPadString(2) + " " +
                d.Hour.ToPadString(2) + "h " + d.Minute.ToPadString(2) + "m (" + allTime.Hours + "h " + (allTime.Minutes + ((allTime.Seconds > 30) ? 1 : 0)) + "m" + ").txt", sb.ToString());
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(new Action(() =>
                {
                    var time = _prevSegment + (DateTime.Now - _linkedList.Last.Value.Start);

                    TaskbarItemInfo.ProgressValue = Math.Max(Math.Min(time.TotalHours/8.0, 1.0), 0.15);

                    LblTime.Content = time.Hours + "h " + time.Minutes + "m " + time.Seconds + "s";
                }));
        }
        private void BtnPlay_OnClick(object sender, RoutedEventArgs e)
        {
            if (_timer.Enabled) Stop();
            else Play();
        }

        private void Play()
        {
            _timer.Start();
            BtnPlay.Background = Brushes.Green;
            BtnPlay.Content = "Stop";
            _linkedList.AddLast(new TimeSegment {Start = DateTime.Now, Count = _linkedList.Count + 1});

            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
        }

        private void Stop()
        {
            _timer.Stop();
            BtnPlay.Background = Brushes.DarkRed;
            BtnPlay.Content = "Play";
            _linkedList.Last.Value.End = DateTime.Now;
            _prevSegment = new TimeSpan(0);
            TxtLog.Text = "";
            var node = _linkedList.First;
            while (node != null)
            {
                var segment = node.Value;
                _prevSegment = _prevSegment.Add(segment.Span);
                TxtLog.Text += segment.Start.ToString("HH:mm:ss") + " - " + segment.End.ToString("HH:mm:ss") + " (" + segment.Span.Hours + "h " + segment.Span.Minutes + "m " + segment.Span.Seconds + "s)\n";
                node = node.Next;
            }

            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
        }
    }

    public class TimeSegment
    {
        public int Count { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public TimeSpan Span { get { return End - Start; } }

    }
}
