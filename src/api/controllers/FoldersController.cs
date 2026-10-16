using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using EmbedIO;
using EmbedIO.Routing;
using SQLiteNetExtensions.Extensions;

public class FoldersController : Controller {
    [Route(HttpVerbs.Get, "/folders")]
    public async Task Index() {
        List<Folder> folders = Database.Connection.Table<Folder>().ToList();
        await SendJson(folders);
    }

    [Route(HttpVerbs.Post, "/folders")]
    public async Task Store() {
        CreateFolderRequest? data = await JsonSerializer
            .DeserializeAsync<CreateFolderRequest>(Request.InputStream)
        ;

        if (data == null || string.IsNullOrWhiteSpace(data.path)) {
            await SendBadRequest();
            return;
        }

        try {
            Backup.AddFolder(data.path);
        }
        catch {
            await SendInternalServerError();
            return;
        }

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

        Folder? folder = Database.Connection.FindWithChildren<Folder>(folderId);

        if (folder == null) {
            await SendNotFound();
            return;
        }

        await SendJson(new { folder });
    }

    [Route(HttpVerbs.Patch, "/folders/{folder}")]
    public async Task Update() {
        UpdateFolderRequest? data = await JsonSerializer
            .DeserializeAsync<UpdateFolderRequest>(Request.InputStream)
        ;

        if (data == null || string.IsNullOrWhiteSpace(data.name)) {
            await SendBadRequest();
            return;
        }

        int folderId;

        try {
            folderId = int.Parse(Route["folder"]);
        }
        catch {
            await SendBadRequest();
            return;
        }

        Folder? folder = Database.Connection.Find<Folder>(folderId);

        if (folder == null) {
            await SendNotFound();
            return;
        }

        try {
            folder.Name = data.name;
            Database.Connection.Update(folder);
        }
        catch {
            await SendInternalServerError();
            return;
        }

        await SendOk();
    }

    [Route(HttpVerbs.Delete, "/folders/{folder}")]
    public async Task Destroy() {
        int folderId;

        try {
            folderId = int.Parse(Route["folder"]);
        }
        catch {
            await SendBadRequest();
            return;
        }

        Folder? folder = Database.Connection.Find<Folder>(folderId);

        if (folder == null) {
            await SendNotFound();
            return;
        }

        if (!folder.Archived) {
            await SendBadRequest();
            return;
        }

        try {
            Backup.Delete(folder);
        }
        catch {
            await SendInternalServerError();
            return;
        }

        await SendOk();
    }

    public sealed record CreateFolderRequest(string name, string path);

    public sealed record UpdateFolderRequest(string name);
}
