using EmbedIO;
using EmbedIO.Routing;

public class ExampleController : Controller {
    [Route(HttpVerbs.Get, "/helloworld")]
    public async Task GetHelloWorld() {
        await SendJson(new { data = "Hello World!" });
    }
}
