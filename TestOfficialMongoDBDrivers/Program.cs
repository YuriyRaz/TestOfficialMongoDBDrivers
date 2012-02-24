using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using JsonPrettyPrinterPlus;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using FluentMongo.Linq;
using TestOfficialMongoDBDrivers.Helpers;

namespace TestOfficialMongoDBDrivers
{
    internal class Program
    {
        private static readonly string[] MenuItems =
            {
                "1. Reinitialize collection (Remove all data, Generate random data, Ensure indices).",
                "2. Select data demo (8 steps)",
                "3. Modify data demo (4 steps)",
                "4. Aggregation & MapReduce demo (5 steps)",
                "5. Delete data demo (2 steps)",
                "6. Exit"
            };

        /// <summary>
        /// The Main method
        /// </summary>
        static void Main()
        {
            QueryDemo();
            //PolymorphicDemo();
            //DictionaryDemo();
            //SerializationDemo();
        }

        /// <summary>
        /// Queries the demo.
        /// </summary>
        static void QueryDemo()
        {
            // Connect to database
            const string connectionString = "mongodb://localhost/TestDB";
            MongoServer server = MongoServer.Create( connectionString );
            // Get database
            MongoDatabase db = server.GetDatabase( "TestDB" );
            // Get users collection
            var userCollection = db.GetCollection<User>( "Users" );

            MenuItem selectedMenuItem;
            do
            {
                selectedMenuItem = ShowMenu();
                switch (selectedMenuItem)
                {
                    case MenuItem.ReinitializeCollection:
                        ReinitializeCollection( userCollection );
                        break;

                    case MenuItem.SelectDemo:
                        SelectDemo( userCollection );
                        break;

                    case MenuItem.ModifyDemo:
                        ModifyDemo( userCollection );
                        break;

                    case MenuItem.DeleteDemo:
                        DeleteDemo( userCollection );
                        break;

                    case MenuItem.AggregationDemo:
                        AggregationDemo( userCollection );
                        break;
                }
            } while (selectedMenuItem != MenuItem.Exit);
        }

        /// <summary>
        /// Deletes the demo.
        /// </summary>
        /// <param name="userCollection">The user collection.</param>
        private static void DeleteDemo( MongoCollection<User> userCollection )
        {
            //====================== Delete demo ==================
            Console.WriteLine();
            Console.Write( "Delete demo." );
            Console.WriteLine();
            Console.WriteLine( "Users count = " + userCollection.Count() );
            Console.WriteLine();
            Console.ReadLine();

            Console.WriteLine( "1) Remove older then 50 years" );

            var selectQuery = Query.GT( "Age", 50 );
            userCollection.Remove( selectQuery );

            Console.WriteLine( "Done." );

            Console.WriteLine();
            Console.WriteLine( "Users count = " + userCollection.Count() );
            Console.WriteLine();
            Console.ReadLine();

            Console.WriteLine( "2) Remove all other users..." );

            userCollection.RemoveAll();

            Console.WriteLine( "Done." );

            Console.WriteLine();
            Console.WriteLine( "Users count = " + userCollection.Count() );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "Show all records from Users:" );

            var allUsers = userCollection.FindAll();

            // Output all users
            ShowItems<User>( allUsers );

            Console.WriteLine();
            Console.WriteLine( "--- END ---" );
            Console.ReadLine();

            //========================= END =========================================
        }

        /// <summary>
        /// Aggregations the demo.
        /// </summary>
        /// <remarks>
        /// On-line documentation:
        /// http://www.mongodb.org/display/DOCS/Aggregation,
        /// http://www.mongodb.org/display/DOCS/MapReduce
        /// </remarks>
        /// <param name="userCollection">The user collection.</param>
        private static void AggregationDemo( MongoCollection<User> userCollection )
        {
            IMongoQuery selectQuery;
            BsonJavaScript map;
            BsonJavaScript reduce;
            MapReduceOptionsBuilder options;
            MapReduceResult mapReduceResult;

            //====================== 1) Count ============================
            Console.WriteLine();
            Console.WriteLine( "1) Count" );
            Console.WriteLine();
            // > db.Users.count()
            Console.Write( "Count of all Users:" + userCollection.Count() );

            Console.ReadLine();
            Console.WriteLine();

            // > db.Users.count( { Gender: "Female" } )
            var selectFemale = Query.EQ( "Gender", Gender.Female.ToString() );
            Console.Write( "Count of females:" + userCollection.Count( selectFemale ) );

            Console.ReadLine();

            //====================== 2) Distinct ============================

            Console.WriteLine();
            Console.WriteLine( "2) Distinct " );
            Console.WriteLine();

            // > db.Users.distinct( "Gender" )
            Console.Write( "Existing Gender values:" + userCollection.Distinct( "Gender" ) );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "Movies at users between 20 and 25:" );

