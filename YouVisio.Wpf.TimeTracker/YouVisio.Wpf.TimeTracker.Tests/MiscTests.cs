using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using NUnit.Framework;

namespace YouVisio.Wpf.TimeTracker.Tests
{
    [TestFixture]
    public class MiscTests
    {
        [Test]
        public void CanConnectToMongo()
        {
            const string cs = "mongodb://localhost/?safe=true";
            var mc = new MongoClient(cs);
            var server = mc.GetServer();
            var db = server.GetDatabase("test");
            var col = db.GetCollection("test");
            try
            {
                col.Insert(new BsonDocument { { "hey", 1 } });
            }
            catch (MongoConnectionException)
            {
                Console.WriteLine("CANNOT CONNECT");
                throw;
            }
        }


        [Test]
        public void CanFormatTimeSegment()
        {
            var segment = new TimeSegment
            {
                Start = new DateTime(2016,8,30,23,44,08),
                End = new DateTime(2016,8,30,23,59,59)
            };
            var json = new BsonDocument
            {
                {"start", segment.Start.ToString("HH:mm:ss")},
                {"end", segment.End.ToString("HH:mm:ss")},
                {"diration", segment.Span.Hours + "h " + segment.Span.Minutes + "m " + segment.Span.Seconds + "s"},
                {"minutes", segment.Span.TotalMinutes.Round(2)},
                {"hours", segment.Span.TotalHours.Round(2)},
                {"task_id", segment.Id},
                {"task_comment", segment.Comment}
            };
            Console.WriteLine(json);
        }

        [Test]
        public void Misc1()
        {
            Console.WriteLine(string.CompareOrdinal("2016-10-30","2016-10-31"));
        }
    }
}