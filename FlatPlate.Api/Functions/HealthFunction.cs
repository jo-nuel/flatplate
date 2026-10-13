using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace FlatPlate.Api.Functions;

/// <summary>
/// Provides a lightweight endpoint for checking that the API host is running.
/// </summary>
public sealed class HealthFunction
{
    /// <summary>
    /// Returns a successful response without querying the database.
    /// </summary>
    [Function("Health")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]
        HttpRequest request)
    {
        return new OkObjectResult(new { Status = "Healthy" });
    }
}