            // > db.Users.distinct( "Favorites.Name", { Age : { $gte : 20, $lte : 25 } } )
            selectQuery = Query.GTE( "Age", 20 ).LTE( 25 );
            var movies = userCollection.Distinct( "Favorites.Name", selectQuery );

            Console.WriteLine( "Select query:" + selectQuery.ToJson() );
            Console.WriteLine( movies );
            Console.ReadLine();

            //====================== 3) Group ============================

            Console.WriteLine();
            Console.WriteLine( "3) Group" );
            Console.WriteLine( "Note: currently one must use map/reduce " +
                              "instead of group() in sharded MongoDB configurations." );
            Console.WriteLine();

            /* > db.Users.group( { 
             *                      key : "Age", 
             *                      cond : { Age : { $gte : 20, $lte : 25 } },
             *                      reduce : function(obj, group) { group.Count++; },
             *                      initial : { Count = 0 }
             *                   } )
             */

            selectQuery = Query.GTE( "Age", 20 ).LTE( 30 );

            var groupResult = userCollection.Group(
                selectQuery,
                GroupBy.Keys( "Age" ),
                new { Count = 0 }.ToBsonDocument(),
                new BsonJavaScript( "function(obj, group) { group.Count++; }" ),
                null );

            Console.WriteLine( "Group by age:" );
            Console.WriteLine( "Select query:" + selectQuery.ToJson() );
            Console.WriteLine( "Result:" );
            foreach (var item in groupResult)
            {
                Console.WriteLine( item );
            }
            Console.ReadLine();

            //====================== 4) [USING DRIVER] Map / Reduce ============================

            Console.WriteLine();
            Console.WriteLine( "4) [USING DRIVER] Map / Reduce" );
            Console.WriteLine();
            Console.WriteLine( "Count users with some age for users that younger then 20 and older then 60 years:" );

            // Define map function
            const string mapJS2 = "function() {                                             " +
                                  "    emit(                                                " +
                                  "             /* group_by := */ this.Age-(this.Age % 10), " +
                                  "             /* data_for_reduce := */ { count : 1 }      " +
                                  "         );                                              " +
                                  "};                                                       ";

            map = BsonJavaScript.Create( mapJS2 );
            // Define reduce function
            const string reduceJS2 = "function( age , arrayOfCounts )                       " +
                                    "{                                                      " +
                                    "   var total = 0;                                      " +
                                    "   for ( var i = 0; i < arrayOfCounts.length; i++ )    " +
                                    "   {                                                   " +
                                    "       total += arrayOfCounts[i].count;                " +
                                    "   }                                                   " +
                                    "   return { count : total };                           " +
                                    "};                                                     ";

            reduce = BsonJavaScript.Create( reduceJS2 );

            // Force calculation in memory but not put result into table
            options = new MapReduceOptionsBuilder();
            options.SetOutput( MapReduceOutput.Inline );

            Console.WriteLine();

            // Define selector query
            selectQuery = Query.GTE( "Age", 20 ).LT( 60 );

            // Execute map/reduce
            mapReduceResult = userCollection.MapReduce( selectQuery, map, reduce, options );

            Console.WriteLine( "Result:" );
            foreach (var item in mapReduceResult.InlineResults)
            {
                Console.WriteLine( item.ToString().Replace( ",", ",\t" ) );
            }
            Console.ReadLine();
            //====================== 5) [LINQ] Map / Reduce ============================

            Console.WriteLine();
            Console.WriteLine( "5) [LINQ] Map / Reduce" );
            Console.WriteLine();
            Console.WriteLine( "Count users with some age for users that younger then 20 and older then 60 years:" );

            // NOTE FluentMongo don't support SelectMany

            var resultLinq = from user in userCollection.AsQueryable()
                             where user.Age >= 20 && user.Age < 60
                             group user by (user.Age - (user.Age % 10)) into g
                             select new { Age = g.Key, Count = g.Count() };

