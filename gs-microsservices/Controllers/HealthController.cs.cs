using Microsoft.AspNetCore.Mvc;

namespace gs_microsservices.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult VerificarStatus()
        {
            return Ok(new { Status = "Service is up and running" });
        }
    }
}
