using BarrageGrab.Entity.Enums;
using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BarrageGrab.Websocket
{
    /// <summary>
    /// local websocket server
    /// </summary>
    internal class LocalWebSocketServer : IDisposable
    {
        #region 属性&字段

        /// <summary>
        /// WebSocket实例
        /// </summary>
        private WebSocketServer? socketServer = null;

        /// <summary>
        /// 连接的客户端
        /// </summary>
        private Dictionary<string, IWebSocketConnection>? clientList;

        /// <summary>
        /// 要移除的客户端列表
        /// </summary>
        private List<string>? removeList;

        private static readonly object GrabControlLock = new();

        #endregion


        #region public void Run()
        public void Start()
        {
            try
            {
                if (socketServer == null)
                {
                    socketServer = new WebSocketServer(GlobalConfigs.LocalWebSocketServer_Location);
                }

                //restart
                socketServer.RestartAfterListenError = true;
                socketServer.Start(ListenWebSocketConnection);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Local webSocket server fail to start：" + ex.Message);
            }
        }
        #endregion

        #region public void ReStart()
        public void ReStart()
        {
            if (socketServer != null)
            {
                socketServer.Dispose();
                socketServer = null;
            }

            this.Start();
        }
        #endregion


        #region private void ListenWebSocketConnection(IWebSocketConnection client)
        private void ListenWebSocketConnection(IWebSocketConnection client)
        {
            string clientId = client.ConnectionInfo.Id.ToString();


            #region OnOpen
            client.OnOpen = () =>
            {
                if (clientList == null || clientList.Count == 0)
                {
                    clientList = new Dictionary<string, IWebSocketConnection>();
                }

                if (!clientList.ContainsKey(clientId))
                {
                    clientList.Add(clientId, client);
                }
            };
            #endregion


            #region OnMessage
            client.OnMessage = (message) =>
            {
                TryHandleClientControlMessage(client, message);
            };
            #endregion


            #region OnClose
            client.OnClose = () =>
            {
                if (clientList != null && clientList.Count > 0)
                {
                    clientList.Remove(clientId);
                }
            };
            #endregion


            #region OnPing
            client.OnPing = (data) =>
            {

            };
            #endregion
        }
        #endregion

        #region 客户端控制：平台 + 直播间

        private static void TryHandleClientControlMessage(IWebSocketConnection client, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var trimmed = message.TrimStart();
            if (!trimmed.StartsWith('{'))
            {
                return;
            }

            JObject? jo;
            try
            {
                jo = JObject.Parse(message);
            }
            catch (JsonException)
            {
                return;
            }

            var cmd = jo["cmd"]?.ToString()?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(cmd))
            {
                return;
            }

            if (cmd is "grabconfig" or "grab_config" or "config")
            {
                HandleGrabConfig(client, jo);
                return;
            }

            if (cmd is "grabstop" or "grab_stop" or "stop")
            {
                HandleGrabStop(client);
            }
        }

        private static void HandleGrabStop(IWebSocketConnection client)
        {
            var mw = ApplicationRuntime.MainWindow;

            void DoStop()
            {
                lock (GrabControlLock)
                {
                    ApplicationRuntime.BarrageGrabService?.Stop();
                    ApplicationRuntime.LivePlatform = null;
                    mw?.StopGrabInteractive();
                    _ = SendGrabAck(client, true, "stopped");
                }
            }

            if (mw != null && !mw.IsDisposed)
            {
                if (mw.InvokeRequired)
                {
                    mw.Invoke(DoStop);
                }
                else
                {
                    DoStop();
                }
            }
            else
            {
                lock (GrabControlLock)
                {
                    ApplicationRuntime.BarrageGrabService?.Stop();
                    ApplicationRuntime.LivePlatform = null;
                    _ = SendGrabAck(client, true, "stopped");
                }
            }
        }

        private static void HandleGrabConfig(IWebSocketConnection client, JObject jo)
        {
            var liveRaw = jo["liveId"]?.ToString() ?? jo["live_id"]?.ToString() ?? string.Empty;
            var liveId = NormalizeLiveId(liveRaw);
            var platformToken = jo["platform"]?.ToString();

            if (string.IsNullOrWhiteSpace(liveId))
            {
                _ = SendGrabAck(client, false, "liveId is required");
                return;
            }

            var platform = TryParsePlatform(platformToken) ?? PlatformTypeEnum.Douyin;

            if (platform != PlatformTypeEnum.Douyin)
            {
                _ = SendGrabAck(client, false, $"platform not supported: {platform}");
                return;
            }

            var display = string.IsNullOrWhiteSpace(liveRaw.Trim()) ? liveId : liveRaw.Trim();
            var mw = ApplicationRuntime.MainWindow;

            void DoApply()
            {
                lock (GrabControlLock)
                {
                    try
                    {
                        if (mw != null && !mw.IsDisposed)
                        {
                            // 在 UI 线程：同步 LiveId/平台；若已在抓取则先停再起，与主窗体按钮逻辑一致
                            mw.ApplyRemoteGrabConfigFromClient(liveId, display, platform);
                        }
                        else
                        {
                            ApplicationRuntime.BarrageGrabService?.Stop();
                            ApplicationRuntime.BarrageGrabService?.Start(liveId);
                            ApplicationRuntime.LivePlatform = platform;
                        }

                        Debug.WriteLine($"[BarrageGrab] grab start (client) platform={platform} liveId={liveId}");
                        _ = SendGrabAck(client, true, "started");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[BarrageGrab] grab start failed: " + ex.Message);
                        _ = SendGrabAck(client, false, ex.Message);
                    }
                }
            }

            if (mw != null && !mw.IsDisposed)
            {
                if (mw.InvokeRequired)
                {
                    mw.Invoke(DoApply);
                }
                else
                {
                    DoApply();
                }
            }
            else
            {
                DoApply();
            }
        }

        private static string NormalizeLiveId(string raw)
        {
            var s = raw.Trim();
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
            {
                return s;
            }

            var last = uri.Segments.LastOrDefault()?.Trim('/');
            return string.IsNullOrEmpty(last) ? s : last;
        }

        private static PlatformTypeEnum? TryParsePlatform(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var s = token.Trim().ToLowerInvariant();
            if (s is "douyin" or "dy" or "抖音")
            {
                return PlatformTypeEnum.Douyin;
            }

            if (int.TryParse(s, out var n) && Enum.IsDefined(typeof(PlatformTypeEnum), n))
            {
                return (PlatformTypeEnum)n;
            }

            return null;
        }

        private static async Task SendGrabAck(IWebSocketConnection client, bool ok, string message)
        {
            try
            {
                if (!client.IsAvailable)
                {
                    return;
                }

                var payload = JsonConvert.SerializeObject(new
                {
                    cmd = "grabAck",
                    ok,
                    message,
                });
                await client.Send(payload);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[BarrageGrab] grabAck send failed: " + ex.Message);
            }
        }

        #endregion


        #region public void Broadcast(string message)
        /// <summary>
        /// Broadcast
        /// </summary>
        /// <param name="message"></param>
        public async Task Broadcast(string message)
        {
            //Broadcast to all clients
            if (clientList == null || clientList.Count == 0)
            {
                return;
            }

            removeList = new List<string>();

            foreach (var client in clientList)
            {
                if (client.Value.IsAvailable)
                {
                    await client.Value.Send(message);
                }
                else
                {
                    removeList.Add(client.Key);
                }
            }

            if (removeList != null && removeList.Count > 0)
            {
                removeList.ForEach(clientId =>
                {
                    clientList.Remove(clientId);
                });
            }
        }

        #endregion


        public void Dispose()
        {
            if (socketServer != null)
            {
                socketServer.Dispose();
                socketServer = null;
            }

            clientList = null;

            removeList = null;
        }
    }
}
