using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BaseCore.LogService;
using BaseCore.LogService.Entities;

namespace BaseCore.AuditLog.Controllers
{
    //[Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AuditLogController : Controller
    {
        private readonly ILogErrorService _logErrorService;
        public AuditLogController(ILogErrorService logErrorService)
        {
            _logErrorService = logErrorService;
        }

        // GET: api/auditLog
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var result = await _logErrorService.GetAllListAsync();
            return Json(result);
        }

        // GET: api/auditLog/testError
        [Route("/api/auditLog/testError")]
        [HttpGet]
        public IActionResult TestError()
        {
            throw new Exception("Exception while fetching all the auditLog from the storage.");
        }

        // POST: api/auditLog
        [HttpPost]
        public async Task<IActionResult> Post([FromBody]LogError model)
        {
            await _logErrorService.CreateAsync(model);
            return new OkObjectResult(model);
        }
    }
}