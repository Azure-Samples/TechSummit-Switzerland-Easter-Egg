using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TechSummitEasterEgg
{
    public static class VerifyId
    {
        [FunctionName("VerifyId")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(
                AuthorizationLevel.Anonymous, 
                "get", 
                Route = "verify/id/{id}")]
            HttpRequest req,
            string id,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            log.LogInformation("Subscription ID: " + Environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME"));
            log.LogInformation("Received ID: " + id);

            if (!Environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME").StartsWith(id))
            {
                return new BadRequestObjectResult("Subscription ID doesn't match");
            }

            return new OkObjectResult("All good");
        }
    }
}
