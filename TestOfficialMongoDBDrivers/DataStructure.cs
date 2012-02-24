using System;
using MongoDB.Bson;

namespace TestOfficialMongoDBDrivers
{

    public class DataStructure
    {
        public ObjectId Id { get; set; }
        public int NumberValue { get; set; }
        public string StringValue { get; set; }
        public DateTime DataTimeValue { get; set; }
        public byte[] BinaryData { get; set; }
    }
}
