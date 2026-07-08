using Microsoft.Data.Sqlite;
using System.Text.Json;

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
              indexed_at_utc TEXT NOT NULL,
              width INTEGER,
              height INTEGER
            );

            CREATE INDEX IF NOT EXISTS idx_images_scan_root ON images(scan_root);
            CREATE INDEX IF NOT EXISTS idx_images_modified ON images(modified_at_utc DESC);
            CREATE INDEX IF NOT EXISTS idx_images_favorite ON images(favorite_level DESC);
            CREATE INDEX IF NOT EXISTS idx_images_filename ON images(filename COLLATE NOCASE);

            CREATE VIRTUAL TABLE IF NOT EXISTS image_search_fts USING fts5(
              image_id UNINDEXED,
              scan_root UNINDEXED,
              search_text,
              tokenize = 'unicode61 remove_diacritics 0'
            );

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

            CREATE TABLE IF NOT EXISTS seen_images (
              image_id TEXT PRIMARY KEY,
              absolute_path TEXT NOT NULL,
              seen_at_utc TEXT NOT NULL,
              source TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_seen_images_path ON seen_images(absolute_path);

            CREATE TABLE IF NOT EXISTS albums (
              album_id TEXT PRIMARY KEY,
              name TEXT NOT NULL,
              image_count INTEGER NOT NULL,
              imported_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS album_images (
              album_id TEXT NOT NULL,
              image_id TEXT NOT NULL,
              absolute_path TEXT NOT NULL,
              position INTEGER NOT NULL,
              imported_at_utc TEXT NOT NULL,
              PRIMARY KEY(album_id, image_id)
            );

            CREATE INDEX IF NOT EXISTS idx_album_images_path ON album_images(absolute_path);

            CREATE TABLE IF NOT EXISTS browser_state (
              key TEXT PRIMARY KEY,
              value TEXT NOT NULL,
              imported_at_utc TEXT NOT NULL,
              source_path TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS cache_compatibility (
              checked_at_utc TEXT PRIMARY KEY,
              folder TEXT NOT NULL,
              images_checked INTEGER NOT NULL,
              thumb_compatible INTEGER NOT NULL,
              thumb_missing INTEGER NOT NULL,
              thumb_incompatible INTEGER NOT NULL,
              display_compatible INTEGER NOT NULL,
              display_missing INTEGER NOT NULL,
              display_incompatible INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS native_settings (
              key TEXT PRIMARY KEY,
              value TEXT NOT NULL,
              updated_at_utc TEXT NOT NULL
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
        EnsureColumn(connection, "images", "width", "INTEGER");
        EnsureColumn(connection, "images", "height", "INTEGER");
    }

    public NativeImportReport ImportProjectState(string? browserStateExportPath = null)
    {
        Initialize();
        var warnings = new List<NativeImportWarning>();
        var favorites = NativeStateBridge.LoadFavorites(_projectRoot, warnings);
        var albums = NativeStateBridge.LoadAlbums(_projectRoot, warnings);
        var browserState = NativeStateBridge.LoadBrowserStateExport(_projectRoot, browserStateExportPath, warnings);
        var resolvedBrowserStateExportPath = NativeStateBridge.ResolveBrowserStateExportPath(_projectRoot, browserStateExportPath) ?? "";
        var settingsPath = Path.Combine(_projectRoot, ".cache", "settings.json");
        var settingsFound = File.Exists(settingsPath);
        var browserSettingsJson = NativeStateBridge.LoadSettingsJson(_projectRoot, warnings);
        var importedAt = DateTime.UtcNow;
        var importedSeenCount = 0;
        var browserStateImportFailed = warnings.Any(static warning =>
            string.Equals(warning.Source, "browser-state-export", StringComparison.Ordinal));

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

        ExecuteNonQuery(connection, transaction, "DELETE FROM albums");
        ExecuteNonQuery(connection, transaction, "DELETE FROM album_images");
        foreach (var album in albums)
        {
            using var insertAlbum = connection.CreateCommand();
            insertAlbum.Transaction = transaction;
            insertAlbum.CommandText = """
                INSERT INTO albums(album_id, name, image_count, imported_at_utc)
                VALUES ($id, $name, $count, $imported)
                ON CONFLICT(album_id) DO UPDATE SET
                  name = excluded.name,
                  image_count = excluded.image_count,
                  imported_at_utc = excluded.imported_at_utc
                """;
            insertAlbum.Parameters.AddWithValue("$id", album.Id);
            insertAlbum.Parameters.AddWithValue("$name", album.Name);
            insertAlbum.Parameters.AddWithValue("$count", album.ImageCount);
            insertAlbum.Parameters.AddWithValue("$imported", importedAt.ToString("O"));
            insertAlbum.ExecuteNonQuery();

            var position = 0;
            foreach (var imagePath in album.ImagePaths)
            {
                using var insertImage = connection.CreateCommand();
                insertImage.Transaction = transaction;
                insertImage.CommandText = """
                    INSERT INTO album_images(album_id, image_id, absolute_path, position, imported_at_utc)
                    VALUES ($album, $image, $path, $position, $imported)
                    ON CONFLICT(album_id, image_id) DO UPDATE SET
                      absolute_path = excluded.absolute_path,
                      position = excluded.position,
                      imported_at_utc = excluded.imported_at_utc
                    """;
                insertImage.Parameters.AddWithValue("$album", album.Id);
                insertImage.Parameters.AddWithValue("$image", imagePath);
                insertImage.Parameters.AddWithValue("$path", imagePath);
                insertImage.Parameters.AddWithValue("$position", position++);
                insertImage.Parameters.AddWithValue("$imported", importedAt.ToString("O"));
                insertImage.ExecuteNonQuery();
            }
        }

        if (!string.IsNullOrWhiteSpace(resolvedBrowserStateExportPath) && !browserStateImportFailed)
        {
            ExecuteNonQuery(connection, transaction, "DELETE FROM browser_state");
            foreach (var record in browserState)
            {
                using var insertState = connection.CreateCommand();
                insertState.Transaction = transaction;
                insertState.CommandText = """
                    INSERT INTO browser_state(key, value, imported_at_utc, source_path)
                    VALUES ($key, $value, $imported, $source)
                    ON CONFLICT(key) DO UPDATE SET
                      value = excluded.value,
                      imported_at_utc = excluded.imported_at_utc,
                      source_path = excluded.source_path
                    """;
                insertState.Parameters.AddWithValue("$key", record.Key);
                insertState.Parameters.AddWithValue("$value", record.Value);
                insertState.Parameters.AddWithValue("$imported", importedAt.ToString("O"));
                insertState.Parameters.AddWithValue("$source", resolvedBrowserStateExportPath);
                insertState.ExecuteNonQuery();
                UpsertSetting(connection, transaction, $"browser_{record.Key}", record.Value, importedAt);
            }

            importedSeenCount = ImportBrowserSeenImages(connection, transaction, browserState, importedAt, warnings);
            ApplyBrowserStateMigrations(connection, transaction, browserState, importedAt, warnings);
        }

        UpsertSetting(connection, transaction, "browser_settings_found", settingsFound ? "1" : "0", importedAt);
        UpsertSetting(connection, transaction, "browser_settings_imported", browserSettingsJson is null ? "0" : "1", importedAt);
        if (browserSettingsJson is not null)
        {
            UpsertSetting(connection, transaction, "browser_settings_json", browserSettingsJson, importedAt);
        }
        else
        {
            UpsertSetting(connection, transaction, "browser_settings_json", "", importedAt);
        }

        UpsertSetting(connection, transaction, "browser_state_export_found", string.IsNullOrWhiteSpace(resolvedBrowserStateExportPath) ? "0" : "1", importedAt);
        UpsertSetting(connection, transaction, "browser_state_export_imported", !browserStateImportFailed && !string.IsNullOrWhiteSpace(resolvedBrowserStateExportPath) ? "1" : "0", importedAt);
        UpsertSetting(connection, transaction, "browser_seen_image_count", importedSeenCount.ToString(System.Globalization.CultureInfo.InvariantCulture), importedAt);
        if (!string.IsNullOrWhiteSpace(resolvedBrowserStateExportPath))
        {
            UpsertSetting(connection, transaction, "browser_state_export_path", resolvedBrowserStateExportPath, importedAt);
        }
        UpsertSetting(connection, transaction, "import_warning_count", warnings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), importedAt);
        UpsertSetting(connection, transaction, "import_warnings_json", JsonSerializer.Serialize(warnings), importedAt);
        UpsertSetting(connection, transaction, "import_recovery_summary", BuildRecoverySummary(warnings), importedAt);

        UpsertSetting(connection, transaction, "keybindings_json", DefaultKeyBindingsJson, importedAt);
        UpsertSetting(connection, transaction, "view_mode", GetSetting(connection, transaction, "view_mode") ?? "details", importedAt);

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
        run.Parameters.AddWithValue("$albums", albums.Count);
        run.Parameters.AddWithValue("$settings", settingsFound ? 1 : 0);
        run.Parameters.AddWithValue("$images", CountImages(connection, transaction));
        run.Parameters.AddWithValue("$root", _projectRoot);
        run.Parameters.AddWithValue("$note", BuildImportNote(browserState.Count, resolvedBrowserStateExportPath, browserStateImportFailed, warnings));
        run.ExecuteNonQuery();

        transaction.Commit();

        return new NativeImportReport(
            DatabasePath: _databasePath,
            FavoriteCount: favorites.Count,
            AlbumCount: albums.Count,
            AlbumImageCount: albums.Sum(static album => album.ImagePaths.Count),
            SettingsFound: settingsFound,
            BrowserStateKeyCount: browserState.Count,
            SeenImageCount: CountSeenImages(connection, null),
            ImageCount: CountImages(),
            ImportedAtUtc: importedAt,
            Warnings: warnings
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

        UpsertSetting(connection, transaction, "recent_folder", resolvedRoot, finishedAt);
        ExecuteNonQuery(connection, transaction, "DELETE FROM images WHERE scan_root = $root", ("$root", resolvedRoot));
        ExecuteNonQuery(connection, transaction, "DELETE FROM image_search_fts WHERE scan_root = $root", ("$root", resolvedRoot));

        UpsertImages(connection, transaction, images, resolvedRoot, indexedAt);
        transaction.Commit();
    }

    public void ApplyIncrementalScan(string root, NativeIncrementalScanResult result, TimeSpan elapsed, bool fullRescan)
    {
        Initialize();
        var resolvedRoot = Path.GetFullPath(root);
        var startedAt = DateTime.UtcNow - elapsed;
        var finishedAt = DateTime.UtcNow;
        var indexedAt = finishedAt.ToString("O");
        var totalCount = result.ScannedCount;

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
            rootCommand.Parameters.AddWithValue("$count", totalCount);
            rootCommand.Parameters.AddWithValue("$elapsed", (long)elapsed.TotalMilliseconds);
            rootCommand.ExecuteNonQuery();
        }

        UpsertSetting(connection, transaction, "recent_folder", resolvedRoot, finishedAt);

        if (fullRescan)
        {
            ExecuteNonQuery(connection, transaction, "DELETE FROM images WHERE scan_root = $root", ("$root", resolvedRoot));
            ExecuteNonQuery(connection, transaction, "DELETE FROM image_search_fts WHERE scan_root = $root", ("$root", resolvedRoot));
            UpsertImages(connection, transaction, result.AddedOrUpdated, resolvedRoot, indexedAt);
        }
        else
        {
            foreach (var removed in result.RemovedPaths)
            {
                ExecuteNonQuery(connection, transaction, "DELETE FROM images WHERE id = $id OR absolute_path = $path", ("$id", removed), ("$path", removed));
                ExecuteNonQuery(connection, transaction, "DELETE FROM image_search_fts WHERE image_id = $id", ("$id", removed));
            }

            if (result.AddedOrUpdated.Count > 0)
            {
                UpsertImages(connection, transaction, result.AddedOrUpdated, resolvedRoot, indexedAt);
            }

            EnsureSearchIndexForRoot(connection, transaction, resolvedRoot);
        }

        transaction.Commit();
    }

    public List<NativeImageRecord> LoadImagesForRoot(string root)
    {
        Initialize();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, absolute_path, filename, folder, size_bytes, created_at_utc, modified_at_utc, favorite_level, width, height,
                   CASE WHEN EXISTS (SELECT 1 FROM seen_images seen WHERE seen.image_id = images.id OR seen.absolute_path = images.absolute_path) THEN 1 ELSE 0 END AS is_seen
            FROM images
            WHERE scan_root = $root
            ORDER BY modified_at_utc DESC, absolute_path COLLATE NOCASE ASC
            """;
        command.Parameters.AddWithValue("$root", Path.GetFullPath(root));

        using var reader = command.ExecuteReader();
        var images = new List<NativeImageRecord>();
        while (reader.Read())
        {
            images.Add(ReadImage(reader));
        }

        return images;
    }

    public List<NativeImageRecord> LoadImagesForRoots(IEnumerable<string> roots)
    {
        return NativeFolderSet.NormalizeDistinct(roots)
            .SelectMany(LoadImagesForRoot)
            .OrderByDescending(static item => item.ModifiedAtUtc)
            .ThenBy(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<NativeImageRecord> SearchImagesIndexed(string root, string query, bool favoritesOnly, int limit = 200)
    {
        return SearchImagesIndexed(root, query, favoritesOnly, limit, out _);
    }

    public List<NativeImageRecord> SearchImagesIndexed(IEnumerable<string> roots, string query, bool favoritesOnly, int limit = 200)
    {
        return SearchImagesIndexed(roots, query, favoritesOnly, limit, out _);
    }

    public List<NativeImageRecord> SearchImagesIndexed(IEnumerable<string> roots, string query, bool favoritesOnly, int limit, out bool usedIndex)
    {
        usedIndex = true;
        var results = new List<NativeImageRecord>();
        foreach (var root in NativeFolderSet.NormalizeDistinct(roots))
        {
            var rootResults = SearchImagesIndexed(root, query, favoritesOnly, limit, out var rootUsedIndex);
            usedIndex = usedIndex && rootUsedIndex;
            results.AddRange(rootResults);
        }

        return results
            .OrderByDescending(static item => item.FavoriteLevel)
            .ThenByDescending(static item => item.ModifiedAtUtc)
            .ThenBy(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    public List<NativeImageRecord> SearchImagesIndexed(string root, string query, bool favoritesOnly, int limit, out bool usedIndex)
    {
        Initialize();
        var resolvedRoot = Path.GetFullPath(root);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        usedIndex = true;

        if (string.IsNullOrWhiteSpace(query))
        {
            command.CommandText = """
                SELECT id, absolute_path, filename, folder, size_bytes, created_at_utc, modified_at_utc, favorite_level, width, height,
                       CASE WHEN EXISTS (SELECT 1 FROM seen_images seen WHERE seen.image_id = images.id OR seen.absolute_path = images.absolute_path) THEN 1 ELSE 0 END AS is_seen
                FROM images
                WHERE scan_root = $root
                  AND ($favoritesOnly = 0 OR favorite_level > 0)
                ORDER BY favorite_level DESC, modified_at_utc DESC, absolute_path COLLATE NOCASE ASC
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$root", resolvedRoot);
            command.Parameters.AddWithValue("$favoritesOnly", favoritesOnly ? 1 : 0);
            command.Parameters.AddWithValue("$limit", limit);
        }
        else
        {
            try
            {
                command.CommandText = """
                    SELECT i.id, i.absolute_path, i.filename, i.folder, i.size_bytes, i.created_at_utc, i.modified_at_utc, i.favorite_level, i.width, i.height,
                           CASE WHEN EXISTS (SELECT 1 FROM seen_images seen WHERE seen.image_id = i.id OR seen.absolute_path = i.absolute_path) THEN 1 ELSE 0 END AS is_seen
                    FROM image_search_fts fts
                    JOIN images i ON i.id = fts.image_id
                    WHERE fts.scan_root = $root
                      AND image_search_fts MATCH $match
                      AND ($favoritesOnly = 0 OR i.favorite_level > 0)
                    ORDER BY i.favorite_level DESC, i.modified_at_utc DESC, i.absolute_path COLLATE NOCASE ASC
                    LIMIT $limit
                    """;
                command.Parameters.AddWithValue("$root", resolvedRoot);
                command.Parameters.AddWithValue("$match", BuildFtsQuery(query));
                command.Parameters.AddWithValue("$favoritesOnly", favoritesOnly ? 1 : 0);
                command.Parameters.AddWithValue("$limit", limit);

                using var ftsReader = command.ExecuteReader();
                var ftsImages = new List<NativeImageRecord>();
                while (ftsReader.Read())
                {
                    ftsImages.Add(ReadImage(ftsReader));
                }

                if (ftsImages.Count == 0)
                {
                    var likeImages = SearchImagesLike(connection, resolvedRoot, query, favoritesOnly, limit);
                    if (likeImages.Count > 0)
                    {
                        usedIndex = false;
                        return likeImages;
                    }
                }

                return ftsImages;
            }
            catch (SqliteException)
            {
                usedIndex = false;
                return SearchImagesLike(connection, resolvedRoot, query, favoritesOnly, limit);
            }
        }

        using var reader = command.ExecuteReader();
        var images = new List<NativeImageRecord>();
        while (reader.Read())
        {
            images.Add(ReadImage(reader));
        }

        return images;
    }

    public List<NativeImageRecord> SearchImagesForRoot(string root, string query, bool favoritesOnly, int limit = 200)
    {
        return SearchImagesIndexed(root, query, favoritesOnly, limit);
    }

    private static string BuildFtsQuery(string query)
    {
        var tokens = query
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static token => $"\"{token.Replace("\"", "\"\"")}\"*");
        return string.Join(' ', tokens);
    }

    private static List<NativeImageRecord> SearchImagesLike(
        SqliteConnection connection,
        string resolvedRoot,
        string query,
        bool favoritesOnly,
        int limit)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, absolute_path, filename, folder, size_bytes, created_at_utc, modified_at_utc, favorite_level, width, height,
                   CASE WHEN EXISTS (SELECT 1 FROM seen_images seen WHERE seen.image_id = images.id OR seen.absolute_path = images.absolute_path) THEN 1 ELSE 0 END AS is_seen
            FROM images
            WHERE scan_root = $root
              AND ($favoritesOnly = 0 OR favorite_level > 0)
              AND (
                $query = ''
                OR filename LIKE $like COLLATE NOCASE
                OR folder LIKE $like COLLATE NOCASE
                OR absolute_path LIKE $like COLLATE NOCASE
              )
            ORDER BY favorite_level DESC, modified_at_utc DESC, absolute_path COLLATE NOCASE ASC
            LIMIT $limit
            """;
        command.Parameters.AddWithValue("$root", resolvedRoot);
        command.Parameters.AddWithValue("$favoritesOnly", favoritesOnly ? 1 : 0);
        command.Parameters.AddWithValue("$query", query);
        command.Parameters.AddWithValue("$like", $"%{query}%");
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var images = new List<NativeImageRecord>();
        while (reader.Read())
        {
            images.Add(ReadImage(reader));
        }

        return images;
    }

    public string? LoadRecentFolder()
    {
        var folderSet = LoadRecentFolderSet();
        if (folderSet.Count > 0)
        {
            return NativeFolderSet.FormatForDisplay(folderSet);
        }

        return null;
    }

    public List<string> LoadRecentFolderSet()
    {
        Initialize();
        using var connection = OpenConnection();
        var storedSet = NativeFolderSet.Parse(GetSetting(connection, null, "recent_folder_set"));
        if (storedSet.Count > 0)
        {
            return storedSet;
        }

        var stored = GetSetting(connection, null, "recent_folder");
        if (!string.IsNullOrWhiteSpace(stored))
        {
            return NativeFolderSet.Parse(stored);
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT root_path
            FROM scan_roots
            ORDER BY last_scan_finished_utc DESC
            LIMIT 1
            """;
        var latestRoot = command.ExecuteScalar() as string;
        return NativeFolderSet.Parse(latestRoot);
    }

    public void SaveRecentFolderSet(IEnumerable<string> roots)
    {
        var normalized = NativeFolderSet.NormalizeDistinct(roots);
        if (normalized.Count == 0)
        {
            return;
        }

        Initialize();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        UpsertSetting(connection, transaction, "recent_folder_set", NativeFolderSet.FormatForSetting(normalized), now);
        UpsertSetting(connection, transaction, "recent_folder", normalized[0], now);
        transaction.Commit();
    }

    public string GetSetting(string key, string defaultValue)
    {
        Initialize();
        using var connection = OpenConnection();
        return GetSetting(connection, null, key) ?? defaultValue;
    }

    public void SaveSetting(string key, string value)
    {
        Initialize();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        UpsertSetting(connection, transaction, key, value, DateTime.UtcNow);
        transaction.Commit();
    }

    public void SaveViewState(
        string viewMode,
        string searchText,
        bool favoritesOnly,
        string favoriteFilter,
        bool enhancedOnly,
        string dateFilter,
        string dateFrom,
        string dateTo)
    {
        Initialize();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        UpsertSetting(connection, transaction, "view_mode", viewMode, now);
        UpsertSetting(connection, transaction, "search_text", searchText, now);
        UpsertSetting(connection, transaction, "favorites_only", favoritesOnly ? "1" : "0", now);
        UpsertSetting(connection, transaction, "favorite_filter", favoriteFilter, now);
        UpsertSetting(connection, transaction, "enhanced_only_filter", enhancedOnly ? "1" : "0", now);
        UpsertSetting(connection, transaction, "date_filter", dateFilter, now);
        UpsertSetting(connection, transaction, "date_from", dateFrom, now);
        UpsertSetting(connection, transaction, "date_to", dateTo, now);
        transaction.Commit();
    }

    public void SaveGalleryState(string absolutePath, int visibleIndex)
    {
        Initialize();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        UpsertSetting(connection, transaction, "last_selected_image", Path.GetFullPath(absolutePath), now);
        UpsertSetting(connection, transaction, "last_visible_index", visibleIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), now);
        transaction.Commit();
    }

    public void MarkImageSeen(string absolutePath, string source = "native")
    {
        Initialize();
        var normalizedPath = Path.GetFullPath(absolutePath);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        UpsertSeenImage(connection, transaction, normalizedPath, DateTime.UtcNow, source);
        transaction.Commit();
    }

    public void SetFavoriteLevel(string absolutePath, int level)
    {
        Initialize();
        var normalizedPath = Path.GetFullPath(absolutePath);
        var clamped = Math.Clamp(level, 0, 5);
        var now = DateTime.UtcNow.ToString("O");

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        if (clamped == 0)
        {
            using var deleteFavorite = connection.CreateCommand();
            deleteFavorite.Transaction = transaction;
            deleteFavorite.CommandText = "DELETE FROM favorites WHERE image_id = $id OR absolute_path = $path";
            deleteFavorite.Parameters.AddWithValue("$id", normalizedPath);
            deleteFavorite.Parameters.AddWithValue("$path", normalizedPath);
            deleteFavorite.ExecuteNonQuery();
        }
        else
        {
            using var upsertFavorite = connection.CreateCommand();
            upsertFavorite.Transaction = transaction;
            upsertFavorite.CommandText = """
                INSERT INTO favorites(image_id, absolute_path, level, imported_at_utc)
                VALUES ($id, $path, $level, $imported)
                ON CONFLICT(image_id) DO UPDATE SET
                  absolute_path = excluded.absolute_path,
                  level = excluded.level,
                  imported_at_utc = excluded.imported_at_utc
                """;
            upsertFavorite.Parameters.AddWithValue("$id", normalizedPath);
            upsertFavorite.Parameters.AddWithValue("$path", normalizedPath);
            upsertFavorite.Parameters.AddWithValue("$level", clamped);
            upsertFavorite.Parameters.AddWithValue("$imported", now);
            upsertFavorite.ExecuteNonQuery();
        }

        using var updateImage = connection.CreateCommand();
        updateImage.Transaction = transaction;
        updateImage.CommandText = "UPDATE images SET favorite_level = $level WHERE id = $id OR absolute_path = $path";
        updateImage.Parameters.AddWithValue("$level", clamped);
        updateImage.Parameters.AddWithValue("$id", normalizedPath);
        updateImage.Parameters.AddWithValue("$path", normalizedPath);
        updateImage.ExecuteNonQuery();
        transaction.Commit();
    }

    public void RemoveImage(string absolutePath)
    {
        Initialize();
        var normalizedPath = Path.GetFullPath(absolutePath);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var deleteImage = connection.CreateCommand())
        {
            deleteImage.Transaction = transaction;
            deleteImage.CommandText = "DELETE FROM images WHERE id = $id OR absolute_path = $path";
            deleteImage.Parameters.AddWithValue("$id", normalizedPath);
            deleteImage.Parameters.AddWithValue("$path", normalizedPath);
            deleteImage.ExecuteNonQuery();
        }

        using (var deleteFavorite = connection.CreateCommand())
        {
            deleteFavorite.Transaction = transaction;
            deleteFavorite.CommandText = "DELETE FROM favorites WHERE image_id = $id OR absolute_path = $path";
            deleteFavorite.Parameters.AddWithValue("$id", normalizedPath);
            deleteFavorite.Parameters.AddWithValue("$path", normalizedPath);
            deleteFavorite.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public int CountImages()
    {
        Initialize();
        using var connection = OpenConnection();
        return CountImages(connection, null);
    }

    public int CountAlbums()
    {
        Initialize();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM albums";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public int CountAlbumImages()
    {
        Initialize();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM album_images";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public int CountBrowserStateKeys()
    {
        Initialize();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM browser_state";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public int CountSettings()
    {
        Initialize();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM native_settings";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public int CountSeenImages()
    {
        Initialize();
        using var connection = OpenConnection();
        return CountSeenImages(connection, null);
    }

    public NativeCacheCompatibilityReport CheckCacheCompatibility(string folder)
    {
        Initialize();
        var images = LoadImagesForRoot(folder);
        var report = NativeCacheCompatibility.Check(_projectRoot, images);
        var checkedAt = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO cache_compatibility(
              checked_at_utc,
              folder,
              images_checked,
              thumb_compatible,
              thumb_missing,
              thumb_incompatible,
              display_compatible,
              display_missing,
              display_incompatible
            )
            VALUES ($checked, $folder, $images, $thumbCompatible, $thumbMissing, $thumbIncompatible, $displayCompatible, $displayMissing, $displayIncompatible)
            """;
        command.Parameters.AddWithValue("$checked", checkedAt.ToString("O"));
        command.Parameters.AddWithValue("$folder", Path.GetFullPath(folder));
        command.Parameters.AddWithValue("$images", report.ImagesChecked);
        command.Parameters.AddWithValue("$thumbCompatible", report.ThumbnailCompatible);
        command.Parameters.AddWithValue("$thumbMissing", report.ThumbnailMissing);
        command.Parameters.AddWithValue("$thumbIncompatible", report.ThumbnailIncompatible);
        command.Parameters.AddWithValue("$displayCompatible", report.DisplayCompatible);
        command.Parameters.AddWithValue("$displayMissing", report.DisplayMissing);
        command.Parameters.AddWithValue("$displayIncompatible", report.DisplayIncompatible);
        command.ExecuteNonQuery();
        transaction.Commit();
        return report;
    }

    private static int ImportBrowserSeenImages(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<NativeBrowserStateRecord> browserState,
        DateTime importedAt,
        ICollection<NativeImportWarning> warnings)
    {
        var seenRecord = browserState.FirstOrDefault(static item =>
            string.Equals(item.Key, "pvu_seen_images", StringComparison.Ordinal));
        if (seenRecord is null || string.IsNullOrWhiteSpace(seenRecord.Value))
        {
            return 0;
        }

        var imported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> seenImageIds;
        try
        {
            seenImageIds = ParseBrowserSeenImageIds(seenRecord.Value).ToList();
        }
        catch (JsonException)
        {
            warnings.Add(new NativeImportWarning(
                "browser-state-export:pvu_seen_images",
                "",
                "malformed-json-value",
                "pvu_seen_images was present but could not be parsed as JSON.",
                "Seen-image state was skipped; rerun the browser export or remove the malformed pvu_seen_images value, then run Import again."));
            return 0;
        }

        foreach (var imageId in seenImageIds)
        {
            if (!imported.Add(imageId))
            {
                continue;
            }

            UpsertSeenImage(connection, transaction, imageId, importedAt, "browser_export");
        }

        return imported.Count;
    }

    private static void ApplyBrowserStateMigrations(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<NativeBrowserStateRecord> browserState,
        DateTime importedAt,
        ICollection<NativeImportWarning> warnings)
    {
        var migrations = new List<string>();

        if (TryGetBrowserStateValue(browserState, "pvu_view", out var pvuView))
        {
            if (TryReadBrowserViewMode(pvuView, out var viewMode, out var warningMessage))
            {
                if (GetSetting(connection, transaction, "view_mode") is null)
                {
                    UpsertSetting(connection, transaction, "view_mode", viewMode, importedAt);
                    migrations.Add("pvu_view.viewMode->view_mode");
                }
            }
            else if (!string.IsNullOrWhiteSpace(warningMessage))
            {
                warnings.Add(new NativeImportWarning(
                    "browser-state-export:pvu_view",
                    "",
                    "malformed-json-value",
                    warningMessage,
                    "Browser view-mode state was skipped; rerun the browser export or remove the malformed pvu_view value, then run Import again."));
            }
        }

        if (TryGetBrowserStateValue(browserState, "pvu_enhanced_only", out var pvuEnhancedOnly))
        {
            if (TryReadBrowserBoolean(pvuEnhancedOnly, out var enhancedOnly, out var warningMessage))
            {
                if (GetSetting(connection, transaction, "enhanced_only_filter") is null)
                {
                    UpsertSetting(connection, transaction, "enhanced_only_filter", enhancedOnly ? "1" : "0", importedAt);
                    migrations.Add("pvu_enhanced_only->enhanced_only_filter");
                }
            }
            else if (!string.IsNullOrWhiteSpace(warningMessage))
            {
                warnings.Add(new NativeImportWarning(
                    "browser-state-export:pvu_enhanced_only",
                    "",
                    "malformed-boolean-value",
                    warningMessage,
                    "Browser enhanced-only state was skipped; rerun the browser export or remove the malformed pvu_enhanced_only value, then run Import again."));
            }
        }

        UpsertSetting(
            connection,
            transaction,
            "pvu_state_migration_count",
            migrations.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            importedAt);
        UpsertSetting(connection, transaction, "pvu_state_migrations", string.Join(",", migrations), importedAt);
    }

    private static bool TryGetBrowserStateValue(
        IReadOnlyList<NativeBrowserStateRecord> browserState,
        string key,
        out string value)
    {
        var record = browserState.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
        if (record is null)
        {
            value = "";
            return false;
        }

        value = record.Value;
        return true;
    }

    private static bool TryReadBrowserViewMode(string value, out string viewMode, out string? warningMessage)
    {
        if (TryNormalizeBrowserViewMode(value, out viewMode))
        {
            warningMessage = null;
            return true;
        }

        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            warningMessage = null;
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.ValueKind == JsonValueKind.String &&
                TryNormalizeBrowserViewMode(document.RootElement.GetString(), out viewMode))
            {
                warningMessage = null;
                return true;
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("viewMode", out var viewModeElement) &&
                viewModeElement.ValueKind == JsonValueKind.String &&
                TryNormalizeBrowserViewMode(viewModeElement.GetString(), out viewMode))
            {
                warningMessage = null;
                return true;
            }
        }
        catch (JsonException ex)
        {
            viewMode = "";
            warningMessage = ex.Message;
            return false;
        }

        viewMode = "";
        warningMessage = null;
        return false;
    }

    private static bool TryNormalizeBrowserViewMode(string? value, out string viewMode)
    {
        if (string.Equals(value, "grid", StringComparison.OrdinalIgnoreCase))
        {
            viewMode = "grid";
            return true;
        }

        if (string.Equals(value, "list", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "details", StringComparison.OrdinalIgnoreCase))
        {
            viewMode = "details";
            return true;
        }

        viewMode = "";
        return false;
    }

    private static bool TryReadBrowserBoolean(string value, out bool result, out string? warningMessage)
    {
        if (TryNormalizeBrowserBoolean(value, out result))
        {
            warningMessage = null;
            return true;
        }

        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            warningMessage = null;
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (TryReadJsonBoolean(document.RootElement, out result))
            {
                warningMessage = null;
                return true;
            }
        }
        catch (JsonException ex)
        {
            result = false;
            warningMessage = ex.Message;
            return false;
        }

        result = false;
        warningMessage = $"Unsupported boolean value: {trimmed}";
        return false;
    }

    private static bool TryReadJsonBoolean(JsonElement element, out bool result)
    {
        if (element.ValueKind == JsonValueKind.True)
        {
            result = true;
            return true;
        }

        if (element.ValueKind == JsonValueKind.False)
        {
            result = false;
            return true;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            if (number == 1)
            {
                result = true;
                return true;
            }

            if (number == 0)
            {
                result = false;
                return true;
            }
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return TryNormalizeBrowserBoolean(element.GetString(), out result);
        }

        result = false;
        return false;
    }

    private static bool TryNormalizeBrowserBoolean(string? value, out bool result)
    {
        var normalized = value?.Trim();
        if (string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "on", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (string.Equals(normalized, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "no", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "off", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }

    private static string BuildImportNote(
        int browserStateCount,
        string resolvedBrowserStateExportPath,
        bool browserStateImportFailed,
        IReadOnlyCollection<NativeImportWarning> warnings)
    {
        if (warnings.Count > 0)
        {
            return $"Import completed with {warnings.Count} recoverable warning(s): {BuildRecoverySummary(warnings)}";
        }

        if (browserStateImportFailed)
        {
            return "Browser localStorage export was skipped after a recoverable import warning.";
        }

        return browserStateCount > 0
            ? $"Imported {browserStateCount} pvu_* keys from explicit browser localStorage export."
            : string.IsNullOrWhiteSpace(resolvedBrowserStateExportPath)
                ? "Browser pvu_* localStorage is not read directly; no explicit export file was imported."
                : "Explicit browser localStorage export contained no pvu_* keys.";
    }

    private static string BuildRecoverySummary(IReadOnlyCollection<NativeImportWarning> warnings)
    {
        return warnings.Count == 0
            ? ""
            : string.Join(" | ", warnings.Select(static warning => $"{warning.Source}: {warning.RecoveryAction}"));
    }

    private static IEnumerable<string> ParseBrowserSeenImageIds(string value)
    {
        using var document = JsonDocument.Parse(value);
        foreach (var imageId in ParseSeenElement(document.RootElement))
        {
            yield return imageId;
        }
    }

    private static IEnumerable<string> ParseSeenElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            if (value.TrimStart().StartsWith("{", StringComparison.Ordinal) ||
                value.TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                using var nested = JsonDocument.Parse(value);
                foreach (var nestedId in ParseSeenElement(nested.RootElement))
                {
                    yield return nestedId;
                }

                yield break;
            }

            yield return NormalizeImageId(value);
            yield break;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var imageId in ParseSeenElement(item))
                {
                    yield return imageId;
                }
            }

            yield break;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (IsTruthyStorageValue(property.Value))
            {
                yield return NormalizeImageId(property.Name);
            }
        }
    }

    private static bool IsTruthyStorageValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.TryGetInt32(out var number) && number != 0,
            JsonValueKind.String => IsTruthyStorageString(value.GetString()),
            _ => false,
        };
    }

    private static bool IsTruthyStorageString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(value, "0", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeImageId(string value)
    {
        try
        {
            return Path.GetFullPath(value);
        }
        catch
        {
            return value;
        }
    }

    private static void UpsertSeenImage(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string imageId,
        DateTime seenAt,
        string source)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO seen_images(image_id, absolute_path, seen_at_utc, source)
            VALUES ($id, $path, $seenAt, $source)
            ON CONFLICT(image_id) DO UPDATE SET
              absolute_path = excluded.absolute_path,
              seen_at_utc = excluded.seen_at_utc,
              source = excluded.source
            """;
        command.Parameters.AddWithValue("$id", imageId);
        command.Parameters.AddWithValue("$path", imageId);
        command.Parameters.AddWithValue("$seenAt", seenAt.ToString("O"));
        command.Parameters.AddWithValue("$source", source);
        command.ExecuteNonQuery();
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

    private static int CountSeenImages(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM seen_images";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static NativeImageRecord ReadImage(SqliteDataReader reader)
    {
        return new NativeImageRecord(
            Id: reader.GetString(0),
            AbsolutePath: reader.GetString(1),
            Filename: reader.GetString(2),
            Folder: reader.GetString(3),
            SizeBytes: reader.GetInt64(4),
            CreatedAtUtc: DateTime.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
            ModifiedAtUtc: DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind),
            FavoriteLevel: reader.GetInt32(7),
            Width: reader.IsDBNull(8) ? null : reader.GetInt32(8),
            Height: reader.IsDBNull(9) ? null : reader.GetInt32(9),
            IsSeen: !reader.IsDBNull(10) && reader.GetInt32(10) != 0
        );
    }

    private static void UpsertImages(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<NativeImageRecord> images,
        string resolvedRoot,
        string indexedAt)
    {
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
              indexed_at_utc,
              width,
              height
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
              $indexed,
              $width,
              $height
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
              indexed_at_utc = excluded.indexed_at_utc,
              width = excluded.width,
              height = excluded.height
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
        var width = upsert.Parameters.Add("$width", SqliteType.Integer);
        var height = upsert.Parameters.Add("$height", SqliteType.Integer);

        using var ftsDelete = connection.CreateCommand();
        ftsDelete.Transaction = transaction;
        ftsDelete.CommandText = "DELETE FROM image_search_fts WHERE image_id = $id";

        using var fts = connection.CreateCommand();
        fts.Transaction = transaction;
        fts.CommandText = """
            INSERT INTO image_search_fts(image_id, scan_root, search_text)
            VALUES ($id, $root, $text)
            """;

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
            width.Value = image.Width.HasValue ? image.Width.Value : DBNull.Value;
            height.Value = image.Height.HasValue ? image.Height.Value : DBNull.Value;
            upsert.ExecuteNonQuery();

            ftsDelete.Parameters.Clear();
            ftsDelete.Parameters.AddWithValue("$id", image.Id);
            ftsDelete.ExecuteNonQuery();

            fts.Parameters.Clear();
            fts.Parameters.AddWithValue("$id", image.Id);
            fts.Parameters.AddWithValue("$root", resolvedRoot);
            fts.Parameters.AddWithValue("$text", BuildSearchText(image));
            fts.ExecuteNonQuery();
        }
    }

    private static string BuildSearchText(NativeImageRecord image)
    {
        return $"{image.Filename} {image.Folder} {image.AbsolutePath}";
    }

    private static void EnsureSearchIndexForRoot(SqliteConnection connection, SqliteTransaction transaction, string resolvedRoot)
    {
        using (var countImages = connection.CreateCommand())
        {
            countImages.Transaction = transaction;
            countImages.CommandText = "SELECT COUNT(*) FROM images WHERE scan_root = $root";
            countImages.Parameters.AddWithValue("$root", resolvedRoot);

            using var countSearch = connection.CreateCommand();
            countSearch.Transaction = transaction;
            countSearch.CommandText = "SELECT COUNT(*) FROM image_search_fts WHERE scan_root = $root";
            countSearch.Parameters.AddWithValue("$root", resolvedRoot);

            var imageCount = Convert.ToInt32(countImages.ExecuteScalar());
            var searchCount = Convert.ToInt32(countSearch.ExecuteScalar());
            if (imageCount == searchCount)
            {
                return;
            }
        }

        var rows = new List<(string Id, string SearchText)>();
        using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = """
                SELECT id, filename, folder, absolute_path
                FROM images
                WHERE scan_root = $root
                """;
            select.Parameters.AddWithValue("$root", resolvedRoot);
            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                rows.Add((
                    reader.GetString(0),
                    $"{reader.GetString(1)} {reader.GetString(2)} {reader.GetString(3)}"));
            }
        }

        ExecuteNonQuery(connection, transaction, "DELETE FROM image_search_fts WHERE scan_root = $root", ("$root", resolvedRoot));

        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO image_search_fts(image_id, scan_root, search_text)
            VALUES ($id, $root, $text)
            """;
        foreach (var row in rows)
        {
            insert.Parameters.Clear();
            insert.Parameters.AddWithValue("$id", row.Id);
            insert.Parameters.AddWithValue("$root", resolvedRoot);
            insert.Parameters.AddWithValue("$text", row.SearchText);
            insert.ExecuteNonQuery();
        }
    }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string type)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table})";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type}";
        alter.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    private static string? GetSetting(SqliteConnection connection, SqliteTransaction? transaction, string key)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT value FROM native_settings WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private static void UpsertSetting(SqliteConnection connection, SqliteTransaction transaction, string key, string value, DateTime updatedAt)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO native_settings(key, value, updated_at_utc)
            VALUES ($key, $value, $updated)
            ON CONFLICT(key) DO UPDATE SET
              value = excluded.value,
              updated_at_utc = excluded.updated_at_utc
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updated", updatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    private const string DefaultKeyBindingsJson = """
        {"next":"Right","previous":"Left","favoriteUp":"Ctrl+Up","favoriteDown":"Ctrl+Down","delete":"Delete","openFile":"Enter","openFolder":"Ctrl+Enter","openDetail":"Ctrl+M","toggleView":"Ctrl+G","togglePreview":"Ctrl+P","toggleDetails":"Ctrl+D","reshuffleSort":"Ctrl+R","detailNext":"Right","detailPrevious":"Left","detailZoomIn":"+","detailZoomOut":"-","detailReset":"0","detailFlip":"F","detailPan":"mouse-drag-or-scrollbars","detailOpenExternal":"Enter","detailFavoriteUp":"Ctrl+Up","detailFavoriteDown":"Ctrl+Down"}
        """;
}
