using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using EmbedIO;
using EmbedIO.Routing;

public class FoldersController : Controller {
    [Route(HttpVerbs.Get, "/folders")]
    public async Task Index() {
        List<Folder> folders = Global.Database.Table<Folder>().ToList();
        await SendJson(folders);
    }

    [Route(HttpVerbs.Post, "/folders")]
    public async Task Store() {
        CreateFolderRequest? data = await JsonSerializer
            .DeserializeAsync<CreateFolderRequest>(Request.InputStream)
        ;

        if (
            data == null ||
            string.IsNullOrWhiteSpace(data.name) ||
            string.IsNullOrWhiteSpace(data.path)
        ) {
            await SendBadRequest();
            return;
        }

        try {
            Folder.Insert(new() {
                Name = data.name,
                Path = data.path,
            });
        }
        catch (Exception e) {
            System.Diagnostics.Debug.WriteLine($"Failed to create folder: {e}");
            await SendInternalServerError();
            return;
        }

        FolderWatcherService.StartWatching(data.path);

        await SendOk();
    }

    [Route(HttpVerbs.Get, "/folders/pick")]
    public async Task Pick() {
        FolderPickerResult result = await FolderPicker.Default.PickAsync(new());

        if (result.IsCancelled || result.Folder == null) {
            await SendJson(new { cancelled = true });
            return;
        }

        await SendJson(result.Folder.Path);
    }

    [Route(HttpVerbs.Get, "/folders/{folder}")]
    public async Task Show() {
        int folderId;

        try {
            folderId = int.Parse(Route["folder"]);
        }
        catch {
            await SendBadRequest();
            return;
        }

        Folder? folder = Global
            .Database
            .Table<Folder>()
            .FirstOrDefault(f => f.Id == folderId)
        ;

        if (folder == null) {
            await SendNotFound();
            return;
        }

        List<File> files = Global
            .Database
            .Table<File>()
            .Where(file => !file.Deleted)
            .Where(file => file.FolderId == folder.Id)
            .ToList()
        ;

        await SendJson(new { folder, files });
    }

    public sealed record CreateFolderRequest(string name, string path);
}
