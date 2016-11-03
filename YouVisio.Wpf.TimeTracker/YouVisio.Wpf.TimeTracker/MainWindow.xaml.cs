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

            EnsureTitle();
        }

        private void LoadPreviousLinkedList()
        {

            var col = GetMongoCollection("time_tracker");

            _prevSegment = new TimeSpan(0);

            var recordsFromToday = col.FindOne(Query.EQ("day", DateTime.Now.ToYearMonthDay()));
            if (recordsFromToday != null)
            {
                _linkedList.Clear();

                var i = 0;
                foreach(BsonDocument seg in recordsFromToday["segments"].AsBsonArray)
                {
                    var ts = GetTimeSegment(seg["start"].AsString, seg["end"].AsString);
                    if(seg.Contains("task_id")) ts.Id = seg["task_id"].AsString;
                    if(seg.Contains("task_comment")) ts.Comment = seg["task_comment"].AsString;
                    _prevSegment += ts.Span;
                    ts.Count = ++i;
                    _linkedList.AddLast(ts);
                }
            }

            SetTextViewFromLinkedList();

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
        }

        private bool EnsureSaveAndIfNeededLastDayClearList()
        {
            var end = _linkedList.Last.Value;
            var lastDayLastPeriodStartString = end.Start.ToYearMonthDay();
            var nowAsString = DateTime.Now.ToYearMonthDay();
            if (string.CompareOrdinal(lastDayLastPeriodStartString,nowAsString) < 0)
            {
                var lastDayLastPeriodEndString = end.End.ToYearMonthDay();

                var lastDayLastPeriodEndTime = new DateTime(end.End.Year, end.End.Month, end.End.Day, 0, 0, 0);

                if (lastDayLastPeriodStartString != lastDayLastPeriodEndString)
                {
                    var endOfYesterday = new DateTime(end.Start.Year, end.Start.Month, end.Start.Day, 23, 59, 59);
                    end.End = endOfYesterday;
                    RecordData(endOfYesterday, _linkedList);

                    _linkedList.Clear();
                    _linkedList.AddFirst(new TimeSegment
                    {
                        Start = lastDayLastPeriodEndTime,
                        End = DateTime.Now,
                        Count = 1,
                        Id = end.Id,
                        Comment =
                            (end.Comment + " (after splitting the time segment because it was crossing midnight)").Trim()
                    });
                    RecordData(DateTime.Now, _linkedList);
                }
                else
                {
                    RecordData(lastDayLastPeriodEndTime, _linkedList);
                    _linkedList.Clear();
                }
                
                return true;
            }
            RecordData(DateTime.Now, _linkedList);
            return false;
        }

        private void RecordData(DateTime dateSaved, LinkedList<TimeSegment> segments)
        {
            var d = dateSaved;
            var node = segments.First;
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
                        {"duration", segment.Span.Hours + "h " + segment.Span.Minutes + "m " + segment.Span.Seconds+"s"},
                        {"minutes", segment.Span.TotalMinutes.Round(2)},
                        {"hours", segment.Span.TotalHours.Round(2)},
                        {"task_id", segment.Id },
                        {"task_comment", segment.Comment }
                    });

                allTime = allTime.Add(segment.Span);
                node = node.Next;
            }
            if (allTime.TotalSeconds < 1) return;
            doc["segments"] = timeParts;
            doc["duration"] = allTime.Hours + "h " + allTime.Minutes + "m " + allTime.Seconds+"s";
            doc["minutes"] = allTime.TotalMinutes.Round(2);
            doc["hours"] = allTime.TotalHours.Round(2);

            if (CanConnectToMongo())
            {
                var col = GetMongoCollection("time_tracker");
                col.Update(Query.EQ("day", day), Update.Replace(doc), UpdateFlags.Upsert);
            }
            else
            {
                var path = Path.GetFullPath(Path.GetTempPath()+ "/__TimeTracker_UnsavedData_" + day + "_" + DateTime.UtcNow.Ticks + ".txt");
                File.WriteAllText(path, doc+"");

                MessageBox.Show("Cannot connect to Mongo. Data saved to "+path);
            }
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
            LoadPreviousLinkedList();
            _timer.Start();
            BtnPlay.Background = Brushes.Green;
            BtnPlay.Content = "Stop";
            _linkedList.AddLast(new TimeSegment
            {
                Start = DateTime.Now,
                Count = _linkedList.Count + 1,
                Id = TaskId.Text.Trim(),
                Comment = TaskComment.Text.Trim()
            });
            EnsureTitle();
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
        }

        private void Stop()
        {
            try
            {
                _timer.Stop();
                BtnPlay.Background = Brushes.DarkRed;
                BtnPlay.Content = "Play";
                _linkedList.Last.Value.End = DateTime.Now;
                
                EnsureSaveAndIfNeededLastDayClearList();
                SetTextViewFromLinkedList();
                EnsureTitle();
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void EnsureTitle()
        {
            var d = DateTime.Now;
            var day = d.Year.ToPadString(4) + "-" + d.Month.ToPadString(2) + "-" + d.Day.ToPadString(2) + " "+d.DayOfWeek;
            Title = "Time Tricker @ YouVisio (" + day + ")";
        }

        private void SetTextViewFromLinkedList()
        {
            _prevSegment = new TimeSpan(0);
            TxtLog.Text = "";
            var node = _linkedList.Last;
            var i = _linkedList.Count;
            while (node != null)
            {
                var segment = node.Value;
                _prevSegment = _prevSegment.Add(segment.Span);
                TxtLog.Text += (i--).ToString().PadLeft(3,' ')+".) "+segment+"\n";
                node = node.Previous;
            }

            var col = GetMongoCollection("time_tracker");
            var recordsFromYesterday = col.FindOne(Query.EQ("day", DateTime.Now.AddDays(-1).ToYearMonthDay()));
            var arr = recordsFromYesterday?["segments"].AsBsonArray;
            if(arr == null) return;
            const int showRecords = 5;
            var j = arr.Count;
            foreach (BsonDocument seg in 
                arr
                .Skip(Math.Max(arr.Count-showRecords,0))
                .Take(showRecords)
                .Reverse()
                .OfType<BsonDocument>())
            {
                var ts = GetTimeSegment(seg["start"].AsString, seg["end"].AsString);
                if(seg.Contains("task_id")) ts.Id = seg["task_id"].AsString;
                if(seg.Contains("task_comment")) ts.Comment = seg["task_comment"].AsString;
                ts.Count = ++i;
                TxtLog.Text += "yesterday  ("+DateTime.Now.AddDays(-1).ToYearMonthDay()+"):" + (j--).ToString().PadLeft(3,' ')+".) "+ts + "\n";
            }
        }

        private void ClearButton_OnClick(object sender, RoutedEventArgs e)
        {
            TaskId.Text = TaskComment.Text = "";
        }
    }

    public class TimeSegment
    {
        public int Count { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public TimeSpan Span => End - Start;
        public string Id { get; set; }
        public string Comment { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb
                .Append(Start.ToString("HH:mm:ss"))
                .Append(" - ")
                .Append(End.ToString("HH:mm:ss"))
                .Append(" (")
                .Append(Span.Hours).Append("h ")
                .Append(Span.Minutes).Append("m ")
                .Append(Span.Seconds).Append("s")
                .Append(")");
            if (!string.IsNullOrWhiteSpace(Id)) sb.Append("       #" + Id);
            if (!string.IsNullOrWhiteSpace(Comment))
            {
                sb.Append("       ")
                  .Append(((Comment.Length > 50)?Comment.Substring(0,50)+"...":Comment).Replace("\n"," ").Replace("\r"," ").Replace("\t"," "));
            }
            return sb.ToString();
        }
    }
}
