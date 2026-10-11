namespace MIN.Helpers.Contracts.Helpers
{
    /// <summary>
    /// Помошник в получении папки, где хранятся общие данные
    /// </summary>
    public static class SharedDirectoryProvider
    {
        /// <summary>
        /// Получить путь к папке, где хранятся общие данные
        /// </summary>
        public static string GetSharedDirectory()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MIN", "Shared");
    }
}
