using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

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


            LoadPreviousLinkedList();
        }

        private void LoadPreviousLinkedList()
        {

            var col = GetMongoCollection("time_tracker");
            var d = DateTime.Now;
            var day = d.Year.ToPadString(4) + "-" + d.Month.ToPadString(2) + "-" + d.Day.ToPadString(2);

            var prev = col.FindOne(Query.EQ("day", day));
            if (prev == null) return;

            var i = 0;
            foreach(BsonDocument seg in prev["segments"].AsBsonArray)
            {
                var ts = GetTimeSegment(seg["start"].AsString, seg["end"].AsString);
                _prevSegment += ts.Span;
                ts.Count = ++i;
                _linkedList.AddLast(ts);
            }

            SetPreviousTimesFromLinkedList();

            var time = _prevSegment;
            LblTime.Content = time.Hours + "h " + time.Minutes + "m " + time.Seconds + "s";
        }

        private TimeSegment GetTimeSegment(string start, string end)
        {
            var d = DateTime.Now;
            var sa = start.Split(':').Select(Int32.Parse).ToArray();
            var ea = end.Split(':').Select(Int32.Parse).ToArray();
            return new TimeSegment
                       {
                           Start = new DateTime(d.Year, d.Month, d.Day).AddHours(sa[0]).AddMinutes(sa[1]).AddSeconds(sa[2]),
                           End = new DateTime(d.Year, d.Month, d.Day).AddHours(ea[0]).AddMinutes(ea[1]).AddSeconds(ea[2])
                       };
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_timer.Enabled) Stop();

            if (CanConnectToMongo()) WriteToMongo();
            else WriteToFile();
            
        }

        private void WriteToMongo()
        {
            var d = DateTime.Now;
            var node = _linkedList.First;
            var allTime = new TimeSpan(0);
            var doc = new BsonDocument();
            var day = d.Year.ToPadString(4) + "-" + d.Month.ToPadString(2) + "-" + d.Day.ToPadString(2);
            var time = d.Hour.ToPadString(2) + ":" + d.Minute.ToPadString(2);
            doc["day"] = day;
            doc["time"] = time;
            doc["weekday"] = d.DayOfWeek.ToString();
            doc["ts"] = new BsonDateTime(d);
            var timeParts = new BsonArray();
            while (node != null)
            {
                var segment = node.Value;

                timeParts.Add(new BsonDocument
                    {
                        {"start", segment.Start.ToString("HH:mm:ss")},
                        {"end", segment.End.ToString("HH:mm:ss")},
                        {"diration", segment.Span.Hours + "h " + segment.Span.Minutes + "m " + segment.Span.Seconds+"s"},
                        {"minutes", segment.Span.TotalMinutes.Round(2)},
                        {"hours", segment.Span.TotalHours.Round(2)}
                    });

                allTime = allTime.Add(segment.Span);
                node = node.Next;
            }
            if (allTime.TotalSeconds < 1) return;
            doc["segments"] = timeParts;
            doc["duration"] = allTime.Hours + "h " + allTime.Minutes + "m " + allTime.Seconds+"s";
            doc["minutes"] = allTime.TotalMinutes.Round(2);
            doc["hours"] = allTime.TotalHours.Round(2);

            var col = GetMongoCollection("time_tracker");
            col.Update(Query.EQ("day", day), Update.Replace(doc), UpdateFlags.Upsert);
        }

        private void WriteToFile()
        {
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

        private bool CanConnectToMongo()
        {
            var col = GetMongoCollection("test");
            try
            {
                col.Update(Query.EQ("_id", "test"),
                           Update.Replace(new BsonDocument {{"_id", "test"}, {"time", new BsonDateTime(DateTime.Now)}}),
                           UpdateFlags.Upsert);
                return true;
            }
            catch (MongoConnectionException)
            {
                return false;
            }
        }

        private MongoCollection<BsonDocument> GetMongoCollection(string name)
        {
            const string cs = "mongodb://localhost/?safe=true";
            var mc = new MongoClient(cs);
            var server = mc.GetServer();
            var db = server.GetDatabase("youvisio");
            return db.GetCollection(name);
        }  

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
                {
                    var time = _prevSegment + (DateTime.Now - _linkedList.Last.Value.Start);

                    TaskbarItemInfo.ProgressValue = Math.Max(Math.Min(time.TotalHours/8.0, 1.0), 0.15);

                    LblTime.Content = time.Hours + "h " + time.Minutes + "m " + time.Seconds + "s";
                });
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
            SetPreviousTimesFromLinkedList();

            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
        }
        private void SetPreviousTimesFromLinkedList()
        {
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