            Console.WriteLine();

            string query = resultLinq.GetMongoQueryObject();
            Console.WriteLine( "Query:" );
            Console.WriteLine( query.PrettyPrintJson() );
            Console.ReadLine();

            Console.WriteLine();
            Console.WriteLine( "Result:" );
            foreach (var item in resultLinq)
            {
                Console.WriteLine( item.ToJson().Replace( ",", ",\t" ) );
            }

            Console.ReadLine();
            //====================== 5) Map / Reduce ============================

            Console.WriteLine();
            Console.WriteLine( "5) Map / Reduce" );
            Console.WriteLine();
            Console.WriteLine( "Count films in user favorites:" );

            // Define map function
            const string mapJS = "function() {                                                  " +
                                 "    this.Favorites.forEach (  " +   // this is each User document in collection
                                 "        function(movieShortDetail) {                          " +
                                 "            emit(                                             " +
                                 "                  /* group_by := */ movieShortDetail.Name ,   " +
                                 "                  /* data_for_reduce := */ { count : 1 }      " +
                                 "                 );                                           " +
                                 "        }                                                     " +
                                 "    );                                                        " +
                                 "};                                                            ";

            map = BsonJavaScript.Create( mapJS );

            // Define reduce function
            const string reduceJS = "function( movieName , arrayOfCounts )                  " +
                                    "{                                                      " +
                                    "   var total = 0;                                      " +
                                    "   for ( var i = 0; i < arrayOfCounts.length; i++ )    " +
                                    "   {                                                   " +
                                    "       total += arrayOfCounts[i].count;                " +
                                    "   }                                                   " +
                                    "   return { count : total };                           " +
                                    "};                                                     ";

            reduce = BsonJavaScript.Create( reduceJS );

            // Force calculation in memory but not put result into table
            options = new MapReduceOptionsBuilder();
            options.SetOutput( MapReduceOutput.Inline );

            Console.WriteLine();

            // Execute map/reduce
            mapReduceResult = userCollection.MapReduce( map, reduce, options );

            Console.WriteLine( "Result:" );
            foreach (var item in mapReduceResult.InlineResults)
            {
                Console.WriteLine( item.ToString().Replace( ",", ",\t" ) );
            }

