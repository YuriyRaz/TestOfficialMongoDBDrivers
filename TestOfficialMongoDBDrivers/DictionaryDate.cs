using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace TestOfficialMongoDBDrivers
{
    class DictionaryDate
    {
        [BsonDictionaryOptions( DictionaryRepresentation.Document )]
        public IDictionary<string, int> Document { get; set; }
        [BsonDictionaryOptions( DictionaryRepresentation.ArrayOfArrays )]
        public IDictionary<int, int> ArrayOfArrays { get; set; }
        [BsonDictionaryOptions( DictionaryRepresentation.ArrayOfDocuments )]
        public IDictionary<int, int> ArrayOfDocuments { get; set; }
    }
}
