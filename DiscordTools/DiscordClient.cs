namespace MusicBeePlugin.DiscordTools
{
  using DiscordRPC;
  using DiscordRPC.Logging;
  using DiscordRPC.Message;
  using System;
  using System.Diagnostics;
  using System.Text;
  using System.Threading.Tasks;

  public class DiscordClient
  {
    public RichPresence discordPresence;

    private DiscordRpcClient _discordClient;
    private LevelDbReader _levelDbReader = new LevelDbReader();
    private string _discordId;
    private bool _artworkUploadEnabled;

    public string DiscordId
    {
      get => _discordId;
      set
      {
        if (value != _discordId && !String.IsNullOrWhiteSpace(value))
        {
          _discordId = value;
          Init();
        }
      }
    }

    private bool _isConnected;
    public bool IsConnected
    {
      get => _isConnected;
      private set
      {
        if (value != _isConnected)
        {
          _isConnected = value;
          if (!value && _discordClient?.IsDisposed == false)
          {
            // _isConnected set from true to false and _discordClient is not null and not disposed
            Console.WriteLine("Closing client connection...", "DiscordBee");
            try
            {
              _discordClient.ClearPresence();
            }
            catch (ObjectDisposedException)
            {
              // connection was null, just continue
            }
            finally
            {
              _discordClient.Dispose();
              AssetManager.Shutdown();
            }
          }
        }
      }
    }

    public bool ArtworkUploadEnabled
    {
      get => _artworkUploadEnabled;
      set
      {
        if (value != _artworkUploadEnabled)
        {
          _artworkUploadEnabled = value;
          if (value && IsConnected)
          {
            Init();
          }
          else if (!value)
          {
          }
        }
      }
    }

    private void Init()
    {
      Console.WriteLine("Initialising new DiscordClient instance...", "DiscordBee");
      initDiscordClient();
    }

    private void initDiscordClient()
    {
      // Make sure we are clean
      IsConnected = false;
      _discordClient = new DiscordRpcClient(DiscordId, logger: new DebugLogger(LogLevel.Trace));
      _discordClient.OnError += ErrorCallback;
      _discordClient.OnClose += DisconnectedCallback;
      _discordClient.OnReady += ReadyCallback;
      _discordClient.OnConnectionFailed += ConnectionFailedCallback;
      _discordClient.ShutdownOnly = true;
      _discordClient.SkipIdenticalPresence = true;
      _discordClient.Initialize();
    }

    public void Close()
    {
      IsConnected = false;
    }

    public void SetPresence(RichPresence desired)
    {
      discordPresence = desired.Clone();

      if (Plugin.Instance.settings.DisplayArtwork)
      {
        string artist = Plugin.Instance.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.AlbumArtist);
        string album = Plugin.Instance.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.Album);
        string assetUrl = AssetManager.GetCachedAssetUrl(artist, album);

        if (assetUrl == null)
          assetUrl = AssetManager.ASSET_LOGO;

        discordPresence.Assets.LargeImageKey = assetUrl;
      }

      // do preprocessing here
      if (IsConnected)
      {
        UpdatePresence();
      }
    }

    public void ClearPresence()
    {
      if (IsConnected)
      {
        discordPresence = null;
        _discordClient.ClearPresence();
      }
    }

    private void EnsureInit()
    {
      if (!IsConnected && DiscordId != null && (_discordClient?.IsDisposed ?? true))
      {
        // _discordClient is either null or disposed
        Init();
      }
    }

    private void UpdatePresence()
    {
      EnsureInit();
      Console.WriteLine($"Sending Presence update ...", "DiscordBee");

      _discordClient.SetPresence(discordPresence);
    }

    private void ConnectionFailedCallback(object sender, ConnectionFailedMessage args)
    {
      if (IsConnected)
      {
        IsConnected = false;
      }
    }

    private void ReadyCallback(object sender, ReadyMessage args)
    {
      Console.WriteLine($"Ready. Connected to Discord Client with User: {args.User.Username}", "DiscordRpc");
      IsConnected = true;
      if (discordPresence != null)
      {
        UpdatePresence();
      }
    }

    private void ErrorCallback(object sender, ErrorMessage e)
    {
      Console.WriteLine($"DiscordRpc: ERROR ({e.Code})", e.Message);
      if (e.Code == ErrorCode.PipeException || e.Code == ErrorCode.UnkownError)
      {
        IsConnected = false;
      }
    }

    private void DisconnectedCallback(object sender, CloseMessage c)
    {
      Console.WriteLine("DiscordRpc: Disconnected ({0}) - {1}", c.Code, c.Reason);
      IsConnected = false;
    }
  }

  public class DebugLogger : ILogger
  {
    public LogLevel Level { get; set; }

    public DebugLogger(LogLevel level)
    {
      Level = level;
    }

    public void Error(string message, params object[] args)
    {
      if (Level > LogLevel.Error)
      {
        return;
      }

      Log(message, args);
    }

    public void Info(string message, params object[] args)
    {
      if (Level > LogLevel.Info)
      {
        return;
      }

      Log(message, args);
    }

    public void Trace(string message, params object[] args)
    {
      if (Level > LogLevel.Trace)
      {
        return;
      }

      Log(message, args);
    }

    public void Warning(string message, params object[] args)
    {
      if (Level > LogLevel.Warning)
      {
        return;
      }

      Log(message, args);
    }

    private void Log(string msg, params object[] args)
    {
      Console.WriteLine("" + Level.ToString() + ": " + msg, args);
    }
  }
}
