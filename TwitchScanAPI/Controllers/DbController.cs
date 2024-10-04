using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using TwitchScanAPI.Controllers.Annotations;
using TwitchScanAPI.DbContext;

namespace TwitchScanAPI.Controllers
{
    /// <summary>
    /// Controller for database operations. Protected by a master key.
    /// </summary>
    [MasterKey]
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
            var collection = _context.StatisticHistory;
            var stats = collection.EstimatedDocumentCount();
            return Ok(stats);
        }

        [HttpDelete]
        public ActionResult CleanDb()
        {
            _context.StatisticHistory.DeleteMany(_ => true);
            return Ok();
        }
    }
}