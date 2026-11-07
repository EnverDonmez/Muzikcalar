using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SadeSes
{
    public static class Thumbnails
    {
        static readonly SemaphoreSlim limit = new SemaphoreSlim(4, 4);
        public static string PathFor(Track track) { return Path.Combine(AppPaths.Cache, "covers", track.Id + ".jpg"); }
        public static async Task<string> Get(Track track, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); string path = PathFor(track);
            if (File.Exists(path) && new FileInfo(path).Length > 100) return path;
            await limit.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 100) return path;
                // A stable YouTube thumbnail URL keeps old saved queue entries compatible.
                string url = "https://i.ytimg.com/vi/" + track.Id + "/mqdefault.jpg";
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(8));
                    using (var response = await Network.Client.GetAsync(url, timeout.Token).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        if (bytes.Length > 2 * 1024 * 1024) throw new InvalidDataException("Thumbnail too large");
                        using (var stream = new MemoryStream(bytes)) using (var image = Image.FromStream(stream)) { if (image.Width < 100) throw new InvalidDataException("Thumbnail unavailable"); }
                        token.ThrowIfCancellationRequested();
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        // Parallel requests for the selected song may finish together.
                        try { File.WriteAllBytes(path, bytes); } catch (IOException) { if (!File.Exists(path)) throw; }
                    }
                }
                return path;
            }
            finally { limit.Release(); }
        }
    }
}
