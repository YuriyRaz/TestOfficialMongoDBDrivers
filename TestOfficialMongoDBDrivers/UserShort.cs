using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TestOfficialMongoDBDrivers
{
    public class UserShort
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
    }
}
