namespace Common.Models
{
    public class Book : MongoDocument
    {
        public string Title { get; set;  }
        public string Author { get; set; }
        public int? PublishedYear{ get; set; }
        public int? Pages { get; set; }
        public string? Genre { get; set; }
    }
}
