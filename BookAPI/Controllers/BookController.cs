using BookAPI.Repositories;
using Common.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
            book.LastChangedAt = DateTime.UtcNow;
            var result = _bookRepository.InsertRecord(book);

            return Ok(result);
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

            book.LastChangedAt = DateTime.UtcNow;
            _bookRepository.UpsertRecord(book);

            return Ok(book);
        }

    }
}
