using BarrageGrab.Entity.Enums;
using BarrageGrab.Framework;
using Google.Protobuf.WellKnownTypes;

namespace BarrageGrab
{
    public partial class MainWindow : Form
    {
        #region 属性&字段

        /// <summary>
        /// 打印的行数
        /// </summary>
        static int printCount = 0;

        #endregion


        public MainWindow()
        {
            ApplicationRuntime.MainWindow = this;

            InitializeComponent();
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
            this.Text = $"抖音快手Tiktok视频号WSS弹幕助手({GlobalConfigs.Version}) by 吴所畏惧 VX：xhhdqq";

            this.lblLocalWebSocket_Location.Text = GlobalConfigs.LocalWebSocketServer_Location;

            #region Platform
            var platformList = new List<KeyValuePair<string, int>>();
            platformList.Add(new KeyValuePair<string, int>("抖音", 1));

            #endregion


        }

        public void PrintConsole(string message)
        {
            this.Invoke(new Action(() =>
            {
                this.txtConsole.AppendText(message + "\r\n");
                this.txtConsole.ScrollToCaret();

                if (++printCount > 10000)
                {
                    printCount = 0;
                    this.txtConsole.Clear();
                }
            }));

        }

        private void MainWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }

        private void btnReBoot_LocalWebSocket_Click(object sender, EventArgs e)
        {
            ApplicationRuntime.LocalWebSocketServer?.ReStart();
        }

        private void btnGrab_Click(object sender, EventArgs e)
        {
            if (!IsGrabRunning())
            {
                string liveUrl = this.txtLiveUrl.Text.Trim();
                if (string.IsNullOrEmpty(liveUrl))
                {
                    MessageBox.Show("LiveId不能为空!", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                StartGrabInteractive(liveUrl);
            }
            else
            {
                StopGrabInteractive();
            }
        }

        /// <summary>
        /// 当前是否处于抓取中（与「开始/停止」按钮状态一致）。
        /// </summary>
        internal bool IsGrabRunning()
        {
            return "stop".Equals(this.btnGrab.Tag?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        internal void StopGrabInteractive()
        {
            ApplicationRuntime.BarrageGrabService?.Stop();
            this.txtLiveUrl.Enabled = true;
            this.btnGrab.Text = "开始";
            this.btnGrab.Tag = "Start";
        }

        internal void StartGrabInteractive(string liveIdOrUrl)
        {
            ApplicationRuntime.BarrageGrabService?.Start(liveIdOrUrl);
            this.txtLiveUrl.Enabled = false;
            this.btnGrab.Text = "停止";
            this.btnGrab.Tag = "Stop";
        }

        /// <summary>
        /// Client 经 WebSocket 下发抓取配置：同步主窗体平台、LiveId，若已在抓取则先停再起。
        /// </summary>
        internal void ApplyRemoteGrabConfigFromClient(string liveIdForService, string liveDisplayText, PlatformTypeEnum platform)
        {
            SelectPlatformRadio(platform);
            this.txtLiveUrl.Text = string.IsNullOrWhiteSpace(liveDisplayText) ? liveIdForService : liveDisplayText.Trim();

            if (IsGrabRunning())
            {
                StopGrabInteractive();
            }

            StartGrabInteractive(liveIdForService);
            ApplicationRuntime.LivePlatform = platform;
        }

        private void SelectPlatformRadio(PlatformTypeEnum platform)
        {
            foreach (var rb in new[] { radio_douyin, radio_kuaishou, radio_bilibili, radio_douyu, radio_acfun, radio_tiktok, radio_huya })
            {
                rb.Checked = false;
            }

            switch (platform)
            {
                case PlatformTypeEnum.Douyin:
                    radio_douyin.Checked = true;
                    break;
                case PlatformTypeEnum.Kuaishou:
                    radio_kuaishou.Checked = true;
                    break;
                case PlatformTypeEnum.Bilibili:
                    radio_bilibili.Checked = true;
                    break;
                case PlatformTypeEnum.Tiktok:
                    radio_tiktok.Checked = true;
                    break;
                default:
                    radio_douyin.Checked = true;
                    break;
            }
        }


        private void tsbtnAbout_Click(object sender, EventArgs e)
        {
            MessageBox.Show("本程序只用作学习交流，请勿用作非法用途。如有违背，责任自行承担。\r\nThis program is only for learning and communication purposes, please do not use it for illegal purposes. If there is any violation, the responsibility shall be borne by oneself.", "警告/Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
