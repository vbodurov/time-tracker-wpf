using System;
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
    }
}