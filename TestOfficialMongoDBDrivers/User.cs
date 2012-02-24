using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TestOfficialMongoDBDrivers
{
    public class User
    {
        private ICollection<MovieShortDetail> _favoriteMovies;
        
        public ObjectId Id { get; set; }
        public string Name { get; set; }

        [BsonRepresentation( BsonType.String )]
        public Gender Gender { get; set; }
        public int Age { get; set; }

        [BsonIgnoreIfNull]
        public ICollection<MovieShortDetail> Favorites
        {
            get { return _favoriteMovies ?? (_favoriteMovies = new HashSet<MovieShortDetail>()); }
            set { _favoriteMovies = new HashSet<MovieShortDetail>( value ?? new MovieShortDetail[] { } ); }
        }

        /// <summary>
        /// Define that MovieShortDetails collections 
        /// should be serialized only if it not empty.
        /// </summary>
        public bool ShouldSerializeMovieShortDetails()
        {
            return Favorites.Count > 0;
        } 
    }
}
