using System.Text.Json;
using EmbedIO;
using EmbedIO.Routing;
using SQLiteNetExtensions.Extensions;

public class FilesController : Controller {
    // TODO: Should probably move this method into DiffsController
    [Route(HttpVerbs.Post, "/files/roll-back")]
    public async Task Store() {
        RollBackFileRequest? data = await JsonSerializer
            .DeserializeAsync<RollBackFileRequest>(Request.InputStream)
        ;

        if (data == null) {
            await SendBadRequest();
            return;
        }

        FileDiff? diff = Database.Connection.Find<FileDiff>(data.diffId);

        if (diff == null) {
            await SendBadRequest();
            return;
        }

        Database.Connection.GetChild(diff, d => d.File);

        if (diff.File == null) {
            await SendNotFound();
            return;
        }

        if (diff.File.Archived) {
            await SendBadRequest();
            return;
        }

        try {
            await Diff.RollBack(diff);
        }
        catch {
            await SendInternalServerError();
            return;
        }

        await SendOk();
    }

    [Route(HttpVerbs.Post, "/files/{file}/restore")]
    public async Task Restore() {
        int fileId;

        try {
            fileId = int.Parse(Route["file"]);
        }
        catch {
            await SendBadRequest();
            return;
        }

        File? file = Database.Connection.Find<File>(fileId);

        if (file == null) {
            await SendNotFound();
            return;
        }

        try {
            await Backup.Restore(file);
        }
        catch (Exception e) {
            await SendInternalServerError(e);
            return;
        }

        await SendOk();
    }

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

        File? file = Database.Connection.Find<File>(fileId);

        if (file == null) {
            await SendNotFound();
            return;
        }

        FileDiff[] diffs = Database.Connection
            .Table<FileDiff>()
            .Where(diff => diff.FileId == file.Id)
            .ToArray()
        ;

        Branch[] branches;
        if (diffs.Length > 0) {
            string diffIds = string.Join(',', diffs.Select(diff => diff.Id));
            string query = "SELECT branches.* " +
                "FROM branches " +
                "JOIN file_diffs ON file_diffs.id = branches.first_diff_id " +
                $"WHERE branches.first_diff_id IN ({diffIds}) " +
                "ORDER BY file_diffs.created_at"
            ;
            branches = Database.Connection.Query<Branch>(query).ToArray();
        } else branches = [];

        await SendJson(new { file, diffs, branches });
    }

    [Route(HttpVerbs.Patch, "/files/{file}")]
    public async Task Update() {
        UpdateFileRequest? data = await JsonSerializer
            .DeserializeAsync<UpdateFileRequest>(Request.InputStream)
        ;

        if (data == null || string.IsNullOrWhiteSpace(data.name)) {
            await SendBadRequest();
            return;
        }

        int fileId;

        try {
            fileId = int.Parse(Route["file"]);
        }
        catch {
            await SendBadRequest();
            return;
        }

        File? file = Database.Connection.Find<File>(fileId);

        if (file == null) {
            await SendNotFound();
            return;
        }

        try {
            file.Name = data.name;
            Database.Connection.Update(file);
        }
        catch {
            await SendInternalServerError();
            return;
        }

        await SendOk();
    }

    [Route(HttpVerbs.Delete, "/files/{file}")]
    public async Task Destroy() {
        int fileId;

        try {
            fileId = int.Parse(Route["file"]);
        }
        catch {
            await SendBadRequest();
            return;
        }

        File? file = Database.Connection.Find<File>(fileId);

        if (file == null) {
            await SendNotFound();
            return;
        }

        if (!file.Archived) {
            await SendBadRequest();
            return;
        }

        try {
            Backup.Delete(file);
        }
        catch {
            await SendInternalServerError();
            return;
        }

        await SendOk();
    }

    // TODO: Should probably move this to DiffsController
    public sealed record RollBackFileRequest(int diffId);

    public sealed record UpdateFileRequest(string name);
}
