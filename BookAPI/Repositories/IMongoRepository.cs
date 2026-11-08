using Common.Models;

namespace BookAPI.Repositories
{
    public interface IMongoRepository<T> where T:MongoDocument
    {
        List<T> GetAllRecords();
        T GetRecordById(Guid id);
        T InsertRecord(T record);
        void UpsertRecord(T record);
        bool DeleteRecord(Guid id);
    }
}
