namespace MusicBeePlugin.DiscordTools
{
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Net.Http;

  internal static class AssetManager
  {
    public const string ASSET_LOGO = "logo";
    public const string ASSET_PLAY = "play";
    public const string ASSET_PAUSE = "pause";
    public const string ASSET_STOP = "stop";
    public const string BASE_LASTFM_API = "http://ws.audioscrobbler.com/2.0";
    public const string LASTFM_API_KEY = "81626a336b15457291e55044f80b7f3b";

    private static string cacheFilePath;
    private static DateTime lastApiCall;
    private readonly static HttpClient httpClient;
    private static Dictionary<string, string> albumUrlPairs;

    static AssetManager()
    {
      httpClient = new HttpClient();
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
      string hash = artist + ":" + album;
      if (albumUrlPairs.TryGetValue(hash, out string url))
        return url;
      else
      {
        GetLastFMAlbumInfo(artist, album, a => CacheAssetUrl(a, hash));
        return null;
      }
    }

    private static void CacheAssetUrl(JObject albInfo, string hash)
    {
      if (albInfo.ContainsKey("error"))
        return;

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
      if (!albumUrlPairs.ContainsKey(hash))
        albumUrlPairs.Add(hash, finalUrl);

      if (finalUrl != null)
        Plugin.Instance._discordClient.SetPresence(Plugin.Instance._discordClient.discordPresence);
    }

    private static async void GetLastFMAlbumInfo(string artist, string album, Action<JObject> callback)
    {
      double msSinceLastCall = (DateTime.Now - lastApiCall).TotalMilliseconds;
      if (msSinceLastCall < 500)
        await System.Threading.Tasks.Task.Delay(1000);

      string url = $"{BASE_LASTFM_API}/?method=album.getinfo&api_key={LASTFM_API_KEY}&artist={Uri.EscapeDataString(artist)}&album={Uri.EscapeDataString(album)}&format=json";
      string response = await httpClient.GetStringAsync(url);
      lastApiCall = DateTime.Now;
      callback(JObject.Parse(response));
    }

    public static void Shutdown()
    {
      httpClient.Dispose();
      File.WriteAllText(cacheFilePath, JsonConvert.SerializeObject(albumUrlPairs));
    }
  }
}
