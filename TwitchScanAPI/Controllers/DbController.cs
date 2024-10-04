using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using TwitchScanAPI.DbContext;

namespace TwitchScanAPI.Controllers
{
    [Route("[controller]/[action]")]
    public class DbController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly MongoDbContext _context;
        
        public DbController(IConfiguration configuration, MongoDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }
        
        [HttpGet]
        public ActionResult GetDbSize()
        {
            var masterKey = _configuration["MasterKey"];
            if (string.IsNullOrEmpty(masterKey))
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
            if (masterKey != Request.Headers["MasterKey"])
            {
                return StatusCode(StatusCodes.Status401Unauthorized);
            }
            var collection = _context.StatisticHistory;
            var stats = collection.EstimatedDocumentCount();
            return Ok(stats);
        }

        [HttpDelete]
        public ActionResult CleanDb()
        {
            var masterKey = _configuration["MasterKey"];
            if (string.IsNullOrEmpty(masterKey))
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
            if (masterKey != Request.Headers["MasterKey"])
            {
                return StatusCode(StatusCodes.Status401Unauthorized);
            }
            _context.StatisticHistory.DeleteMany(_ => true);
            return Ok();
        }
    }
}