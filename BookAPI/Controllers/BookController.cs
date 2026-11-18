using BookAPI.Repositories;
using Common.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace BookAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookController : ControllerBase
    {
        private readonly IMongoRepository<Book> _bookRepository;
        public BookController(IMongoRepository<Book> bookRepository)
        {
            _bookRepository = bookRepository;
        }

        [HttpGet]
        public List<Book> GetAllBooks()
        {
            return _bookRepository.GetAllRecords();
        }

        [HttpPost]
        public IActionResult CreateBook(Book book)
        {
            try
            {
                var result = _bookRepository.InsertRecord(book);
                return Ok(result);
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return Conflict($"Există deja o carte cu Id {book.Id}");
            }
        }

        [HttpGet("{id}")]
        public Book GetBookById(Guid id)
        {
            var result = _bookRepository.GetRecordById(id);

            return result;
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteBook(Guid id)
        {
           bool isDeleted = _bookRepository.DeleteRecord(id);

            if(!isDeleted)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpPut]
        public IActionResult UpsertBook(Book book)
        {
            if (book.Id == Guid.Empty)
            {
                book.Id = Guid.NewGuid();
            }

            _bookRepository.UpsertRecord(book);

            return Ok(book);
        }

    }
}
