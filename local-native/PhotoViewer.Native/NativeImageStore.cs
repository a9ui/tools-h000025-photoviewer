using Microsoft.Data.Sqlite;

namespace PhotoViewer.Native;

internal sealed class NativeImageStore
{
    private readonly string _projectRoot;
    private readonly string _databasePath;
    private readonly string _connectionString;

    public NativeImageStore(string projectRoot)
    {
        _projectRoot = projectRoot;
        _databasePath = Path.Combine(projectRoot, ".cache", "native", "photoviewer-native.sqlite");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public string DatabasePath => _databasePath;

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;

            CREATE TABLE IF NOT EXISTS images (
              id TEXT PRIMARY KEY,
              absolute_path TEXT NOT NULL,
              filename TEXT NOT NULL,
              folder TEXT NOT NULL,
              extension TEXT NOT NULL,
              size_bytes INTEGER NOT NULL,
              created_at_utc TEXT NOT NULL,
              modified_at_utc TEXT NOT NULL,
              favorite_level INTEGER NOT NULL DEFAULT 0,
              scan_root TEXT NOT NULL,
              indexed_at_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_images_scan_root ON images(scan_root);
            CREATE INDEX IF NOT EXISTS idx_images_modified ON images(modified_at_utc DESC);
            CREATE INDEX IF NOT EXISTS idx_images_favorite ON images(favorite_level DESC);
            CREATE INDEX IF NOT EXISTS idx_images_filename ON images(filename COLLATE NOCASE);

            CREATE TABLE IF NOT EXISTS scan_roots (
              root_path TEXT PRIMARY KEY,
              last_scan_started_utc TEXT NOT NULL,
              last_scan_finished_utc TEXT NOT NULL,
              image_count INTEGER NOT NULL,
              elapsed_ms INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS favorites (
              image_id TEXT PRIMARY KEY,
              absolute_path TEXT NOT NULL,
              level INTEGER NOT NULL,
              imported_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS import_runs (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              imported_at_utc TEXT NOT NULL,
              favorite_count INTEGER NOT NULL,
              album_count INTEGER NOT NULL,
              settings_found INTEGER NOT NULL,
              image_count INTEGER NOT NULL,
              source_root TEXT NOT NULL,
              localstorage_note TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public NativeImportReport ImportProjectState()
    {
        Initialize();
        var favorites = NativeStateBridge.LoadFavorites(_projectRoot);
        var summary = NativeStateBridge.ReadSummary(_projectRoot);
        var importedAt = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        ExecuteNonQuery(connection, transaction, "DELETE FROM favorites");
        foreach (var (path, level) in favorites)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO favorites(image_id, absolute_path, level, imported_at_utc)
                VALUES ($id, $path, $level, $imported)
                ON CONFLICT(image_id) DO UPDATE SET
                  absolute_path = excluded.absolute_path,
                  level = excluded.level,
                  imported_at_utc = excluded.imported_at_utc
                """;
            insert.Parameters.AddWithValue("$id", path);
            insert.Parameters.AddWithValue("$path", path);
            insert.Parameters.AddWithValue("$level", level);
            insert.Parameters.AddWithValue("$imported", importedAt.ToString("O"));
            insert.ExecuteNonQuery();
        }

        ExecuteNonQuery(connection, transaction, "UPDATE images SET favorite_level = 0");
        ExecuteNonQuery(connection, transaction, """
            UPDATE images
            SET favorite_level = COALESCE((SELECT level FROM favorites WHERE favorites.image_id = images.id), 0)
            """);

        using var run = connection.CreateCommand();
        run.Transaction = transaction;
        run.CommandText = """
            INSERT INTO import_runs(
              imported_at_utc,
              favorite_count,
              album_count,
              settings_found,
              image_count,
              source_root,
              localstorage_note
            )
            VALUES ($imported, $favorites, $albums, $settings, $images, $root, $note)
            """;
        run.Parameters.AddWithValue("$imported", importedAt.ToString("O"));
        run.Parameters.AddWithValue("$favorites", favorites.Count);
        run.Parameters.AddWithValue("$albums", summary.AlbumCount);
        run.Parameters.AddWithValue("$settings", summary.SettingsFound ? 1 : 0);
        run.Parameters.AddWithValue("$images", CountImages(connection, transaction));
        run.Parameters.AddWithValue("$root", _projectRoot);
        run.Parameters.AddWithValue("$note", "Browser pvu_* localStorage is not read directly; use explicit export/import in a later milestone.");
        run.ExecuteNonQuery();

        transaction.Commit();

        return new NativeImportReport(
            DatabasePath: _databasePath,
            FavoriteCount: favorites.Count,
            AlbumCount: summary.AlbumCount,
            SettingsFound: summary.SettingsFound,
            ImageCount: CountImages(),
            ImportedAtUtc: importedAt
        );
    }

    public Dictionary<string, int> LoadFavorites()
    {
        Initialize();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT absolute_path, level FROM favorites";

        using var reader = command.ExecuteReader();
        var favorites = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            favorites[reader.GetString(0)] = reader.GetInt32(1);
        }

        return favorites;
    }

    public void SaveScanResult(string root, IReadOnlyList<NativeImageRecord> images, TimeSpan elapsed)
    {
        Initialize();
        var resolvedRoot = Path.GetFullPath(root);
        var startedAt = DateTime.UtcNow - elapsed;
        var finishedAt = DateTime.UtcNow;
        var indexedAt = finishedAt.ToString("O");

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var rootCommand = connection.CreateCommand())
        {
            rootCommand.Transaction = transaction;
            rootCommand.CommandText = """
                INSERT INTO scan_roots(root_path, last_scan_started_utc, last_scan_finished_utc, image_count, elapsed_ms)
                VALUES ($root, $started, $finished, $count, $elapsed)
                ON CONFLICT(root_path) DO UPDATE SET
                  last_scan_started_utc = excluded.last_scan_started_utc,
                  last_scan_finished_utc = excluded.last_scan_finished_utc,
                  image_count = excluded.image_count,
                  elapsed_ms = excluded.elapsed_ms
                """;
            rootCommand.Parameters.AddWithValue("$root", resolvedRoot);
            rootCommand.Parameters.AddWithValue("$started", startedAt.ToString("O"));
            rootCommand.Parameters.AddWithValue("$finished", finishedAt.ToString("O"));
            rootCommand.Parameters.AddWithValue("$count", images.Count);
            rootCommand.Parameters.AddWithValue("$elapsed", (long)elapsed.TotalMilliseconds);
            rootCommand.ExecuteNonQuery();
        }

        using var upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = """
            INSERT INTO images(
              id,
              absolute_path,
              filename,
              folder,
              extension,
              size_bytes,
              created_at_utc,
              modified_at_utc,
              favorite_level,
              scan_root,
              indexed_at_utc
            )
            VALUES (
              $id,
              $path,
              $filename,
              $folder,
              $extension,
              $size,
              $created,
              $modified,
              $favorite,
              $root,
              $indexed
            )
            ON CONFLICT(id) DO UPDATE SET
              absolute_path = excluded.absolute_path,
              filename = excluded.filename,
              folder = excluded.folder,
              extension = excluded.extension,
              size_bytes = excluded.size_bytes,
              created_at_utc = excluded.created_at_utc,
              modified_at_utc = excluded.modified_at_utc,
              favorite_level = excluded.favorite_level,
              scan_root = excluded.scan_root,
              indexed_at_utc = excluded.indexed_at_utc
            """;
        var id = upsert.Parameters.Add("$id", SqliteType.Text);
        var path = upsert.Parameters.Add("$path", SqliteType.Text);
        var filename = upsert.Parameters.Add("$filename", SqliteType.Text);
        var folder = upsert.Parameters.Add("$folder", SqliteType.Text);
        var extension = upsert.Parameters.Add("$extension", SqliteType.Text);
        var size = upsert.Parameters.Add("$size", SqliteType.Integer);
        var created = upsert.Parameters.Add("$created", SqliteType.Text);
        var modified = upsert.Parameters.Add("$modified", SqliteType.Text);
        var favorite = upsert.Parameters.Add("$favorite", SqliteType.Integer);
        var scanRoot = upsert.Parameters.Add("$root", SqliteType.Text);
        var indexed = upsert.Parameters.Add("$indexed", SqliteType.Text);

        foreach (var image in images)
        {
            id.Value = image.Id;
            path.Value = image.AbsolutePath;
            filename.Value = image.Filename;
            folder.Value = image.Folder;
            extension.Value = Path.GetExtension(image.AbsolutePath).ToLowerInvariant();
            size.Value = image.SizeBytes;
            created.Value = image.CreatedAtUtc.ToString("O");
            modified.Value = image.ModifiedAtUtc.ToString("O");
            favorite.Value = image.FavoriteLevel;
            scanRoot.Value = resolvedRoot;
            indexed.Value = indexedAt;
            upsert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public List<NativeImageRecord> LoadImagesForRoot(string root)
    {
        Initialize();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, absolute_path, filename, folder, size_bytes, created_at_utc, modified_at_utc, favorite_level
            FROM images
            WHERE scan_root = $root
            ORDER BY modified_at_utc DESC, absolute_path COLLATE NOCASE ASC
            """;
        command.Parameters.AddWithValue("$root", Path.GetFullPath(root));

        using var reader = command.ExecuteReader();
        var images = new List<NativeImageRecord>();
        while (reader.Read())
        {
            images.Add(new NativeImageRecord(
                Id: reader.GetString(0),
                AbsolutePath: reader.GetString(1),
                Filename: reader.GetString(2),
                Folder: reader.GetString(3),
                SizeBytes: reader.GetInt64(4),
                CreatedAtUtc: DateTime.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
                ModifiedAtUtc: DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind),
                FavoriteLevel: reader.GetInt32(7)
            ));
        }

        return images;
    }

    public int CountImages()
    {
        Initialize();
        using var connection = OpenConnection();
        return CountImages(connection, null);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static int CountImages(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM images";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
