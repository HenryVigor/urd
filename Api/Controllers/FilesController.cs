using EmbedIO;
using EmbedIO.Routing;

public class FilesController : Controller {
    [Route(HttpVerbs.Get, "/files/{file}")]
    public async Task Show() {
        int fileId;

        try {
            fileId = int.Parse(Route["file"]);
        }
        catch {
            await SendBadRequest();
            return;
        }

        File? file = Global.Database
            .Table<File>()
            .FirstOrDefault(f => f.Id == fileId)
        ;

        if (file == null) {
            await SendNotFound();
            return;
        }

        await SendJson(new { file });
    }
}
