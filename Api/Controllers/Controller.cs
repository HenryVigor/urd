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
}
