namespace MusicBeePlugin.DiscordTools
{
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;
  using System.Security.Cryptography;
  using System.Collections.Generic;
  using System.Text;
  using System.IO;
  using System.Net;

  internal static class AssetManager
  {
    public const string ASSET_LOGO = "logo";
    public const string ASSET_PLAY = "play";
    public const string ASSET_PAUSE = "pause";
    public const string ASSET_STOP = "stop";
    public const string BASE_LASTFM_API = "http://ws.audioscrobbler.com/2.0";
    public const string LASTFM_API_KEY = "81626a336b15457291e55044f80b7f3b";

    private static string cacheFilePath;
    private readonly static WebClient webClient;
    private static Dictionary<string, string> albumUrlPairs;

    static AssetManager()
    {
      webClient = new WebClient();
    }

    public static void SetCachePath(string path)
    {
      cacheFilePath = path;
      if (File.Exists(cacheFilePath))
        albumUrlPairs = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(cacheFilePath));
      else
        albumUrlPairs = new Dictionary<string, string>();
    }

    public static string GetCachedAssetUrl(string artist, string album)
    {
      // hashing prevents any possible character collisions in the stored json
      string hash = GetHash(artist, album);
      if (albumUrlPairs.TryGetValue(hash, out string url))
      {
        return url;
      }
      else
      {
        System.Diagnostics.Debug.WriteLine($"DiscordBee: Adding {hash} for {artist} : {album}");
        JObject albInfo = GetLastFMAlbumInfo(artist, album);
        if (albInfo.ContainsKey("error"))
          return null;

        string finalUrl = null;
        JToken imageArray = albInfo["album"]["image"];
        string extraLargeUrl = imageArray[3]["#text"].ToString();

        if (!string.IsNullOrEmpty(extraLargeUrl))
          finalUrl = extraLargeUrl;
        else
        {
          foreach (JToken size in imageArray)
          {
            string sizeUrl = size["#text"].ToString();
            if (!string.IsNullOrWhiteSpace(sizeUrl))
            {
              finalUrl = sizeUrl;
              break;
            }
            else
              continue;
          }
        }

        // we're also adding nulls here so we arent searching for them every time and we know to ignore them
        albumUrlPairs.Add(hash, finalUrl);
      }

      return null;
    }

    private static string GetHash(string artist, string album)
    {
      using (SHA1Managed sha = new SHA1Managed())
      {
        string hashedArtist = GenerateHash(sha, artist);
        string hashedAlbum = GenerateHash(sha, album);
        return hashedArtist + ":" + hashedAlbum;
      }

      string GenerateHash(HashAlgorithm hashAlgorithm, string input)
      {
        byte[] data = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sBuilder = new StringBuilder();

        for (int i = 0; i < data.Length; i++)
          sBuilder.Append(data[i].ToString("x2"));

        return sBuilder.ToString();
      }
    }

    private static JObject GetLastFMAlbumInfo(string artist, string album)
    {
      // TODO: swap with async HttpClient
      string url = $"{BASE_LASTFM_API}/?method=album.getinfo&api_key={LASTFM_API_KEY}&artist={artist}&album={album}&format=json";
      string response = webClient.DownloadString(url);
      return JObject.Parse(response);
    }

    public static void Shutdown()
    {
      webClient.Dispose();
      File.WriteAllText(cacheFilePath, JsonConvert.SerializeObject(albumUrlPairs));
    }
  }
}