            Console.WriteLine();
            Console.WriteLine( "--- END ---" );
            Console.ReadLine();
            //========================= END =========================================
        }

        /// <summary>
        /// Modifies the demo.
        /// </summary>
        /// <param name="userCollection">The user collection.</param>
        private static void ModifyDemo( MongoCollection<User> userCollection )
        {
            MongoCursor<User> users;
            IMongoQuery selectQuery;
            IMongoUpdate updateQuery;

            //====================== 1) Save demo ==================
            Console.WriteLine();
            Console.WriteLine( "1) Save demo." );

            // Generate same id for movie
            User user = new User
                            {
                                Name = "Vasiliy",
                                Age = 25,
                                Gender = Gender.Male,
                                Favorites =
                                    {
                                        new MovieShortDetail
                                            {
                                                Name = "Matrix"
                                            },
                                        new MovieShortDetail
                                            {
                                                Name = "Hulk"
                                            }

                                    }
                            };

            Console.WriteLine();
            Console.WriteLine( "Find all Vasilies:" );

            var selectVasiliy = Query.EQ( "Name", "Vasiliy" );

            Console.WriteLine( "Select query:" + selectVasiliy.ToJson() );

            // Find all user with name "Vasiliy"
            // > db.Users.find( Name : "Vasiliy" )
            users = userCollection.Find( selectVasiliy );

            ShowItems<User>( users, true );

            Console.WriteLine();
            Console.WriteLine( "Created user:\n" + user.ToJson().PrettyPrintJson() );

            Console.WriteLine();
            Console.Write( "Save user..." );

            // Save user
            // > db.Users.save( { Name: "Vasiliy", Age: 25, Gender: "Male", Favorites: [ { Name: "Matrix" }, { Name: "Hulk" } ] } )
            userCollection.Save( user );
            Console.WriteLine( "Done." );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "Find all Vasilies:" );
            Console.WriteLine( "Select query:" + selectVasiliy.ToJson() );

            //// Find all user with name "Vasiliy"
            //users = userCollection.Find(
            //    Query.EQ( "Name", "Vasiliy" ) );

            ShowItems<User>( users, true );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "Change Age to 35 and Save..." );

            // Change age and save user
            user.Age = 35;
            // > db.Users.save( { Name: "Vasiliy", Age: 35, Gender: "Male", Favorites: [ { Name: "Matrix" }, { Name: "Hulk" } ] } )
            userCollection.Save( user );

            Console.WriteLine( "Done." );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "Find all Vasilies:" );
            Console.WriteLine( "Select query:" + selectVasiliy.ToJson() );
            // Retry all user with name "Vasiliy"
            ShowItems<User>( users, true );
            Console.ReadLine();

            //====================== 2) Find And Modify ============================

            // NOTE: To prevent ABA conflict:
            //      1) Use the entire object in the update's query expression
            //      2) Put a version variable in the object, and increment it on each update.
            // See also: http://www.mongodb.org/display/DOCS/Atomic+Operations

            Console.WriteLine();
            Console.WriteLine( "2) Find And Modify" );
            Console.WriteLine();
            Console.WriteLine( "Find Vasiliy user and strike out Matrix for they favorites:" );

            // Define select query
            selectQuery = new CommandDocument( user.ToBsonDocument() );

            // Define update query
            updateQuery = Update.Pull(
                "Favorites",
                (new MovieShortDetail { Name = "Matrix" })
                .ToBsonDocument() );

            Console.WriteLine( "Select query:" + selectQuery.ToJson().PrettyPrintJson() );
            Console.WriteLine( "Update query:" + updateQuery.ToJson().PrettyPrintJson() );

            
            // > db.Users.findAndModify( { "_id" : ObjectId("XXX"), Name: "Vasiliy", Age: 35, Gender: "Male", Favorites: [ { Name: "Matrix" }, { Name: "Hulk" } ] }, { "$pull": { "Favorites": { "Name": "Matrix" } } } )
            FindAndModifyResult result = userCollection
                .FindAndModify( selectQuery, SortBy.Null, updateQuery, true );
            
            Console.WriteLine();

            if (result.Ok)
            {
                Console.WriteLine( "Done." );
                Console.WriteLine();

                Console.ReadLine();
                Console.WriteLine( "Result document:" );
                Console.WriteLine( result.ModifiedDocument.ToJson().PrettyPrintJson() );
            }
            else
            {
                Console.WriteLine( "Error: " + result.ErrorMessage );
            }

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "Find all Vasilies:" );
            Console.WriteLine( "Select query:" + selectVasiliy.ToJson() );
            // Find all user with name "Vasiliy"
            users = userCollection.Find( selectVasiliy );
            ShowItems<User>( users, true );
            Console.ReadLine();

            //====================== 3) Update document atomically ==================
            Console.WriteLine();
            Console.WriteLine( "3) Update document atomically" );
            Console.WriteLine();

            selectQuery = Query.EQ( "Name", user.Name );
            updateQuery = Update
                .Inc( "Age", 10 )
                .AddToSetEach(
                    "Favorites",
                    new BsonArray( new[]
                                      {
                                          (new MovieShortDetail {Name = "Hulk"})
                                              .ToBsonDocument(),
                                          (new MovieShortDetail {Name = "Cinderella"})
                                              .ToBsonDocument()
                                      } ) );

            Console.WriteLine( "Execute Update (Apply to Vasiliy: increase Age on 10 and add two movies to Favorites)... " );
            Console.WriteLine( "Select query:" + selectQuery.ToJson() );
            Console.WriteLine( "Update query:" + updateQuery.ToJson().PrettyPrintJson() );

            // Execute atomically update
            //      NOTE: Will be updated only _first_ Vasiliy, not all

            // > db.Users.update( { "Name" : "Vasiliy" }, { "$inc": { "Age": 10 }, "$addToSet": { "Favorites": { "$each": [{ "Name": "Hulk" }, { "Name": "Cinderella" }] } } } )
            userCollection.Update( selectQuery, updateQuery );

            Console.WriteLine( "Done." );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "Find all Vasilies:" );
            Console.WriteLine( "Select query:" + selectVasiliy.ToJson() );
            // Find all user with name "Vasiliy"
            users = userCollection.Find( selectVasiliy );
            ShowItems<User>( users, true );
            Console.ReadLine();


            //====================== 4) Multiply Update ============================
            Console.WriteLine();
            Console.WriteLine( "4) Multiply Update " );
            Console.WriteLine();
            Console.WriteLine( "Find top 10 males, sorted by name:" );

            // Define select query
            selectQuery = Query.EQ( "Gender", "Male" );

            Console.WriteLine( "Select query:" + selectQuery.ToJson() );

            // Select top 10 males
            users = userCollection.Find( selectQuery )
                .SetSortOrder( SortBy.Ascending( "Name" ) )
                .SetLimit( 10 );

            ShowItems<User>( users );
            Console.ReadLine();

            Console.WriteLine();

            // Define update query
            updateQuery = Update.Inc( "Age", -100 );

            Console.WriteLine( "Decrease age for all male on 100:" );
            Console.WriteLine( "Select query:" + selectQuery.ToJson() );
            Console.WriteLine( "Update query:" + updateQuery.ToJson() );
            Console.WriteLine( "Flag :" + UpdateFlags.Multi );

            // Execute multiply update
            userCollection.Update(
                selectQuery,
                updateQuery,
                UpdateFlags.Multi );

            Console.WriteLine( "Done." );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "Find top 10 male, sorted by name:" );
            Console.WriteLine( "Select query:" + selectQuery.ToJson() );
            ShowItems<User>( users );

            Console.WriteLine( "--- END ---" );
            Console.ReadLine();
            //========================= END =========================================
        }

        /// <summary>
        /// Selects the demo.
        /// </summary>
        /// <param name="userCollection">The user collection.</param>
        private static void SelectDemo( MongoCollection<User> userCollection )
        {
            string mongoQuery;

            //====================== Get all documents ==================

            // > db.Users.find()
            var allUsers = userCollection.FindAll();

            // Output all users
            Console.WriteLine();
            Console.WriteLine( "[QUERY OBJECT] All items from collection User after insert:" );
            Console.ReadLine();
            ShowItems<User>( allUsers );

            //=============== 1) Select users using query object ===============
            var selectQuery = Query.And(
                Query.Matches( "Name", new Regex( "^User" ) ),
                Query.GTE( "Age", 10 ).LTE( 20 ),
                Query.EQ( "Gender", "Female" )
                );

            var sortOrder = SortBy.Ascending( "Gender" ).Descending( "Age" );

            // > db.Users.find( { Age: { "$gte": 10, "$lte": 20 }, Gender: "Female" } ).sort( { Gender : 1, Age : -1 } )
            var userSet1 = userCollection
                .Find( selectQuery )
                .SetSortOrder( sortOrder );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "1) [QUERY OBJECT] Query for users that 10 <= Age <= 20 and is Female:" );
            Console.WriteLine( "Select query: " );
            Console.WriteLine( selectQuery.ToJson().PrettyPrintJson() );
            Console.WriteLine( "Sort query: " );
            Console.WriteLine( sortOrder.ToJson().PrettyPrintJson() );
            Console.ReadLine();
            ShowItems<User>( userSet1 );

            //================ 2) Use FluentMongo Linq extension ===============

            // > db.Users.find( { Age: { "$gte": 10, "$lte": 20 } } ).sort( { Gender : 1, Age : -1 } )
            var userSet2 = userCollection.AsQueryable()
                .Where( u => u.Name.StartsWith("User") && u.Age >= 10 && u.Age <= 20 )
                .OrderBy( u => u.Gender )
                .ThenByDescending( u => u.Age );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "2) [LINQ] Users that 10 <= Age <= 20:" );
            mongoQuery = userSet2.GetMongoQueryObject();
            Console.WriteLine( "Query generated by Linq expression:" );
            Console.WriteLine( mongoQuery.PrettyPrintJson() );
            Console.ReadLine();
            ShowItems<User>( userSet2 );

            //================== 3) Bug in FluentMongo ============================
            // FluentMongo not recognize BsonRepresentation attribute for Gender property and 
            // generate query with integer values of Female constant
            // > db.Users.find( { Age: { "$gte": 10, "$lte": 20 }, Gender : 1 } )
            var wrongLinqQuery = userCollection.AsQueryable()
                .Where( u => u.Age >= 10 && u.Age <= 20 && u.Gender == Gender.Female );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "3) [LINQ] Wrong Linq query. In generated query used integer value of constant Gender.Female" );
            Console.WriteLine( "Query generated by Linq expression:" );
            Console.WriteLine( wrongLinqQuery.GetMongoQuery().ToJson().PrettyPrintJson() );
            Console.ReadLine();
            ShowItems<User>( wrongLinqQuery );

            //=================== 4) Modification of MongoCursor ======================

            // > db.Users.find().sort( { Name : 1 } ).skip(20).limit(10)
            MongoCursor<User> userSet4 = userCollection.FindAll()
                .SetSortOrder( SortBy.Ascending( "Name" ) )
                .SetLimit( 10 )
                .SetSkip( 20 );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "4) [QUERY OBJECT] Limit 10 and Skip 20:" );
            Console.WriteLine( "MongoCursor.Limit = " + userSet4.Limit );
            Console.WriteLine( "MongoCursor.Skip = " + userSet4.Skip );
            Console.ReadLine();
            ShowItems<User>( userSet4 );

            //=================== 5) Using Linq Take() and Skip() extensions ===========

            // ATTENTION: Don't mix up order of Skip() & Limit() in Linq queries.

            // > db.Users.find().sort( { Name : 1 } ).skip(20).limit(10)
            var userSet5 = userCollection.AsQueryable()
                .OrderBy( u => u.Name )
                .Skip( 20 )
                .Take( 10 );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "5) [LINQ] Limit 10 and Skip 20:" );
            mongoQuery = userSet5.GetMongoQueryObject().PrettyPrintJson();
            Console.WriteLine( "Query generated by Linq expression:" );
            Console.WriteLine( mongoQuery );
            Console.ReadLine();
            ShowItems<User>( userSet5 );

            //=================== 6) [QUERY OBJECT] Get document partially ======================

            // > db.Users.find( {}, { Name : 1 } ).sort( { Name : 1 } ).skip(20).limit(10)
            var userSet6 = userCollection.FindAllAs<UserShort>()
                .SetSortOrder( SortBy.Ascending( "Name" ) )
                .SetLimit( 10 )
                .SetSkip( 20 )
                .SetFields( Fields.Include( "Name" ) );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "6) [QUERY OBJECT] Get document partially (include _id, Name):" );
            Console.WriteLine( "MongoCursor.Fields = " + userSet6.Fields.ToJson().PrettyPrintJson() );
            Console.ReadLine();
            ShowItems<UserShort>( userSet6 );

            //=================== 7) [LINQ] Get document partially ======================

            // > db.Users.find( {}, { Name : 1, _id : 0 } ).sort( { Name : 1 } ).skip(20).limit(10)
            var userSet7 = userCollection.AsQueryable()
                .OrderBy( u => u.Name )
                .Skip( 20 )
                .Take( 10 )
                .Select( u => new UserShort { Name = u.Name } );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "7) [QUERY OBJECT] Get document partially (exclude _id, include Name):" );
            mongoQuery = userSet7.GetMongoQueryObject().PrettyPrintJson();
            Console.WriteLine( "Query generated by Linq expression:" );
            Console.WriteLine( mongoQuery );
            Console.ReadLine();
            ShowItems<UserShort>( userSet7 );

            Console.ReadLine();
            //=================== 8) $where with JavaScript ======================

            // > db.Users.find( { $where : "this.Age % 10 <= 1" } )
            selectQuery = Query.Where( new BsonJavaScript( "this.Age % 10 <= 1" ) );
            var userSet8 = userCollection.Find( selectQuery );

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine( "8) [QUERY OBJECT] Where Age % 10 <= 1:" );
            Console.WriteLine( "Select query" + selectQuery.ToJson() );
            Console.ReadLine();
            ShowItems<User>( userSet8 );

            Console.WriteLine();
            Console.WriteLine( "--- END ---" );
            Console.ReadLine();
            //=========================== END ===========================================

        }

        /// <summary>
        /// Reinitializes the collection.
        /// </summary>
        private static void ReinitializeCollection( MongoCollection<User> userCollection )
        {
            Console.Write( "Create indices... " );

            // Create indices
            IndexKeysBuilder indexBuilder = new IndexKeysBuilder();
            userCollection.EnsureIndex(
                indexBuilder
                    .Ascending( "Name" )
                    .Descending( "Age" ) );

            Console.WriteLine( "Done." );
            Console.WriteLine();
            Console.Write( "Remove all data form collection Users... " );

            // Remove all documents
            userCollection.RemoveAll();

            Console.WriteLine( "Done." );
            Console.WriteLine();
            Console.Write( "Generate random data... " );

            var movies = new[]
                             {
                                 new MovieShortDetail {Name = "A"},
                                 new MovieShortDetail {Name = "B"},
                                 new MovieShortDetail {Name = "C"},
                                 new MovieShortDetail {Name = "D"}
                             };

            // Fill users randomly
            Random random = new Random();
            for (int i = 0; i < 100; i++)
            {
                User user = new User
                {
                    Name = string.Format( "User{0}", i + 1 ),
                    Age = random.Next( 10, 91 ),
                    Gender = random.Next( 2 ) == 0 ? Gender.Male : Gender.Female,
                };

                if (i % 3 == 0)
                {
                    user.Favorites.Add(
                        movies[
                            random.Next( movies.Length )] );
                }

                userCollection.Insert( user );
            }

            Console.WriteLine( "Done." );
            Console.WriteLine();
            Console.ReadLine();
        }

        /// <summary>
        /// Shows the menu.
        /// </summary>
        static MenuItem ShowMenu()
        {
            int itemNumber;
            do
            {
                Console.Clear();
                foreach (var menuItem in MenuItems)
                {
                    Console.WriteLine( menuItem );
                }
                string item = Console.ReadLine();

                if (!int.TryParse( item, out itemNumber ))
                {
                    continue;
                }
            } while (!(itemNumber > 0 && itemNumber <= (int)MenuItem.Exit));

            return (MenuItem)itemNumber;
        }

        /// <summary>
        /// Shows the items.
        /// </summary>
        /// <param name="cursor">The cursor.</param>
        /// <param name="format">Multi string Json format</param>
        static void ShowItems<T>( IEnumerable cursor, bool format = false )
        {
            bool isEmpty = true;
            foreach (T document in cursor)
            {
                var json = document.ToJson();
                if (format) json = json.PrettyPrintJson();
                Console.WriteLine( json );
                isEmpty = false;
            }

            if (isEmpty)
            {
                Console.WriteLine( ">> Result is empty." );
            }
        }

        /// <summary>
        /// Serializations the demo.
        /// </summary>
        static void SerializationDemo()
        {
            var data = new DataStructure
            {
                Id = ObjectId.GenerateNewId(),
                NumberValue = 123,
                StringValue = "some string",
                DataTimeValue = DateTime.Now,
                BinaryData = new byte[] { 125, 0, 255 }
            };

            byte[] bson = data.ToBson();
            string json = data.ToJson();
            BsonDocument bsonDocument = data.ToBsonDocument();

            DataStructure data2 = BsonSerializer.Deserialize<DataStructure>( bson );

            BsonDocument bsonDocument2 = BsonSerializer.Deserialize<BsonDocument>( bson );

            Debug.Assert( !ReferenceEquals( bsonDocument, bsonDocument2 ) );
            Debug.Assert( bsonDocument.Equals( bsonDocument2 ) );
            Debug.Assert( bsonDocument == bsonDocument2 );

            //Debug.Assert( !ReferenceEquals( data, data2 ) );
            //Debug.Assert( data.Equals( data2 ) );
        }

        /// <summary>
        /// Dictionaries the demo.
        /// </summary>
        static void DictionaryDemo()
        {
            DictionaryDate data = new DictionaryDate
            {
                Document = new Dictionary<string, int> { { "First", 10 }, { "Second", 20 } },
                ArrayOfArrays = new Dictionary<int, int> { { 1, 10 }, { 2, 20 } },
                ArrayOfDocuments = new Dictionary<int, int> { { 3, 30 }, { 4, 40 } }
            };

            string json = data.ToJson();
        }

        /// <summary>
        /// Polymorphics the demo.
        /// </summary>
        static void PolymorphicDemo()
        {
            //BsonClassMap.RegisterClassMap<Animal>();

            MongoServer server = MongoServer.Create( "mongodb://localhost" );
            var animalsCollection = server
                .GetDatabase( "TestDB" )
                .GetCollection<Animal>( "Animals" );

            animalsCollection.RemoveAll();
            animalsCollection.InsertBatch( new[]
                                              {
                                                  new Animal(1) , new Cat(2), new Dog(3), new Lion(4), new Tiger(5), 
                                              } );

            var result = animalsCollection.FindAll();
            foreach (var animal in result)
            {
                Console.WriteLine( string.Format( "{0} - Type: {1}", animal.ToJson(), animal.GetType().Name ) );
            }
        }

    }
}
