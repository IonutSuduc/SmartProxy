using BookAPI.Settings;
using Common.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BookAPI.Repositories
{
    public class MongoRepository<T> : IMongoRepository<T> where T : MongoDocument
    {
        private readonly IMongoDatabase _db;
        private readonly IMongoCollection<T> _collection;
        public MongoRepository(IMongoDbSettings dbSettings)
        {
            var clientSettings = MongoClientSettings.FromConnectionString(dbSettings.ConnectionString);
            // Dacă Mongo e oprit, eșuăm în 2 secunde în loc de 30
            clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
            _db = new MongoClient(clientSettings).GetDatabase(dbSettings.DatabaseName);

            string tableName = typeof(T).Name.ToLower();
            _collection = _db.GetCollection<T>(tableName);
        }

        public bool DeleteRecord(Guid id)
        {
            var result = _collection.DeleteOne(doc => doc.Id == id);
            return result.DeletedCount > 0;
        }

        public List<T> GetAllRecords()
        {
            var records = _collection.Find(new BsonDocument()).ToList();

            return records;
        }

        public T GetRecordById(Guid id)
        {
            var record = _collection.Find(doc => doc.Id == id).FirstOrDefault();
            return record;
        }

        public T InsertRecord(T record)
        {
            _collection.InsertOne(record);
            return record;
        }

        public void UpsertRecord(T record)
        {
            _collection.ReplaceOne(doc => doc.Id == record.Id, record, 
                new ReplaceOptions { IsUpsert = true });
        }
    }
}
