using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using NUnit.Framework;

namespace YouVisio.Wpf.TimeTracker.Tests
{
    [TestFixture]
    public class UpdateTimeForDay
    {
        [Test]
        public void UpdateTime()
        {
            // set here the day that is being updated
            var d = DateTime.Now.AddDays(-1);



            var day = d.ToYearMonthDay();

            var col = GetMongoCollection("time_tracker");
            var recordsForDay = col.FindOne(Query.EQ("day", day));
            var arr = recordsForDay?["segments"].AsBsonArray;
            var i = arr.Count;
            var timePassed = new TimeSpan(0);

            foreach (BsonDocument seg in arr)
            {
                var ts = GetTimeSegment(seg["start"].AsString, seg["end"].AsString);
                if (seg.Contains("task_id")) ts.Id = seg["task_id"].AsString;
                if (seg.Contains("task_comment")) ts.Comment = seg["task_comment"].AsString;
                timePassed = timePassed.Add(ts.Span);

            }

            recordsForDay["duration"] = timePassed.Hours + "h " + timePassed.Minutes + "m " + timePassed.Seconds + "s";
            recordsForDay["minutes"] = timePassed.TotalMinutes.Round(2);
            recordsForDay["hours"] = timePassed.TotalHours.Round(2);
            col.Update(Query.EQ("day", day), Update.Replace(recordsForDay), UpdateFlags.Upsert);

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
        private MongoCollection<BsonDocument> GetMongoCollection(string name)
        {
            const string cs = "mongodb://localhost/?safe=true";
            var mc = new MongoClient(cs);
            var server = mc.GetServer();
            var db = server.GetDatabase("youvisio");
            return db.GetCollection(name);
        }
    }
}
