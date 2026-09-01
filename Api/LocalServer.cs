using System.Reflection;
using EmbedIO;
using EmbedIO.WebApi;

namespace Api;

public static class LocalServer {
    static WebServer? Server;

    public static void Start() => Task.Run(async () => StartAsync());

    public static async Task StartAsync() {
        if (Server != null) return;

        try {
            Server = new((WebServerOptions options) => {
                options
                    .WithUrlPrefix("http://localhost:5001/")
                    .WithMode(HttpListenerMode.EmbedIO)
                ;
            });

            WebApiModule module = new("/api");
            RegisterAllControllers(module);

            await Server.WithCors().WithModule(module).RunAsync();
        }
        catch (Exception e) {
            System.Diagnostics.Debug.WriteLine(
                $"Local server error: {e.Message}"
            );
        }
    }

    public static void Stop() {
        if (Server == null) return;

        Server.Dispose();
        Server = null;
    }

    static void RegisterAllControllers(WebApiModule module) {
        IEnumerable<Type> controllers = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where((Type t) => {
                return typeof(WebApiController).IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    t.IsClass
                ;
            })
        ;

        foreach (Type type in controllers) module.WithController(type);
    }
}
