using DevTaskHub.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DevTaskHub.Tests;

public class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsHealthyStatus()
    {
        var controller = new HealthController();

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(new { status = "Healthy" }.ToString(), ok.Value!.ToString());
    }
}
