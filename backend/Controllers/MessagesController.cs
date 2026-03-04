using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend;

namespace backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessagesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public MessagesController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("db")]
        public async Task<string> GetMessageFromDb()
        {
            var msg = await _db.Messages.FirstAsync();
            return msg.Text;
        }
    }
}
