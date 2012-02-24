using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TestOfficialMongoDBDrivers
{
    [BsonDiscriminator( RootClass = true )]
    [BsonKnownTypes( typeof( Cat ), typeof( Dog ) )]
    public class Animal
    {
        public ObjectId Id { get; set; }
        public int Number { get; set; }

        public Animal() {}
        public Animal( int number ) { Number = number; }
    }

    [BsonKnownTypes( typeof( Lion ), typeof( Tiger ) )]
    public class Cat : Animal
    {
        public Cat() {}
        public Cat( int number ) : base( number ) { }
    }

    public class Dog : Animal
    {
        public Dog() {}
        public Dog( int number ) : base( number ) { }
    }

    public class Lion : Cat
    {
        public Lion() {}
        public Lion( int number ) : base( number ) { }
    }

    public class Tiger : Cat
    {
        public Tiger() {}
        public Tiger( int number ) : base( number ) { }
    }
}
