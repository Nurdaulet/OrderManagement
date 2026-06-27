using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Controllers;

namespace OrderManagement.Tests;

public sealed class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsHealthyStatus()
    {
        var controller = new HealthController();

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("Healthy", payload.Status);
    }
}
