using System.Text.Json;
using EmbedIO;
using EmbedIO.WebApi;

public abstract class Controller : WebApiController {
    protected async Task SendJson(object data) {
        Response.ContentType = "application/json";

        await HttpContext.SendStringAsync(
            JsonSerializer.Serialize(data),
            "application/json",
            System.Text.Encoding.UTF8
        );
    }

    protected async Task SendOk() {
        Response.StatusCode = 200;
        await SendJson(new { status = "ok" });
    }

    protected async Task SendBadRequest() {
        Response.StatusCode = 400;
        await SendJson(new { error = "Bad Request" });
    }

    protected async Task SendNotFound() {
        Response.StatusCode = 404;
        await SendJson(new { error = "Not Found" });
    }

    protected async Task SendInternalServerError() {
        Response.StatusCode = 500;
        await SendJson(new { error = "Internal Server Error" });
    }
}
