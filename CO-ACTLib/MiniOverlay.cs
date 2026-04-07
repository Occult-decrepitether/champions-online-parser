using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;
using System.Xml;
using Advanced_Combat_Tracker;

namespace Parsing_Plugin
{
    public class MiniOverlay : Form
    {
        private Timer updateTimer;

        private Panel panelTopRow;
        private Panel panelDPS;
        private Panel panelHPS;
        private Label lblDPSHeader;
        private Label lblHPSHeader;
        private Label lblDPSContent;
        private Label lblHPSContent;
        private Label lblDPSEncTitle;
        private Label lblHPSEncTitle;

        private Panel panelBottomRow;
        private Panel panelMyDPS;
        private Panel panelMyHPS;
        private Label lblMyDPSHeader;
        private Label lblMyHPSHeader;
        private Label lblMyDPSContent;
        private Label lblMyHPSContent;

        private Panel dividerHorizontal;

        private Panel panelMend;
        private Label lblMendHeader;
        private Label lblMendContent;
        private Label lblMendEmoticon;
        private Panel mendEmoticonWrapper;
        private Panel dividerMend;

        private Panel panelDTPS;
        private Label lblDTPSHeader;
        private Label lblDTPSContent;
        private Label lblDTPSEncTitle;

        private Panel panelMyDTPS;
        private Label lblMyDTPSHeader;
        private Label lblMyDTPSContent;
        private Panel panelDTPSHandleSingle;
        private TextBox txtDTPSHandle;
        public string DTPSHandle { get; set; }
        private const string PLACEHOLDER_DTPS = "name or @handle...";

        private Panel panelButtonBar;
        private Label lblHandleTracking;

        private Panel panelHandleBar;
        private Panel panelDPSHandleSingle;
        private Panel panelHPSHandleSingle;
        private TextBox txtDPSHandle;
        private TextBox txtHPSHandle;

        private int currentOpacityPct = 99;
        private bool isDragging = false;
        private Point dragOffset;

        private enum LayoutMode { Main, Vertical, Horizontal }
        private LayoutMode currentLayout = LayoutMode.Main;
        private string lastPresetName = "";

        private class TileInfo
        {
            public string Name;
            public Panel Panel;
            public bool Visible;
            public TileInfo(string name, Panel panel) { Name = name; Panel = panel; Visible = true; }
        }
        private List<TileInfo> tileOrder;
        private Form tilesPopup;

        private List<ChildOverlay> childOverlays = new List<ChildOverlay>();
        private Form addTilesPopup;

        public string DPSHandle { get; set; }
        public string HPSHandle { get; set; }

        public void SetHandle(string handle)
        {
            SetDPSHandle(handle);
            SetHPSHandle(handle);
        }
        private const string PLACEHOLDER_DPS = "name or @handle...";
        private const string PLACEHOLDER_HPS = "name or @handle...";
        private static readonly Color PLACEHOLDER_COLOR = Color.FromArgb(120, 120, 120);

        private void SetPlaceholder(TextBox txt, string text)
        {
            txt.Text = text;
            txt.ForeColor = PLACEHOLDER_COLOR;
            txt.Font = new Font("Consolas", 8F, FontStyle.Italic);
        }

        public void SetDPSHandle(string handle)
        {
            DPSHandle = handle;
            if (txtDPSHandle == null) return;
            if (!string.IsNullOrEmpty(handle))
            {
                txtDPSHandle.Text = handle;
                txtDPSHandle.ForeColor = TEXT_COLOR;
                txtDPSHandle.Font = new Font("Consolas", 8F, FontStyle.Regular);
            }
            else
            {
                SetPlaceholder(txtDPSHandle, PLACEHOLDER_DPS);
            }
        }
        public void SetHPSHandle(string handle)
        {
            HPSHandle = handle;
            if (txtHPSHandle == null) return;
            if (!string.IsNullOrEmpty(handle))
            {
                txtHPSHandle.Text = handle;
                txtHPSHandle.ForeColor = TEXT_COLOR;
                txtHPSHandle.Font = new Font("Consolas", 8F, FontStyle.Regular);
            }
            else
            {
                SetPlaceholder(txtHPSHandle, PLACEHOLDER_HPS);
            }
        }
        public void SetDTPSHandle(string handle)
        {
            DTPSHandle = handle;
            if (txtDTPSHandle == null) return;
            if (!string.IsNullOrEmpty(handle))
            {
                txtDTPSHandle.Text = handle;
                txtDTPSHandle.ForeColor = TEXT_COLOR;
                txtDTPSHandle.Font = new Font("Consolas", 8F, FontStyle.Regular);
            }
            else
            {
                SetPlaceholder(txtDTPSHandle, PLACEHOLDER_DTPS);
            }
        }

        private const int PANEL_WIDTH = 220;
        private const int HEADER_HEIGHT = 20;
        private const int DIVIDER_WIDTH = 2;
        private const int UPDATE_INTERVAL_MS = 1000;

        private const string CURRENT_VERSION = "3.1.6";
        private const string UPDATE_API_URL = "https://api.github.com/repos/Occult-decrepitether/champions-online-parser/releases/latest";
        private bool updateAvailable = false;
        private string latestVersion = "";
        private string latestDownloadUrl = "";
        private Timer pulseTimer;
        private float pulsePhase = 0f;
        private Button btnUpdateRef = null;

        private int maxRowsDPS = 0;
        private int maxRowsHPS = 0;
        private int maxRowsDTPS = 0;
        private int maxRowsTrackedDPS = 0;
        private int maxRowsTrackedHPS = 0;
        private int maxRowsTrackedDTPS = 0;
        private const string PLACEHOLDER_ROWS = "type 0 to show all";

        private static readonly Color HEADER_BG = Color.FromArgb(255, 50, 50, 50);
        private static readonly Color DIVIDER_COLOR = Color.FromArgb(255, 70, 70, 70);
        private static readonly Color TEXT_COLOR = Color.FromArgb(255, 230, 230, 230);
        private static readonly Color DPS_ACCENT = Color.FromArgb(255, 220, 60, 60);
        private static readonly Color HPS_ACCENT = Color.FromArgb(255, 80, 200, 80);
        private static readonly Color ENC_TITLE_COLOR = Color.FromArgb(255, 160, 160, 160);
        private static readonly Color MY_DPS_ACCENT = Color.FromArgb(255, 255, 100, 100);
        private static readonly Color MY_HPS_ACCENT = Color.FromArgb(255, 120, 230, 120);
        private static readonly Color DTPS_ACCENT = Color.FromArgb(255, 80, 220, 220);
        private static readonly Color MY_DTPS_ACCENT = Color.FromArgb(255, 120, 240, 240);

        public MiniOverlay()
        {
            InitializeOverlay();
            SetupTimer();
            CheckForUpdate();
        }

        private void InitializeOverlay()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(100, 100);
            this.Size = new Size(PANEL_WIDTH * 2 + DIVIDER_WIDTH + 60, 700);
            this.TopMost = true;
            this.ShowInTaskbar = true;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.Padding = new Padding(0, 0, 0, 16);
            this.Opacity = 1.0;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.Text = "CO Mini Parse";

            SetAppUserModelId(this.Handle, "CO.MiniParse.Overlay");

            try
            {
                int iconSize = 256;
                Bitmap bmp = new Bitmap(iconSize, iconSize);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(30, 30, 30));
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    Font iconFont = new Font("Consolas", 36F, FontStyle.Bold);
                    string iconText = ":8~^.)";
                    SizeF textSize = g.MeasureString(iconText, iconFont);
                    float x = (iconSize - textSize.Width) / 2;
                    float y = (iconSize - textSize.Height) / 2;
                    g.DrawString(iconText, iconFont, Brushes.Orange, x, y);
                }
                this.Icon = Icon.FromHandle(bmp.GetHicon());
            }
            catch { }

            panelTopRow = new Panel();
            panelTopRow.Dock = DockStyle.Top;
            panelTopRow.Height = 120;
            panelTopRow.BackColor = Color.Transparent;

            panelDPS = new Panel();
            panelDPS.Dock = DockStyle.Left;
            panelDPS.Width = PANEL_WIDTH;
            panelDPS.BackColor = Color.Transparent;

            lblDPSHeader = CreateHeaderLabel("DPS", DPS_ACCENT);
            lblDPSHeader.Dock = DockStyle.Top;
            lblDPSHeader.Height = HEADER_HEIGHT;
            lblDPSHeader.BackColor = HEADER_BG;

            lblDPSEncTitle = CreateEncTitleLabel();
            lblDPSEncTitle.Dock = DockStyle.Top;
            lblDPSEncTitle.Height = 16;

            lblDPSContent = CreateContentLabel();
            lblDPSContent.Dock = DockStyle.Fill;

            panelDPS.Controls.Add(lblDPSContent);
            panelDPS.Controls.Add(lblDPSEncTitle);
            panelDPS.Controls.Add(lblDPSHeader);

            Panel dividerTop = new Panel();
            dividerTop.Dock = DockStyle.Left;
            dividerTop.Width = DIVIDER_WIDTH;
            dividerTop.BackColor = Color.Transparent;

            panelHPS = new Panel();
            panelHPS.Dock = DockStyle.Fill;
            panelHPS.BackColor = Color.Transparent;

            lblHPSHeader = CreateHeaderLabel("HPS", HPS_ACCENT);
            lblHPSHeader.Dock = DockStyle.Top;
            lblHPSHeader.Height = HEADER_HEIGHT;
            lblHPSHeader.BackColor = HEADER_BG;

            lblHPSEncTitle = CreateEncTitleLabel();
            lblHPSEncTitle.Dock = DockStyle.Top;
            lblHPSEncTitle.Height = 16;

            lblHPSContent = CreateContentLabel();
            lblHPSContent.Dock = DockStyle.Fill;

            panelHPS.Controls.Add(lblHPSContent);
            panelHPS.Controls.Add(lblHPSEncTitle);
            panelHPS.Controls.Add(lblHPSHeader);

            panelTopRow.Controls.Add(panelHPS);
            panelTopRow.Controls.Add(dividerTop);
            panelTopRow.Controls.Add(panelDPS);

            dividerHorizontal = new Panel();
            dividerHorizontal.Dock = DockStyle.Top;
            dividerHorizontal.Height = DIVIDER_WIDTH;
            dividerHorizontal.BackColor = DIVIDER_COLOR;

            panelMend = new Panel();
            panelMend.Dock = DockStyle.Top;
            panelMend.Height = 60;
            panelMend.BackColor = Color.Transparent;

            lblMendHeader = CreateHeaderLabel("Mend Tracker", Color.FromArgb(255, 180, 130, 255));
            lblMendHeader.Dock = DockStyle.Top;
            lblMendHeader.Height = HEADER_HEIGHT;
            lblMendHeader.BackColor = HEADER_BG;

            lblMendContent = CreateContentLabel();
            lblMendContent.Dock = DockStyle.Fill;

            lblMendEmoticon = new Label();
            lblMendEmoticon.Text = "";
            lblMendEmoticon.Font = new Font("Consolas", 18F, FontStyle.Bold);
            lblMendEmoticon.Dock = DockStyle.Bottom;
            lblMendEmoticon.Height = 30;
            lblMendEmoticon.TextAlign = ContentAlignment.MiddleLeft;
            lblMendEmoticon.Padding = new Padding(4, 0, 0, 0);
            lblMendEmoticon.BackColor = Color.FromArgb(30, 30, 30);
            lblMendEmoticon.Visible = false;

            Panel mendEmoticonDivider = new Panel();
            mendEmoticonDivider.Dock = DockStyle.Bottom;
            mendEmoticonDivider.Height = 2;
            mendEmoticonDivider.BackColor = DIVIDER_COLOR;

            mendEmoticonWrapper = new Panel();
            mendEmoticonWrapper.Dock = DockStyle.Bottom;
            mendEmoticonWrapper.Height = 34;
            mendEmoticonWrapper.BackColor = DIVIDER_COLOR;
            mendEmoticonWrapper.Padding = new Padding(2, 0, 2, 2);
            mendEmoticonWrapper.Visible = false;

            lblMendEmoticon.Dock = DockStyle.Fill;
            lblMendEmoticon.Height = 0;
            mendEmoticonWrapper.Controls.Add(lblMendEmoticon);

            panelMend.Controls.Add(lblMendContent);
            panelMend.Controls.Add(mendEmoticonDivider);
            panelMend.Controls.Add(mendEmoticonWrapper);
            panelMend.Controls.Add(lblMendHeader);

            dividerMend = new Panel();
            dividerMend.Dock = DockStyle.Top;
            dividerMend.Height = DIVIDER_WIDTH;
            dividerMend.BackColor = DIVIDER_COLOR;

            panelDTPS = new Panel();
            panelDTPS.Dock = DockStyle.Top;
            panelDTPS.Height = 120;
            panelDTPS.BackColor = Color.Transparent;

            lblDTPSHeader = CreateHeaderLabel("DTPS", DTPS_ACCENT);
            lblDTPSHeader.Dock = DockStyle.Top;
            lblDTPSHeader.Height = HEADER_HEIGHT;
            lblDTPSHeader.BackColor = HEADER_BG;

            lblDTPSEncTitle = CreateEncTitleLabel();
            lblDTPSEncTitle.Dock = DockStyle.Top;
            lblDTPSEncTitle.Height = 16;

            lblDTPSContent = CreateContentLabel();
            lblDTPSContent.Dock = DockStyle.Fill;

            panelDTPS.Controls.Add(lblDTPSContent);
            panelDTPS.Controls.Add(lblDTPSEncTitle);
            panelDTPS.Controls.Add(lblDTPSHeader);

            panelBottomRow = new Panel();
            panelBottomRow.Dock = DockStyle.Fill;
            panelBottomRow.BackColor = Color.Transparent;

            panelMyDPS = new Panel();
            panelMyDPS.Dock = DockStyle.Left;
            panelMyDPS.Width = PANEL_WIDTH;
            panelMyDPS.BackColor = Color.Transparent;

            lblMyDPSHeader = CreateHeaderLabel("Tracked DPS", MY_DPS_ACCENT);
            lblMyDPSHeader.Dock = DockStyle.Top;
            lblMyDPSHeader.Height = HEADER_HEIGHT;
            lblMyDPSHeader.BackColor = HEADER_BG;

            lblMyDPSContent = CreateContentLabel();
            lblMyDPSContent.Dock = DockStyle.Fill;

            panelMyDPS.Controls.Add(lblMyDPSContent);
            panelMyDPS.Controls.Add(lblMyDPSHeader);

            Panel dividerBottom = new Panel();
            dividerBottom.Dock = DockStyle.Left;
            dividerBottom.Width = DIVIDER_WIDTH;
            dividerBottom.BackColor = DIVIDER_COLOR;

            panelMyHPS = new Panel();
            panelMyHPS.Dock = DockStyle.Fill;
            panelMyHPS.BackColor = Color.Transparent;

            lblMyHPSHeader = CreateHeaderLabel("Tracked HPS", MY_HPS_ACCENT);
            lblMyHPSHeader.Dock = DockStyle.Top;
            lblMyHPSHeader.Height = HEADER_HEIGHT;
            lblMyHPSHeader.BackColor = HEADER_BG;

            lblMyHPSContent = CreateContentLabel();
            lblMyHPSContent.Dock = DockStyle.Fill;

            panelMyHPS.Controls.Add(lblMyHPSContent);
            panelMyHPS.Controls.Add(lblMyHPSHeader);

            panelBottomRow.Controls.Add(panelMyHPS);
            panelBottomRow.Controls.Add(dividerBottom);
            panelBottomRow.Controls.Add(panelMyDPS);

            panelMyDTPS = new Panel();
            panelMyDTPS.Dock = DockStyle.Top;
            panelMyDTPS.Height = 120;
            panelMyDTPS.BackColor = Color.Transparent;

            lblMyDTPSHeader = CreateHeaderLabel("Tracked DTPS", MY_DTPS_ACCENT);
            lblMyDTPSHeader.Dock = DockStyle.Top;
            lblMyDTPSHeader.Height = HEADER_HEIGHT;
            lblMyDTPSHeader.BackColor = HEADER_BG;

            lblMyDTPSContent = CreateContentLabel();
            lblMyDTPSContent.Dock = DockStyle.Fill;

            panelMyDTPS.Controls.Add(lblMyDTPSContent);
            panelMyDTPS.Controls.Add(lblMyDTPSHeader);

            panelDTPSHandleSingle = new Panel();
            panelDTPSHandleSingle.Dock = DockStyle.Top;
            panelDTPSHandleSingle.Height = 22;
            panelDTPSHandleSingle.BackColor = HEADER_BG;
            panelDTPSHandleSingle.Padding = new Padding(2);

            txtDTPSHandle = new TextBox();
            txtDTPSHandle.BorderStyle = BorderStyle.FixedSingle;
            txtDTPSHandle.BackColor = Color.FromArgb(40, 40, 40);
            txtDTPSHandle.Font = new Font("Consolas", 8F, FontStyle.Italic);
            txtDTPSHandle.ForeColor = PLACEHOLDER_COLOR;
            txtDTPSHandle.Text = PLACEHOLDER_DTPS;
            txtDTPSHandle.Dock = DockStyle.Fill;
            txtDTPSHandle.Enter += (s, ev) =>
            {
                if (txtDTPSHandle.Text == PLACEHOLDER_DTPS)
                {
                    txtDTPSHandle.Text = "";
                    txtDTPSHandle.ForeColor = TEXT_COLOR;
                    txtDTPSHandle.Font = new Font("Consolas", 8F, FontStyle.Regular);
                }
            };
            txtDTPSHandle.Leave += (s, ev) =>
            {
                if (string.IsNullOrEmpty(txtDTPSHandle.Text))
                {
                    SetPlaceholder(txtDTPSHandle, PLACEHOLDER_DTPS);
                }
            };
            txtDTPSHandle.TextChanged += (s, ev) =>
            {
                if (txtDTPSHandle.Text != PLACEHOLDER_DTPS)
                    DTPSHandle = txtDTPSHandle.Text;
            };
            panelDTPSHandleSingle.Controls.Add(txtDTPSHandle);

            panelButtonBar = new Panel();
            panelButtonBar.Dock = DockStyle.Top;
            panelButtonBar.Height = 28;
            panelButtonBar.BackColor = HEADER_BG;

            Button btnMinimize = new Button();
            btnMinimize.Text = "\u2500";
            btnMinimize.Dock = DockStyle.Right;
            btnMinimize.FlatStyle = FlatStyle.Flat;
            btnMinimize.FlatAppearance.BorderColor = DIVIDER_COLOR;
            btnMinimize.FlatAppearance.BorderSize = 1;
            btnMinimize.BackColor = Color.FromArgb(60, 60, 60);
            btnMinimize.ForeColor = TEXT_COLOR;
            btnMinimize.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            btnMinimize.Width = 26;
            btnMinimize.TextAlign = ContentAlignment.MiddleCenter;
            btnMinimize.TabStop = false;
            btnMinimize.Cursor = Cursors.Hand;
            btnMinimize.Click += (s, ev) => { this.WindowState = FormWindowState.Minimized; };

            Button btnMaximize = new Button();
            btnMaximize.Text = "\u25A1";
            btnMaximize.Dock = DockStyle.Right;
            btnMaximize.FlatStyle = FlatStyle.Flat;
            btnMaximize.FlatAppearance.BorderColor = DIVIDER_COLOR;
            btnMaximize.FlatAppearance.BorderSize = 1;
            btnMaximize.BackColor = Color.FromArgb(60, 60, 60);
            btnMaximize.ForeColor = TEXT_COLOR;
            btnMaximize.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            btnMaximize.Width = 26;
            btnMaximize.TextAlign = ContentAlignment.MiddleCenter;
            btnMaximize.TabStop = false;
            btnMaximize.Cursor = Cursors.Hand;
            Rectangle preMaxBounds = Rectangle.Empty;
            bool isMaxed = false;
            btnMaximize.Click += (s, ev) =>
            {
                if (isMaxed)
                {
                    this.Bounds = preMaxBounds;
                    isMaxed = false;
                }
                else
                {
                    preMaxBounds = this.Bounds;
                    Screen scr = Screen.FromControl(this);
                    this.Bounds = scr.WorkingArea;
                    isMaxed = true;
                }
                try
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        ApplyLayout(currentLayout);
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            DoMainLayout();
                            panelButtonBar.Invalidate(true);
                            this.Invalidate(true);
                            this.Refresh();
                        });
                    });
                }
                catch { }
            };

            Button btnClose = new Button();
            btnClose.Text = "\u2715";
            btnClose.Dock = DockStyle.Right;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderColor = Color.FromArgb(255, 200, 80, 80);
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.BackColor = Color.FromArgb(60, 60, 60);
            btnClose.ForeColor = Color.FromArgb(255, 200, 80, 80);
            btnClose.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnClose.Width = 26;
            btnClose.TextAlign = ContentAlignment.MiddleCenter;
            btnClose.TabStop = false;
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, ev) => this.Hide();

            Button btnReset = CreateBarButton("Reset Parser", 90, Color.FromArgb(255, 180, 50, 50));
            btnReset.Click += (s, ev) => { try { ActGlobals.oFormActMain.EndCombat(true); } catch { } };

            Button btnLayout = CreateBarButton("Layout", 70, Color.FromArgb(255, 220, 150, 50));
            btnLayout.Click += (s, ev) =>
            {
                if (currentLayout == LayoutMode.Main) ApplyLayout(LayoutMode.Vertical);
                else if (currentLayout == LayoutMode.Vertical) ApplyLayout(LayoutMode.Horizontal);
                else ApplyLayout(LayoutMode.Main);
            };

            tileOrder = new List<TileInfo>
            {
                new TileInfo("DPS", panelDPS),
                new TileInfo("HPS", panelHPS),
                new TileInfo("DTPS", panelDTPS),
                new TileInfo("Mend Tracker", panelMend),
                new TileInfo("Tracked DPS", panelMyDPS),
                new TileInfo("Tracked HPS", panelMyHPS),
                new TileInfo("Tracked DTPS", panelMyDTPS)
            };

            Button btnTiles = CreateBarButton("Tiles", 55, Color.FromArgb(255, 100, 180, 220));
            btnTiles.Click += (s, ev) => ShowTilesPopup(btnTiles);

            Button btnOpacity = CreateBarButton("Opacity", 70, Color.FromArgb(255, 80, 200, 80));
            btnOpacity.Click += (s, ev) => ShowOpacityPopup(btnOpacity);

            Button btnTextSize = CreateBarButton("Text Size", 70, Color.FromArgb(255, 180, 130, 255));
            btnTextSize.Click += (s, ev) => ShowTextSizePopup(btnTextSize);

            Button btnSettings = new Button();
            btnSettings.Text = "\u2699";
            btnSettings.Dock = DockStyle.Right;
            btnSettings.FlatStyle = FlatStyle.Flat;
            btnSettings.FlatAppearance.BorderColor = DIVIDER_COLOR;
            btnSettings.FlatAppearance.BorderSize = 1;
            btnSettings.BackColor = Color.FromArgb(60, 60, 60);
            btnSettings.ForeColor = TEXT_COLOR;
            btnSettings.Font = new Font("Segoe UI", 12F, FontStyle.Regular);
            btnSettings.Width = 30;
            btnSettings.TextAlign = ContentAlignment.MiddleCenter;
            btnSettings.TabStop = false;
            btnSettings.Cursor = Cursors.Hand;
            btnSettings.Click += (s, ev) =>
            {
                ShowSettingsPopup(btnSettings, btnReset, btnLayout, btnTiles, btnOpacity, btnTextSize);
            };

            btnReset.Dock = DockStyle.Right; btnReset.Width = 90;
            btnMinimize.Dock = DockStyle.Right; btnMinimize.Width = 26;
            btnMaximize.Dock = DockStyle.Right; btnMaximize.Width = 26;
            btnClose.Dock = DockStyle.Right; btnClose.Width = 26;

            Label lblEmoticon = new Label();
            lblEmoticon.Text = ":8~^.()";
            lblEmoticon.ForeColor = Color.FromArgb(255, 220, 150, 50);
            lblEmoticon.Font = new Font("Consolas", 9F, FontStyle.Bold);
            lblEmoticon.Dock = DockStyle.Fill;
            lblEmoticon.TextAlign = ContentAlignment.MiddleLeft;
            lblEmoticon.Padding = new Padding(4, 0, 0, 0);
            WireDragEvents(lblEmoticon);

            panelButtonBar.Controls.Add(btnReset);
            panelButtonBar.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 2, BackColor = HEADER_BG });
            panelButtonBar.Controls.Add(btnSettings);
            panelButtonBar.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 2, BackColor = HEADER_BG });
            panelButtonBar.Controls.Add(btnMinimize);
            panelButtonBar.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 2, BackColor = HEADER_BG });
            panelButtonBar.Controls.Add(btnMaximize);
            panelButtonBar.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 2, BackColor = HEADER_BG });
            panelButtonBar.Controls.Add(btnClose);
            panelButtonBar.Controls.Add(lblEmoticon);

            this.MinimumSize = new Size(115, 200);

            lblHandleTracking = new Label();
            lblHandleTracking.Text = "Handle Tracking";
            lblHandleTracking.Dock = DockStyle.Top;
            lblHandleTracking.Height = HEADER_HEIGHT;
            lblHandleTracking.ForeColor = Color.FromArgb(255, 200, 200, 255);
            lblHandleTracking.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblHandleTracking.TextAlign = ContentAlignment.MiddleCenter;
            lblHandleTracking.BackColor = HEADER_BG;
            WireDragEvents(lblHandleTracking);

            txtDPSHandle = new TextBox();
            txtDPSHandle.BackColor = Color.FromArgb(30, 30, 30);
            txtDPSHandle.BorderStyle = BorderStyle.None;
            txtDPSHandle.TabStop = false;
            txtDPSHandle.Dock = DockStyle.Fill;
            SetPlaceholder(txtDPSHandle, PLACEHOLDER_DPS);
            txtDPSHandle.Enter += (s, ev) =>
            {
                if (txtDPSHandle.Text == PLACEHOLDER_DPS)
                {
                    txtDPSHandle.Text = "";
                    txtDPSHandle.ForeColor = TEXT_COLOR;
                    txtDPSHandle.Font = new Font("Consolas", 8F, FontStyle.Regular);
                }
            };
            txtDPSHandle.Leave += (s, ev) =>
            {
                if (string.IsNullOrEmpty(txtDPSHandle.Text))
                    SetPlaceholder(txtDPSHandle, PLACEHOLDER_DPS);
                this.ActiveControl = null;
            };
            txtDPSHandle.TextChanged += (s, ev) =>
            {
                if (txtDPSHandle.Text != PLACEHOLDER_DPS)
                    DPSHandle = txtDPSHandle.Text;
            };

            txtHPSHandle = new TextBox();
            txtHPSHandle.BackColor = Color.FromArgb(30, 30, 30);
            txtHPSHandle.BorderStyle = BorderStyle.None;
            txtHPSHandle.TabStop = false;
            txtHPSHandle.Dock = DockStyle.Fill;
            SetPlaceholder(txtHPSHandle, PLACEHOLDER_HPS);
            txtHPSHandle.Enter += (s, ev) =>
            {
                if (txtHPSHandle.Text == PLACEHOLDER_HPS)
                {
                    txtHPSHandle.Text = "";
                    txtHPSHandle.ForeColor = TEXT_COLOR;
                    txtHPSHandle.Font = new Font("Consolas", 8F, FontStyle.Regular);
                }
            };
            txtHPSHandle.Leave += (s, ev) =>
            {
                if (string.IsNullOrEmpty(txtHPSHandle.Text))
                    SetPlaceholder(txtHPSHandle, PLACEHOLDER_HPS);
                this.ActiveControl = null;
            };
            txtHPSHandle.TextChanged += (s, ev) =>
            {
                if (txtHPSHandle.Text != PLACEHOLDER_HPS)
                    HPSHandle = txtHPSHandle.Text;
            };

            panelHandleBar = new Panel();
            panelHandleBar.Dock = DockStyle.Top;
            panelHandleBar.Height = 26;
            panelHandleBar.BackColor = Color.FromArgb(40, 40, 40);
            panelHandleBar.Padding = new Padding(2, 2, 2, 2);

            panelDPSHandleSingle = new Panel();
            panelDPSHandleSingle.Dock = DockStyle.Top;
            panelDPSHandleSingle.Height = 24;
            panelDPSHandleSingle.BackColor = Color.FromArgb(100, 100, 100);
            panelDPSHandleSingle.Padding = new Padding(1, 1, 1, 1);

            panelHPSHandleSingle = new Panel();
            panelHPSHandleSingle.Dock = DockStyle.Top;
            panelHPSHandleSingle.Height = 24;
            panelHPSHandleSingle.BackColor = Color.FromArgb(100, 100, 100);
            panelHPSHandleSingle.Padding = new Padding(1, 1, 1, 1);

            ApplyLayout(LayoutMode.Main);
            LoadSettings();

            WireDragEvents(this);
            WireDragEvents(panelTopRow);
            WireDragEvents(panelBottomRow);
            WireDragEvents(panelDPS);
            WireDragEvents(panelHPS);
            WireDragEvents(panelMyDPS);
            WireDragEvents(panelMyHPS);
            WireDragEvents(dividerTop);
            WireDragEvents(dividerBottom);
            WireDragEvents(dividerHorizontal);
            WireDragEvents(lblDPSHeader);
            WireDragEvents(lblHPSHeader);
            WireDragEvents(lblDPSContent);
            WireDragEvents(lblHPSContent);
            WireDragEvents(lblDPSEncTitle);
            WireDragEvents(lblHPSEncTitle);
            WireDragEvents(lblMyDPSHeader);
            WireDragEvents(lblMyHPSHeader);
            WireDragEvents(lblMyDPSContent);
            WireDragEvents(lblMyHPSContent);
            WireDragEvents(panelMend);
            WireDragEvents(lblMendHeader);
            WireDragEvents(lblMendContent);
            WireDragEvents(dividerMend);
            WireDragEvents(panelDTPS);
            WireDragEvents(lblDTPSHeader);
            WireDragEvents(lblDTPSContent);
            WireDragEvents(lblDTPSEncTitle);
            WireDragEvents(panelMyDTPS);
            WireDragEvents(lblMyDTPSHeader);
            WireDragEvents(lblMyDTPSContent);
            WireDragEvents(panelButtonBar);
            WireDragEvents(panelHandleBar);
        }

        private Label CreateHeaderLabel(string text, Color accentColor)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.ForeColor = accentColor;
            lbl.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lbl.TextAlign = ContentAlignment.MiddleCenter;
            lbl.Padding = new Padding(0);
            return lbl;
        }

        private Label CreateEncTitleLabel()
        {
            Label lbl = new Label();
            lbl.Text = "";
            lbl.ForeColor = ENC_TITLE_COLOR;
            lbl.Font = new Font("Segoe UI", 7F, FontStyle.Italic);
            lbl.TextAlign = ContentAlignment.MiddleCenter;
            lbl.BackColor = Color.FromArgb(255, 40, 40, 40);
            return lbl;
        }

        private Label CreateContentLabel()
        {
            MultiColorLabel lbl = new MultiColorLabel();
            lbl.Text = "";
            lbl.ForeColor = TEXT_COLOR;
            lbl.Font = new Font("Consolas", 8.5F, FontStyle.Regular);
            lbl.TextAlign = ContentAlignment.TopLeft;
            lbl.Padding = new Padding(4, 2, 4, 2);
            lbl.AutoSize = false;
            return lbl;
        }

        internal class MultiColorLabel : Label
        {
            public MultiColorLabel()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                DoubleBuffered = true;
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                if (Parent != null) InvokePaintBackground(Parent, e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (string.IsNullOrEmpty(Text)) return;

                string[] lines = Text.Split('\n');
                int yPos = Padding.Top;
                int lineH = TextRenderer.MeasureText("X", Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Height;
                Color othersColor = Color.FromArgb(255, 255, 220, 80);

                foreach (string line in lines)
                {
                    string l = line.TrimEnd('\r');
                    Color c = ForeColor;
                    if (l.TrimStart().StartsWith("Others"))
                        c = othersColor;
                    TextRenderer.DrawText(e.Graphics, l, Font, new Point(Padding.Left, yPos), c, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                    yPos += lineH;
                }
            }
        }

        private void WireDragEvents(Control ctrl)
        {
            ctrl.MouseDown += Overlay_MouseDown;
            ctrl.MouseMove += Overlay_MouseMove;
            ctrl.MouseUp += Overlay_MouseUp;
        }

        private bool IsInResizeZone(Point formPoint)
        {
            return formPoint.X >= this.ClientSize.Width - RESIZE_BORDER &&
                   formPoint.Y >= this.ClientSize.Height - RESIZE_BORDER;
        }

        private void Overlay_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (sender != txtDPSHandle && sender != txtHPSHandle)
                    this.ActiveControl = null;

                Point formPoint = this.PointToClient(Control.MousePosition);
                if (IsInResizeZone(formPoint))
                {
                    const int WM_NCLBUTTONDOWN = 0xA1;
                    int hitResult = GetHitResult(formPoint);
                    if (hitResult != 0)
                    {
                        ReleaseCapture();
                        SendMessage(this.Handle, WM_NCLBUTTONDOWN, hitResult, 0);
                        return;
                    }
                }
                isDragging = true;
                dragOffset = formPoint;
            }
        }

        private int GetHitResult(Point pos)
        {
            if (pos.X >= this.ClientSize.Width - RESIZE_BORDER && pos.Y >= this.ClientSize.Height - RESIZE_BORDER)
                return HTBOTTOMRIGHT;
            return 0;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private void SetOpacitySafe(double opacity)
        {
            if (opacity < 0.4) opacity = 0.4;
            if (opacity > 1.0) opacity = 1.0;
            currentOpacityPct = (int)(opacity * 100);
            this.Opacity = opacity;
            foreach (ChildOverlay c in childOverlays)
                c.Opacity = opacity;
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        internal static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid,
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Interface)] out object propertyStore);

        [System.Runtime.InteropServices.ComImport]
        [System.Runtime.InteropServices.Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PropertyKey pkey);
            void GetValue(ref PropertyKey key, out PropVariant pv);
            void SetValue(ref PropertyKey key, ref PropVariant pv);
            void Commit();
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
        internal struct PropertyKey
        {
            public Guid fmtid;
            public uint pid;
            public PropertyKey(Guid fmtid, uint pid) { this.fmtid = fmtid; this.pid = pid; }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct PropVariant
        {
            public ushort vt;
            public ushort wReserved1, wReserved2, wReserved3;
            public IntPtr p;
            public PropVariant(string value)
            {
                vt = 31; // VT_LPWSTR
                wReserved1 = wReserved2 = wReserved3 = 0;
                p = System.Runtime.InteropServices.Marshal.StringToCoTaskMemUni(value);
            }
        }

        internal static void SetAppUserModelId(IntPtr hwnd, string appId)
        {
            try
            {
                Guid IID_IPropertyStore = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
                object store;
                SHGetPropertyStoreForWindow(hwnd, ref IID_IPropertyStore, out store);
                if (store != null)
                {
                    PropVariant pv = new PropVariant(appId);
                    PropertyKey pk = new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
                    ((IPropertyStore)store).SetValue(ref pk, ref pv);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(store);
                }
            }
            catch { }
        }

        private void Overlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point cursor = Control.MousePosition;
                this.Location = new Point(cursor.X - dragOffset.X, cursor.Y - dragOffset.Y);
            }
        }

        private void Overlay_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                isDragging = false;
        }

        private void SetupTimer()
        {
            updateTimer = new Timer();
            updateTimer.Interval = UPDATE_INTERVAL_MS;
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                UpdateOverlayData();
            }
            catch { }
        }

        private static void SetText(Label lbl, string text)
        {
            if (lbl.Text != text) lbl.Text = text;
        }

        public void EnsureOnScreen()
        {
            Point center = new Point(this.Left + this.Width / 2, this.Top + this.Height / 2);
            bool onScreen = false;
            foreach (Screen s in Screen.AllScreens)
            {
                if (s.WorkingArea.Contains(center)) { onScreen = true; break; }
            }
            if (!onScreen) this.Location = new Point(100, 100);
        }

        public void CheckForUpdate()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "CO-ACTLib-UpdateCheck");
                        string json = wc.DownloadString(UPDATE_API_URL);
                        string tag = ExtractJsonValue(json, "tag_name");
                        string url = ExtractJsonValue(json, "browser_download_url");
                        if (string.IsNullOrEmpty(tag)) return;
                        string remote = tag.TrimStart('v', 'V');
                        if (CompareVersions(remote, CURRENT_VERSION) > 0)
                        {
                            latestVersion = remote;
                            latestDownloadUrl = url;
                            updateAvailable = true;
                            try { this.BeginInvoke((MethodInvoker)delegate { StartPulse(); }); } catch { }
                        }
                    }
                }
                catch { }
            });
        }

        private static string ExtractJsonValue(string json, string key)
        {
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search);
            if (idx < 0) return "";
            int colon = json.IndexOf(':', idx);
            if (colon < 0) return "";
            int q1 = json.IndexOf('"', colon);
            if (q1 < 0) return "";
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        private static int CompareVersions(string a, string b)
        {
            string[] aParts = a.Split('.');
            string[] bParts = b.Split('.');
            int len = Math.Max(aParts.Length, bParts.Length);
            for (int i = 0; i < len; i++)
            {
                int ai = 0, bi = 0;
                if (i < aParts.Length) int.TryParse(aParts[i], out ai);
                if (i < bParts.Length) int.TryParse(bParts[i], out bi);
                if (ai != bi) return ai.CompareTo(bi);
            }
            return 0;
        }

        private void StartPulse()
        {
            if (pulseTimer != null) return;
            pulseTimer = new Timer();
            pulseTimer.Interval = 50;
            pulseTimer.Tick += (s, ev) =>
            {
                pulsePhase += 0.15f;
                if (pulsePhase > Math.PI * 2) pulsePhase -= (float)(Math.PI * 2);
                if (btnUpdateRef != null && !btnUpdateRef.IsDisposed)
                {
                    int intensity = (int)(127 + 128 * Math.Sin(pulsePhase));
                    if (intensity < 0) intensity = 0;
                    if (intensity > 255) intensity = 255;
                    btnUpdateRef.FlatAppearance.BorderColor = Color.FromArgb(255, 0, intensity, 0);
                }
            };
            pulseTimer.Start();
        }

        private const string CANONICAL_DLL_NAME = "CO-ACTLib64v2.dll";

        private void InstallUpdate()
        {
            try
            {
                if (string.IsNullOrEmpty(latestDownloadUrl)) return;

                string currentDll = ResolveLoadedPluginPath();
                if (string.IsNullOrEmpty(currentDll))
                {
                    MessageBox.Show("Update failed: could not locate the running plugin DLL on disk.", "CO-ACTLib Update", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Always write the new file as CO-ACTLib64v2.dll, even if the user is
                // running an older build with a different filename (e.g. CO-ACTLib64.dll).
                // This standardizes the filename across all installs going forward.
                string pluginDir = Path.GetDirectoryName(currentDll);
                string targetDll = Path.Combine(pluginDir, CANONICAL_DLL_NAME);
                bool renamed = !string.Equals(Path.GetFileName(currentDll), CANONICAL_DLL_NAME, StringComparison.OrdinalIgnoreCase);

                string tempDll = targetDll + ".new";
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "CO-ACTLib-Update");
                    wc.DownloadFile(latestDownloadUrl, tempDll);
                }

                FileInfo fi = new FileInfo(tempDll);
                if (!fi.Exists || fi.Length < 1024)
                {
                    try { if (File.Exists(tempDll)) File.Delete(tempDll); } catch { }
                    MessageBox.Show("Update failed: downloaded file is missing or too small.", "CO-ACTLib Update", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (File.Exists(targetDll)) File.Delete(targetDll);
                File.Move(tempDll, targetDll);

                // If the user was running an older filename, remove the old DLL so they
                // don't end up with two copies sitting in the Plugins folder.
                string oldFileMsg = "";
                if (renamed)
                {
                    try
                    {
                        if (File.Exists(currentDll)) File.Delete(currentDll);
                        oldFileMsg = "\n\nThe plugin filename has been updated from " + Path.GetFileName(currentDll) + " to " + CANONICAL_DLL_NAME + ". After restarting ACT, you may need to enable the new plugin entry in the Plugins tab.";
                    }
                    catch
                    {
                        oldFileMsg = "\n\nNote: the old file " + Path.GetFileName(currentDll) + " could not be removed automatically (it may be locked). Please delete it manually after closing ACT, then re-enable " + CANONICAL_DLL_NAME + " in the Plugins tab.";
                    }
                }

                MessageBox.Show("Update downloaded to:\n" + targetDll + "\n\nClose ACT completely and restart it to apply the update." + oldFileMsg, "CO-ACTLib Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update failed: " + ex.Message, "CO-ACTLib Update", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string ResolveLoadedPluginPath()
        {
            // Most reliable: ask the running assembly where it lives on disk.
            try
            {
                string asmPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(asmPath) && File.Exists(asmPath))
                    return asmPath;
            }
            catch { }

            // Fallback: walk ACT's plugin list, but only consider entries that are
            // actually loaded AND whose file still exists on disk. This avoids picking
            // up stale registrations from previous installs with different filenames.
            try
            {
                Type myType = this.GetType();
                foreach (ActPluginData plugin in ActGlobals.oFormActMain.ActPlugins)
                {
                    if (plugin.pluginFile == null) continue;
                    if (!plugin.pluginFile.Exists) continue;
                    if (plugin.pluginObj == null) continue;
                    if (plugin.pluginObj.GetType().Assembly == myType.Assembly)
                        return plugin.pluginFile.FullName;
                }
                // Last-ditch: any enabled CO-ACTLib entry whose file exists.
                foreach (ActPluginData plugin in ActGlobals.oFormActMain.ActPlugins)
                {
                    if (plugin.pluginFile == null) continue;
                    if (!plugin.pluginFile.Exists) continue;
                    if (plugin.pluginFile.Name.IndexOf("CO-ACTLib", StringComparison.OrdinalIgnoreCase) >= 0)
                        return plugin.pluginFile.FullName;
                }
            }
            catch { }

            return "";
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (this.Visible) EnsureOnScreen();
        }

        private void UpdateOverlayData()
        {
            if (!this.Visible) return;
            this.SuspendLayout();

            EncounterData encounter = GetCurrentEncounter();
            if (encounter == null)
            {
                if (string.IsNullOrEmpty(lblDPSContent.Text) || lblDPSContent.Text == "  No encounter data")
                {
                    SetText(lblDPSEncTitle, "");
                    SetText(lblHPSEncTitle, "");
                    SetText(lblDTPSEncTitle, "");
                    SetText(lblDPSContent, "  No encounter data");
                    SetText(lblHPSContent, "  No encounter data");
                    SetText(lblDTPSContent, "  No encounter data");
                    SetText(lblMyDPSContent, "  No data");
                    SetText(lblMyHPSContent, "  No data");
                    SetText(lblMyDTPSContent, "  No data");
                    SetText(lblMendContent, "  No Mend detected");
                    if (lblMendEmoticon != null) { lblMendEmoticon.Visible = false; if (mendEmoticonWrapper != null) mendEmoticonWrapper.Visible = false; }
                }
                this.ResumeLayout();
                return;
            }

            string encTitle = encounter.Title;
            string duration = encounter.DurationS;
            string titleText = encTitle + " (" + duration + ")";
            SetText(lblDPSEncTitle, titleText);
            SetText(lblHPSEncTitle, titleText);
            SetText(lblDTPSEncTitle, titleText);

            List<CombatantData> combatants = new List<CombatantData>();
            try
            {
                foreach (CombatantData cd in encounter.Items.Values)
                {
                    combatants.Add(cd);
                }
            }
            catch { }

            List<CombatantData> dpsSorted = new List<CombatantData>(combatants);
            dpsSorted.Sort((a, b) => b.Damage.CompareTo(a.Damage));

            List<CombatantData> hpsSorted = new List<CombatantData>(combatants);
            hpsSorted.Sort((a, b) => b.Healed.CompareTo(a.Healed));

            double encDur = encounter.Duration.TotalSeconds;
            string dpsText = FormatCombatantList(dpsSorted, CombatantTileMode.DPS, encDur);
            string hpsText = FormatCombatantList(hpsSorted, CombatantTileMode.HPS, encDur);
            string dtpsText = FormatCombatantList(combatants, CombatantTileMode.DTPS, encDur);
            SetText(lblDPSContent, dpsText);
            SetText(lblHPSContent, hpsText);
            SetText(lblDTPSContent, dtpsText);

            CombatantData dpsChar = FindMyCharacter(combatants, DPSHandle);
            if (dpsChar != null)
            {
                SetText(lblMyDPSHeader, dpsChar.Name.Trim() + "'s Tracked DPS");
                SetText(lblMyDPSContent, FormatPowerBreakdown(dpsChar, true, encounter, lblMyDPSContent.Width));
            }
            else
            {
                SetText(lblMyDPSHeader, "Tracked DPS");
                string hint = string.IsNullOrEmpty(DPSHandle) ? " (set DPS handle above)" : "";
                SetText(lblMyDPSContent, "  No char found" + hint);
            }

            CombatantData hpsChar = FindMyCharacter(combatants, HPSHandle);
            if (hpsChar != null)
            {
                SetText(lblMyHPSHeader, hpsChar.Name.Trim() + "'s Tracked HPS");
                SetText(lblMyHPSContent, FormatPowerBreakdown(hpsChar, false, encounter, lblMyHPSContent.Width));
            }
            else
            {
                SetText(lblMyHPSHeader, "Tracked HPS");
                string hint = string.IsNullOrEmpty(HPSHandle) ? " (set HPS handle above)" : "";
                SetText(lblMyHPSContent, "  No char found" + hint);
            }

            CombatantData dtpsChar = FindMyCharacter(combatants, DTPSHandle);
            if (dtpsChar != null)
            {
                SetText(lblMyDTPSHeader, dtpsChar.Name.Trim() + "'s Tracked DTPS");
                SetText(lblMyDTPSContent, FormatPowerBreakdown(dtpsChar, PowerBreakdownMode.DTPS, encounter, lblMyDTPSContent.Width, maxRowsTrackedDTPS));
            }
            else
            {
                SetText(lblMyDTPSHeader, "Tracked DTPS");
                string hint = string.IsNullOrEmpty(DTPSHandle) ? " (set DTPS handle above)" : "";
                SetText(lblMyDTPSContent, "  No char found" + hint);
            }

            UpdateMendTracker(encounter);
            AutoFitAllFonts();
            this.ResumeLayout();
        }

        private void UpdateMendTracker(EncounterData encounter)
        {
            Dictionary<string, COParser.MendData> mendData = COParser.GetMendData();
            if (mendData.Count == 0)
            {
                SetText(lblMendContent, "  No Mend detected");
                if (lblMendEmoticon != null) { lblMendEmoticon.Visible = false; if (mendEmoticonWrapper != null) mendEmoticonWrapper.Visible = false; }
                return;
            }

            double encDuration = encounter.Duration.TotalSeconds;
            if (encDuration < 1) encDuration = 1;

            List<COParser.MendData> sorted = new List<COParser.MendData>(mendData.Values);
            sorted.Sort((a, b) => b.TotalHealed.CompareTo(a.TotalHealed));

            int longestName = 4;
            foreach (COParser.MendData md in sorted)
            {
                string n = md.OwnerName.Trim();
                if (n.Length > longestName) longestName = n.Length;
            }
            int nameW = Math.Min(longestName, 20);

            int wHps = 6, wMax = 6, wMin = 6;

            string result = String.Format(" {0,-" + nameW + "}\u2502{1," + wHps + "}\u2502{2," + wMax + "}\u2502{3," + wMin + "}\r\n",
                ClipText("User", nameW), ClipText("HPS", wHps), ClipText("MaxHit", wMax), ClipText("MinHit", wMin));
            result += " " + new string('\u2500', nameW) + "\u253C" + new string('\u2500', wHps) + "\u253C" + new string('\u2500', wMax) + "\u253C" + new string('\u2500', wMin) + "\r\n";

            foreach (COParser.MendData md in sorted)
            {
                string name = ClipText(md.OwnerName.Trim(), nameW);
                double hps = md.TotalHealed / encDuration;
                int minHit = md.MinHit == int.MaxValue ? 0 : md.MinHit;

                result += String.Format(" {0,-" + nameW + "}\u2502{1," + wHps + "}\u2502{2," + wMax + "}\u2502{3," + wMin + "}\r\n",
                    name,
                    ClipText(FormatNumber(hps), wHps),
                    ClipText(FormatNumber(md.MaxHit), wMax),
                    ClipText(FormatNumber(minHit), wMin));
            }

            long totalHealed = 0;
            int totalTicks = 0;
            foreach (COParser.MendData md in sorted)
            {
                totalHealed += md.TotalHealed;
                totalTicks += md.Ticks;
            }
            int overallAvg = totalTicks > 0 ? (int)(totalHealed / totalTicks) : 0;

            SetText(lblMendContent, result);

            if (lblMendEmoticon != null)
            {
                if (overallAvg < 300)
                {
                    lblMendEmoticon.Text = ">8~^./";
                    lblMendEmoticon.ForeColor = Color.FromArgb(255, 255, 60, 60);
                }
                else
                {
                    lblMendEmoticon.Text = ":8~^.)";
                    lblMendEmoticon.ForeColor = Color.FromArgb(255, 60, 255, 60);
                }
                lblMendEmoticon.Visible = true;
                if (mendEmoticonWrapper != null) mendEmoticonWrapper.Visible = true;
            }
        }

        internal CombatantData FindMyCharacter(List<CombatantData> combatants, string handle)
        {
            if (!string.IsNullOrEmpty(handle))
            {
                string query = handle.Trim();
                if (query.StartsWith("@"))
                {
                    string displayName = COParser.GetNameForHandle(query);
                    if (displayName != null)
                    {
                        foreach (CombatantData cd in combatants)
                        {
                            if (cd.Name.Trim().Equals(displayName.Trim(), StringComparison.OrdinalIgnoreCase))
                                return cd;
                        }
                    }
                }
                else
                {
                    foreach (CombatantData cd in combatants)
                    {
                        if (cd.Name.Trim().Equals(query, StringComparison.OrdinalIgnoreCase))
                            return cd;
                    }
                }
            }

            try
            {
                if (!string.IsNullOrEmpty(ActGlobals.charName))
                {
                    foreach (CombatantData cd in combatants)
                    {
                        if (cd.Name.Equals(ActGlobals.charName, StringComparison.OrdinalIgnoreCase))
                            return cd;
                    }
                }
            }
            catch { }

            return null;
        }

        internal enum PowerBreakdownMode { DPS, HPS, DTPS }

        internal string FormatPowerBreakdown(CombatantData myData, bool isDPS, EncounterData encounter, int panelWidth)
        {
            return FormatPowerBreakdown(myData, isDPS ? PowerBreakdownMode.DPS : PowerBreakdownMode.HPS, encounter, panelWidth, isDPS ? maxRowsTrackedDPS : maxRowsTrackedHPS);
        }

        internal string FormatPowerBreakdown(CombatantData myData, bool isDPS, EncounterData encounter, int panelWidth, int rowLimit)
        {
            return FormatPowerBreakdown(myData, isDPS ? PowerBreakdownMode.DPS : PowerBreakdownMode.HPS, encounter, panelWidth, rowLimit);
        }

        internal string FormatPowerBreakdown(CombatantData myData, PowerBreakdownMode mode, EncounterData encounter, int panelWidth, int rowLimit)
        {
            bool isDPS = mode == PowerBreakdownMode.DPS;
            bool isHPS = mode == PowerBreakdownMode.HPS;
            bool isDTPS = mode == PowerBreakdownMode.DTPS;
            string result = "";
            List<PowerEntry> powers = new List<PowerEntry>();
            double encDuration = encounter.Duration.TotalSeconds;
            if (encDuration < 1) encDuration = 1;

            long totalActualDmg = 0;
            long totalBaseDmg = 0;

            try
            {
                string typeKey;
                if (isDPS) typeKey = "Outgoing Damage";
                else if (isHPS) typeKey = "Healing (Out)";
                else typeKey = "Incoming Damage";

                DamageTypeData dtd;
                if (!myData.Items.TryGetValue(typeKey, out dtd))
                    return "  No data";

                List<string> keys;
                try { keys = new List<string>(dtd.Items.Keys); }
                catch { return "  No data"; }

                foreach (string name in keys)
                {
                    if (name == "All") continue;

                    AttackType at;
                    try { if (!dtd.Items.TryGetValue(name, out at)) continue; }
                    catch { continue; }

                    long value = (long)at.Damage;
                    if (value <= 0) continue;

                    int highest = 0;
                    int lowest = int.MaxValue;

                    try
                    {
                        List<MasterSwing> swings = new List<MasterSwing>(at.Items);
                        foreach (MasterSwing ms in swings)
                        {
                            int tick = (int)ms.Damage.Number;
                            if (tick > 0)
                            {
                                if (tick > highest) highest = tick;
                                if (tick < lowest) lowest = tick;
                            }
                            if ((isDPS || isDTPS) && tick > 0)
                            {
                                totalActualDmg += tick;
                                int baseDmg = 0;
                                if (!string.IsNullOrEmpty(ms.Damage.DamageString2))
                                    int.TryParse(ms.Damage.DamageString2, out baseDmg);
                                if (baseDmg <= 0) baseDmg = tick;
                                totalBaseDmg += baseDmg;
                            }
                        }
                    }
                    catch { }
                    if (lowest == int.MaxValue) lowest = 0;

                    string displayName = name;
                    int bracketEnd = name.IndexOf("] ");
                    if (bracketEnd >= 0)
                        displayName = name.Substring(bracketEnd + 2);

                    PowerEntry pe = new PowerEntry();
                    pe.Name = displayName;
                    pe.Value = value;
                    pe.PerSecond = value / encDuration;
                    pe.HighestTick = highest;
                    pe.LowestTick = lowest;
                    powers.Add(pe);
                }
            }
            catch { }

            if (powers.Count == 0)
                return "  No data";

            powers.Sort((a, b) => b.Value.CompareTo(a.Value));

            long total = 0;
            double totalPs = 0;
            foreach (PowerEntry pe in powers)
            {
                total += pe.Value;
                totalPs += pe.PerSecond;
            }
            if (total == 0) total = 1;

            string psLabel = isDPS ? "DPS" : (isHPS ? "HPS" : "DTPS");
            string totalLabel = isDPS ? "Damage" : (isHPS ? "Healed" : "DmgTaken");

            int longestName = 6;
            foreach (PowerEntry pe in powers)
                if (pe.Name.Length > longestName) longestName = pe.Name.Length;
            if ("Total:".Length > longestName) longestName = "Total:".Length;
            int nameWidth = Math.Min(longestName, 20);

            int wPs = 5;
            int wPct = 4;
            int wMax = 6;
            int wLow = 6;
            int wTot = isDTPS ? 8 : 6;

            string hdrPower = ClipText("Power", nameWidth);
            string hdrPs = ClipText(psLabel, wPs);
            string hdrPct = ClipText("%", wPct);
            string hdrMaxHit = ClipText("MaxHit", wMax);
            string hdrLowHit = ClipText("LowHit", wLow);
            string hdrTotal = ClipText(totalLabel, wTot);

            result += String.Format(" {0,-" + nameWidth + "}\u2502{1," + wPs + "}\u2502{2," + wPct + "}\u2502{3," + wMax + "}\u2502{4," + wLow + "}\u2502{5," + wTot + "}\r\n",
                hdrPower, hdrPs, hdrPct, hdrMaxHit, hdrLowHit, hdrTotal);
            result += " " + new string('\u2500', nameWidth) + "\u253C" + new string('\u2500', wPs) + "\u253C" + new string('\u2500', wPct) + "\u253C" + new string('\u2500', wMax) + "\u253C" + new string('\u2500', wLow) + "\u253C" + new string('\u2500', wTot) + "\r\n";

            int hardCap = 50;
            int rowsToShow = (rowLimit > 0) ? rowLimit : hardCap;
            bool useOthers = (rowLimit > 0 && powers.Count > rowLimit);
            int topN = useOthers ? rowLimit - 1 : Math.Min(rowsToShow, powers.Count);
            if (topN < 0) topN = 0;

            int count = 0;
            long othersValue = 0;
            double othersPs = 0;
            foreach (PowerEntry pe in powers)
            {
                if (count < topN)
                {
                    string name = ClipText(pe.Name, nameWidth);
                    double pct = (pe.Value * 100.0) / total;

                    result += String.Format(" {0,-" + nameWidth + "}\u2502{1," + wPs + "}\u2502{2," + wPct + "}\u2502{3," + wMax + "}\u2502{4," + wLow + "}\u2502{5," + wTot + "}\r\n",
                        name,
                        ClipText(FormatNumber(pe.PerSecond), wPs),
                        ClipText(((int)pct).ToString() + "%", wPct),
                        ClipText(FormatNumber(pe.HighestTick), wMax),
                        ClipText(FormatNumber(pe.LowestTick), wLow),
                        ClipText(FormatNumber(pe.Value), wTot));
                }
                else if (useOthers)
                {
                    othersValue += pe.Value;
                    othersPs += pe.PerSecond;
                }
                count++;
            }

            if (useOthers && othersValue > 0)
            {
                double othersPct = (othersValue * 100.0) / total;
                result += String.Format(" {0,-" + nameWidth + "}\u2502{1," + wPs + "}\u2502{2," + wPct + "}\u2502{3," + wMax + "}\u2502{4," + wLow + "}\u2502{5," + wTot + "}\r\n",
                    ClipText("Others", nameWidth),
                    ClipText(FormatNumber(othersPs), wPs),
                    ClipText(((int)othersPct).ToString() + "%", wPct),
                    "",
                    "",
                    ClipText(FormatNumber(othersValue), wTot));
            }

            result += " " + new string('\u2500', nameWidth) + "\u253C" + new string('\u2500', wPs) + "\u253C" + new string('\u2500', wPct) + "\u253C" + new string('\u2500', wMax) + "\u253C" + new string('\u2500', wLow) + "\u253C" + new string('\u2500', wTot) + "\r\n";
            result += String.Format(" {0,-" + nameWidth + "}\u2502{1," + wPs + "}\u2502{2," + wPct + "}\u2502{3," + wMax + "}\u2502{4," + wLow + "}\u2502{5," + wTot + "}\r\n",
                ClipText("Total:", nameWidth),
                ClipText(FormatNumber(totalPs), wPs),
                "100%",
                "",
                "",
                ClipText(FormatNumber(total), wTot));

            if ((isDPS || isDTPS) && totalBaseDmg > 0)
            {
                int sepWidth = nameWidth + wPs + wPct + wMax + wLow + wTot + 5;
                result += " " + new string('\u2500', sepWidth) + "\r\n";
                double bonus = ((double)(totalActualDmg - totalBaseDmg) / totalBaseDmg) * 100.0;
                string sign = bonus >= 0 ? "-" : "+";
                string label;
                if (isDPS) label = "NPC's Resists: ";
                else label = (myData != null ? myData.Name.Trim() : "Your") + "'s Resists: ";
                result += " " + label + sign + Math.Abs(bonus).ToString("F2") + "%\r\n";
            }

            return result;
        }

        private class PowerEntry
        {
            public string Name;
            public long Value;
            public double PerSecond;
            public int HighestTick;
            public int LowestTick;
        }

        private enum CombatantTileMode { DPS, HPS, DTPS }

        private string FormatCombatantList(List<CombatantData> combatants, CombatantTileMode mode, double encDuration)
        {
            if (combatants.Count == 0)
                return "  No data";

            long total = 0;
            foreach (CombatantData cd in combatants)
            {
                if (cd.Name.StartsWith("[")) continue;
                if (mode == CombatantTileMode.DPS)
                    total += cd.Damage;
                else if (mode == CombatantTileMode.HPS)
                {
                    if (cd.Healed > 0) total += cd.Healed;
                }
                else
                {
                    if (cd.DamageTaken > 0) total += cd.DamageTaken;
                }
            }
            if (total == 0) total = 1;

            int wName = 12, wVal = 6, wPct = 4;

            string valLabel = mode == CombatantTileMode.DPS ? "DPS" : (mode == CombatantTileMode.HPS ? "HPS" : "DTPS");
            string result = String.Format(" {0,-" + wName + "}\u2502{1," + wVal + "}\u2502{2," + wPct + "}\r\n",
                ClipText("Name", wName), ClipText(valLabel, wVal), ClipText("%", wPct));
            result += " " + new string('\u2500', wName) + "\u253C" + new string('\u2500', wVal) + "\u253C" + new string('\u2500', wPct) + "\r\n";

            List<CombatantData> filtered = new List<CombatantData>();
            foreach (CombatantData cd in combatants)
            {
                if (cd.Name.StartsWith("[")) continue;
                if (mode == CombatantTileMode.HPS && cd.Healed <= 0) continue;
                if (mode == CombatantTileMode.DTPS && cd.DamageTaken <= 0) continue;
                filtered.Add(cd);
            }

            if (mode == CombatantTileMode.DTPS)
                filtered.Sort((a, b) => b.DamageTaken.CompareTo(a.DamageTaken));

            int rowLimit = mode == CombatantTileMode.DPS ? maxRowsDPS : (mode == CombatantTileMode.HPS ? maxRowsHPS : maxRowsDTPS);
            int hardCap = 50;
            bool useOthers = (rowLimit > 0 && filtered.Count > rowLimit);
            int topN = useOthers ? rowLimit - 1 : (rowLimit > 0 ? Math.Min(rowLimit, filtered.Count) : Math.Min(hardCap, filtered.Count));
            if (topN < 0) topN = 0;

            int count = 0;
            long othersValue = 0;
            double othersRate = 0;
            foreach (CombatantData cd in filtered)
            {
                if (count < topN)
                {
                    string name = ClipText(cd.Name, wName);
                    if (mode == CombatantTileMode.DPS)
                    {
                        double dps = cd.EncDPS;
                        double pct = (cd.Damage * 100.0) / total;
                        result += String.Format(" {0,-" + wName + "}\u2502{1," + wVal + "}\u2502{2," + (wPct - 1) + ":0}%\r\n",
                            name, ClipText(FormatNumber(dps), wVal), pct);
                    }
                    else if (mode == CombatantTileMode.HPS)
                    {
                        long healed = cd.Healed;
                        double hps = cd.EncHPS;
                        double pct = (healed * 100.0) / total;
                        result += String.Format(" {0,-" + wName + "}\u2502{1," + wVal + "}\u2502{2," + (wPct - 1) + ":0}%\r\n",
                            name, ClipText(FormatNumber(hps), wVal), pct);
                    }
                    else
                    {
                        long taken = cd.DamageTaken;
                        double dur = encDuration > 0 ? encDuration : cd.Duration.TotalSeconds;
                        if (dur < 1) dur = 1;
                        double dtps = taken / dur;
                        double pct = (taken * 100.0) / total;
                        result += String.Format(" {0,-" + wName + "}\u2502{1," + wVal + "}\u2502{2," + (wPct - 1) + ":0}%\r\n",
                            name, ClipText(FormatNumber(dtps), wVal), pct);
                    }
                }
                else if (useOthers)
                {
                    if (mode == CombatantTileMode.DPS) { othersValue += cd.Damage; othersRate += cd.EncDPS; }
                    else if (mode == CombatantTileMode.HPS) { othersValue += cd.Healed; othersRate += cd.EncHPS; }
                    else
                    {
                        double odur = encDuration > 0 ? encDuration : cd.Duration.TotalSeconds;
                        if (odur < 1) odur = 1;
                        othersValue += cd.DamageTaken;
                        othersRate += cd.DamageTaken / odur;
                    }
                }
                count++;
            }

            if (useOthers && othersValue > 0)
            {
                double othersPct = (othersValue * 100.0) / total;
                result += String.Format(" {0,-" + wName + "}\u2502{1," + wVal + "}\u2502{2," + (wPct - 1) + ":0}%\r\n",
                    ClipText("Others", wName), ClipText(FormatNumber(othersRate), wVal), othersPct);
            }

            return result;
        }

        private static string ClipText(string text, int maxWidth)
        {
            if (text.Length <= maxWidth) return text;
            if (maxWidth <= 1) return text.Substring(0, maxWidth);
            return text.Substring(0, maxWidth - 1) + ".";
        }

        private string FormatNumber(double value)
        {
            if (value >= 1000000)
                return (value / 1000000).ToString("0.0") + "M";
            else if (value >= 1000)
                return (value / 1000).ToString("0.0") + "K";
            else
                return value.ToString("0");
        }

        private Button CreateBarButton(string text, int width, Color borderColor)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Dock = DockStyle.Right;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = borderColor;
            btn.FlatAppearance.BorderSize = 1;
            btn.BackColor = Color.FromArgb(60, 60, 60);
            btn.ForeColor = TEXT_COLOR;
            btn.Font = new Font("Segoe UI", 7F, FontStyle.Regular);
            btn.Width = width;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.TabStop = false;
            btn.Cursor = Cursors.Hand;
            btn.Margin = new Padding(2, 0, 0, 0);
            return btn;
        }

        private float[] tileFontSizes = { 48f, 48f, 48f, 48f, 48f, 48f, 48f };

        private void ApplyFontSize(int tileIdx, float size)
        {
            tileFontSizes[tileIdx] = size;
            AutoFitTileFont(tileIdx);
        }

        private void AutoFitTileFont(int tileIdx)
        {
            Label[] contentLabels = { lblDPSContent, lblHPSContent, lblMendContent, lblMyDPSContent, lblMyHPSContent, lblDTPSContent, lblMyDTPSContent };
            Label lbl = contentLabels[tileIdx];
            if (lbl == null || string.IsNullOrEmpty(lbl.Text)) return;

            float maxSize = tileFontSizes[tileIdx];
            int rawWidth = lbl.Width - lbl.Padding.Left - lbl.Padding.Right;
            int availWidth = rawWidth - 20;
            if (availWidth < 20) { lbl.Font = new Font("Consolas", maxSize); return; }

            string[] lines = lbl.Text.Split('\n');
            string longestLine = "";
            foreach (string line in lines)
            {
                if (line.Length > longestLine.Length) longestLine = line;
            }
            if (longestLine.Length == 0) { lbl.Font = new Font("Consolas", maxSize); return; }

            int availHeight = lbl.Height - lbl.Padding.Top - lbl.Padding.Bottom;
            int lineCount = lines.Length;

            float bestSize = 3f;
            try
            {
                for (float sz = maxSize; sz >= 3f; sz -= 0.5f)
                {
                    using (Font f = new Font("Consolas", sz))
                    {
                        int w = TextRenderer.MeasureText(longestLine, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                        if (w > availWidth) continue;
                        int lineH = TextRenderer.MeasureText("X", f).Height;
                        if (lineCount * lineH > availHeight && availHeight > 0) continue;
                        bestSize = sz;
                        break;
                    }
                }
            }
            catch { bestSize = maxSize; }

            if (Math.Abs(lbl.Font.Size - bestSize) > 0.1f)
                lbl.Font = new Font("Consolas", bestSize);
        }

        private void AutoFitAllFonts()
        {
            for (int i = 0; i < 7; i++)
                AutoFitTileFont(i);
        }

        private void ShowTextSizePopup(Control anchor) { ShowTextSizePopup(anchor.PointToScreen(new Point(0, anchor.Height))); }
        private void ShowTextSizePopup(Point location)
        {
            Form popup = new Form();
            popup.FormBorderStyle = FormBorderStyle.None;
            popup.StartPosition = FormStartPosition.Manual;
            popup.BackColor = Color.FromArgb(50, 50, 50);
            popup.TopMost = true;
            popup.ShowInTaskbar = false;
            popup.Location = location;
            popup.Deactivate += (s, ev) => { popup.Close(); };

            string[] names = { "General", "DPS", "HPS", "Mend Tracker", "Tracked DPS", "Tracked HPS" };
            TrackBar[] sliders = new TrackBar[6];
            int rowH = 80;
            int y = 4;

            for (int i = 0; i < 6; i++)
            {
                int idx = i;

                Label lbl = new Label();
                lbl.Text = names[i];
                lbl.ForeColor = TEXT_COLOR;
                lbl.Font = new Font("Segoe UI", 7.5F);
                lbl.Location = new Point(6, y + 8);
                lbl.AutoSize = true;

                TrackBar slider = new TrackBar();
                slider.Minimum = 3;
                slider.Maximum = 30;
                slider.Value = (idx == 0) ? (int)tileFontSizes[0] : (int)tileFontSizes[idx - 1];
                slider.TickStyle = TickStyle.None;
                slider.Location = new Point(100, y);
                slider.Size = new Size(140, rowH);
                slider.BackColor = Color.FromArgb(50, 50, 50);
                sliders[i] = slider;

                if (idx == 0)
                {
                    slider.ValueChanged += (s, ev) =>
                    {
                        float sz = slider.Value;
                        for (int t = 0; t < 5; t++)
                            ApplyFontSize(t, sz);
                        for (int si = 1; si < 6; si++)
                            if (sliders[si].Value != slider.Value)
                                sliders[si].Value = slider.Value;
                    };
                }
                else
                {
                    int tileIdx = idx - 1;
                    slider.ValueChanged += (s, ev) =>
                    {
                        ApplyFontSize(tileIdx, slider.Value);
                    };
                }

                popup.Controls.Add(lbl);
                popup.Controls.Add(slider);
                y += rowH;

                if (i == 0)
                {
                    Panel sep = new Panel();
                    sep.Location = new Point(6, y);
                    sep.Size = new Size(230, 1);
                    sep.BackColor = DIVIDER_COLOR;
                    popup.Controls.Add(sep);
                    y += 5;
                }
            }

            popup.Size = new Size(250, y + 10);
            popup.Show();
        }

        private void ShowSettingsPopup(Control anchor, Button btnReset, Button btnLayout, Button btnTiles, Button btnOpacity, Button btnTextSize)
        {
            Form popup = new Form();
            popup.FormBorderStyle = FormBorderStyle.None;
            popup.StartPosition = FormStartPosition.Manual;
            popup.BackColor = Color.FromArgb(50, 50, 50);
            popup.TopMost = true;
            popup.ShowInTaskbar = false;
            popup.MinimumSize = new Size(1, 1);
            popup.Location = anchor.PointToScreen(new Point(0, anchor.Height));
            bool suppressClose = false;
            popup.Deactivate += (s, ev) => { if (!suppressClose) popup.Close(); };

            Button btnUpdate = CreateSettingsButton("Update", updateAvailable ? Color.FromArgb(255, 0, 200, 0) : Color.FromArgb(255, 90, 90, 90));
            btnUpdateRef = btnUpdate;
            btnUpdate.Click += (s, ev) =>
            {
                suppressClose = true;
                if (updateAvailable) InstallUpdate();
                else MessageBox.Show("You are running the latest version (" + CURRENT_VERSION + ").", "CO-ACTLib", MessageBoxButtons.OK, MessageBoxIcon.Information);
                suppressClose = false;
            };

            Button[] buttons = new Button[] {
                CreateSettingsButton("Layout", Color.FromArgb(255, 220, 150, 50)),
                CreateSettingsButton("Tiles", Color.FromArgb(255, 100, 180, 220)),
                CreateSettingsButton("Opacity", Color.FromArgb(255, 80, 200, 80)),
                CreateSettingsButton("Presets", Color.FromArgb(255, 50, 80, 160)),
                CreateSettingsButton("Add Tiles", Color.FromArgb(255, 220, 180, 50)),
                CreateSettingsButton("Show Rows", Color.FromArgb(255, 200, 120, 200)),
                btnUpdate
            };

            buttons[0].Click += (s, ev) =>
            {
                suppressClose = true;
                Point settingsScreenPos = anchor.PointToScreen(Point.Empty);
                if (currentLayout == LayoutMode.Main) ApplyLayout(LayoutMode.Vertical);
                else if (currentLayout == LayoutMode.Vertical) ApplyLayout(LayoutMode.Horizontal);
                else ApplyLayout(LayoutMode.Main);
                Point newSettingsPos = anchor.PointToScreen(Point.Empty);
                this.Location = new Point(
                    this.Location.X + (settingsScreenPos.X - newSettingsPos.X),
                    this.Location.Y + (settingsScreenPos.Y - newSettingsPos.Y));
                popup.Location = anchor.PointToScreen(new Point(0, anchor.Height));
                Point popLoc2 = popup.Location;
                popLoc2.X = popLoc2.X - popup.Width + anchor.Width;
                popup.Location = popLoc2;
                popup.Show();
                popup.BringToFront();
                suppressClose = false;
            };
            buttons[1].Click += (s, ev) => { Point loc = popup.Location; popup.Close(); ShowTilesPopup(loc); };
            buttons[2].Click += (s, ev) => { Point loc = popup.Location; popup.Close(); ShowOpacityPopup(loc); };
            buttons[3].Click += (s, ev) => { Point loc = popup.Location; popup.Close(); ShowPresetsPopup(loc); };
            buttons[4].Click += (s, ev) => { Point loc = popup.Location; popup.Close(); ShowAddTilesPopup(loc); };
            buttons[5].Click += (s, ev) => { Point loc = popup.Location; popup.Close(); ShowRowsPopup(loc); };

            int y = 4;
            int btnW = 110;
            foreach (Button b in buttons)
            {
                b.Location = new Point(3, y);
                b.Size = new Size(btnW, 24);
                popup.Controls.Add(b);
                y += 26;
            }

            int popW = btnW + 6;
            popup.Size = new Size(popW, y + 4);
            Point popLoc = popup.Location;
            popLoc.X = popLoc.X - popW + anchor.Width;
            popup.Location = popLoc;
            popup.Show();
        }

        private void RenumberChildOverlays()
        {
            int dpsNum = 0;
            int hpsNum = 0;
            int dtpsNum = 0;
            foreach (ChildOverlay c in childOverlays)
            {
                if (c.Mode == ChildOverlay.ChildMode.DPSOnly)
                {
                    dpsNum++;
                    c.TileNumber = dpsNum;
                }
                else if (c.Mode == ChildOverlay.ChildMode.HPSOnly)
                {
                    hpsNum++;
                    c.TileNumber = hpsNum;
                }
                else if (c.Mode == ChildOverlay.ChildMode.DTPSOnly)
                {
                    dtpsNum++;
                    c.TileNumber = dtpsNum;
                }
                c.UpdateTileNumberLabel();
            }
        }

        private void ShowAddTilesPopup(Point location)
        {
            if (addTilesPopup != null && !addTilesPopup.IsDisposed)
            {
                addTilesPopup.Close();
                addTilesPopup = null;
                return;
            }

            addTilesPopup = new Form();
            addTilesPopup.FormBorderStyle = FormBorderStyle.None;
            addTilesPopup.StartPosition = FormStartPosition.Manual;
            addTilesPopup.BackColor = Color.FromArgb(50, 50, 50);
            addTilesPopup.TopMost = true;
            addTilesPopup.ShowInTaskbar = false;
            addTilesPopup.MinimumSize = new Size(1, 1);
            addTilesPopup.Location = location;
            addTilesPopup.Deactivate += (s, ev) => { addTilesPopup.Close(); };

            BuildAddTilesContent();
            addTilesPopup.Show();
        }

        private void BuildAddTilesContent()
        {
            if (addTilesPopup == null || addTilesPopup.IsDisposed) return;
            addTilesPopup.SuspendLayout();
            addTilesPopup.Controls.Clear();

            int y = 4;
            int btnW = 120;
            int btnH = 24;
            int rowH = 26;

            string[] sections = { "Tracked DPS", "Tracked HPS", "Tracked DTPS" };
            ChildOverlay.ChildMode[] modes = { ChildOverlay.ChildMode.DPSOnly, ChildOverlay.ChildMode.HPSOnly, ChildOverlay.ChildMode.DTPSOnly };

            for (int i = 0; i < sections.Length; i++)
            {
                ChildOverlay.ChildMode mode = modes[i];

                Button btnAdd = CreateSettingsButton(sections[i], Color.FromArgb(255, 80, 200, 80));
                btnAdd.Location = new Point(3, y);
                btnAdd.Size = new Size(btnW, btnH);
                btnAdd.Click += (s, ev) =>
                {
                    ChildOverlay child = new ChildOverlay(this, mode);
                    child.StartPosition = FormStartPosition.Manual;
                    child.Location = new Point(this.Location.X + 30 + (childOverlays.Count * 20), this.Location.Y + 30 + (childOverlays.Count * 20));
                    child.FormClosed += (s2, ev2) =>
                    {
                        childOverlays.Remove(child);
                        RenumberChildOverlays();
                        BuildAddTilesContent();
                    };
                    childOverlays.Add(child);
                    RenumberChildOverlays();
                    child.Show();
                    BuildAddTilesContent();
                };
                addTilesPopup.Controls.Add(btnAdd);
                y += rowH;
            }

            y += 4;
            Button btnCloseAll = CreateSettingsButton("Close All", Color.FromArgb(255, 200, 80, 80));
            btnCloseAll.Location = new Point(3, y);
            btnCloseAll.Size = new Size(btnW, btnH);
            btnCloseAll.Enabled = childOverlays.Count > 0;
            btnCloseAll.Click += (s, ev) =>
            {
                List<ChildOverlay> copy = new List<ChildOverlay>(childOverlays);
                childOverlays.Clear();
                foreach (ChildOverlay c in copy)
                    c.Close();
                RenumberChildOverlays();
                BuildAddTilesContent();
            };
            addTilesPopup.Controls.Add(btnCloseAll);
            y += rowH;

            int popW = btnW + 6;
            addTilesPopup.Size = new Size(popW, y + 4);
            addTilesPopup.ResumeLayout();
        }

        private Button CreateSettingsButton(string text, Color borderColor)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = borderColor;
            btn.FlatAppearance.BorderSize = 1;
            btn.BackColor = Color.FromArgb(60, 60, 60);
            btn.ForeColor = TEXT_COLOR;
            btn.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
            btn.Size = new Size(125, 28);
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.Cursor = Cursors.Hand;
            btn.Margin = new Padding(0, 0, 0, 3);
            return btn;
        }

        private string GetPresetsDir()
        {
            string presetsDir = Path.Combine(GetPluginDir(), "Presets");
            if (!Directory.Exists(presetsDir))
                Directory.CreateDirectory(presetsDir);
            return presetsDir;
        }

        private void ShowPresetsPopup(Control anchor) { ShowPresetsPopup(anchor.PointToScreen(new Point(0, anchor.Height))); }
        private void ShowPresetsPopup(Point location)
        {
            Form popup = new Form();
            popup.FormBorderStyle = FormBorderStyle.None;
            popup.StartPosition = FormStartPosition.Manual;
            popup.BackColor = Color.FromArgb(50, 50, 50);
            popup.TopMost = true;
            popup.ShowInTaskbar = false;
            popup.Location = location;
            popup.Deactivate += (s, ev) => { popup.Close(); };

            BuildPresetsContent(popup);
            popup.Show();
        }

        private void BuildPresetsContent(Form popup)
        {
            popup.SuspendLayout();
            popup.Controls.Clear();

            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.AutoScroll = true;
            content.BackColor = Color.FromArgb(50, 50, 50);

            int y = 4;
            int popupW = 250;

            Label lblSave = new Label();
            lblSave.Text = "Save Current Settings:";
            lblSave.ForeColor = TEXT_COLOR;
            lblSave.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblSave.Location = new Point(4, y);
            lblSave.AutoSize = true;
            content.Controls.Add(lblSave);
            y += 26;

            TextBox txtPresetName = new TextBox();
            txtPresetName.Location = new Point(4, y);
            txtPresetName.Size = new Size(popupW - 66, 22);
            txtPresetName.Font = new Font("Consolas", 8F);
            txtPresetName.BackColor = Color.FromArgb(30, 30, 30);
            txtPresetName.ForeColor = TEXT_COLOR;
            txtPresetName.BorderStyle = BorderStyle.FixedSingle;
            content.Controls.Add(txtPresetName);

            Button btnSave = CreateSettingsButton("Save", Color.FromArgb(255, 80, 200, 80));
            btnSave.Location = new Point(popupW - 58, y);
            btnSave.Size = new Size(52, 22);
            btnSave.Click += (s, ev) =>
            {
                string name = txtPresetName.Text.Trim();
                if (string.IsNullOrEmpty(name)) return;
                foreach (char c in Path.GetInvalidFileNameChars())
                    name = name.Replace(c, '_');
                string presetPath = Path.Combine(GetPresetsDir(), name + ".preset.xml");
                lastPresetName = name;
                SaveSettingsTo(presetPath);
                SaveSettings();
                BuildPresetsContent(popup);
            };
            content.Controls.Add(btnSave);
            y += 30;

            Panel sep = new Panel();
            sep.Location = new Point(4, y);
            sep.Size = new Size(popupW - 8, 1);
            sep.BackColor = DIVIDER_COLOR;
            content.Controls.Add(sep);
            y += 6;

            string[] presetFiles = Directory.GetFiles(GetPresetsDir(), "*.preset.xml");

            if (presetFiles.Length == 0)
            {
                Label lblNone = new Label();
                lblNone.Text = "No saved presets";
                lblNone.ForeColor = ENC_TITLE_COLOR;
                lblNone.Font = new Font("Segoe UI", 8F, FontStyle.Italic);
                lblNone.Location = new Point(4, y);
                lblNone.AutoSize = true;
                content.Controls.Add(lblNone);
                y += 20;
            }
            else
            {
                foreach (string file in presetFiles)
                {
                    string presetName = Path.GetFileNameWithoutExtension(file).Replace(".preset", "");
                    string filePath = file;

                    Button btnLoad = CreateSettingsButton(presetName, Color.FromArgb(255, 50, 80, 160));
                    btnLoad.Location = new Point(4, y);
                    btnLoad.Size = new Size(popupW - 40, 24);
                    btnLoad.Click += (s, ev) =>
                    {
                        lastPresetName = presetName;
                        LoadSettingsFrom(filePath);
                        SaveSettings();
                        popup.Close();
                    };
                    content.Controls.Add(btnLoad);

                    Button btnDel = new Button();
                    btnDel.Text = "\u2715";
                    btnDel.FlatStyle = FlatStyle.Flat;
                    btnDel.FlatAppearance.BorderColor = Color.FromArgb(255, 180, 50, 50);
                    btnDel.FlatAppearance.BorderSize = 1;
                    btnDel.BackColor = Color.FromArgb(60, 60, 60);
                    btnDel.ForeColor = Color.FromArgb(255, 200, 80, 80);
                    btnDel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
                    btnDel.Location = new Point(popupW - 32, y);
                    btnDel.Size = new Size(26, 24);
                    btnDel.Cursor = Cursors.Hand;
                    btnDel.Click += (s, ev) =>
                    {
                        try { File.Delete(filePath); } catch { }
                        BuildPresetsContent(popup);
                    };
                    content.Controls.Add(btnDel);

                    y += 28;
                }
            }

            popup.Controls.Add(content);
            popup.Size = new Size(popupW, Math.Min(y + 8, 400));
            popup.ResumeLayout(true);
        }

        private void SaveSettingsTo(string path)
        {
            string origPath = GetSettingsPath();
            try
            {
                XmlTextWriter xw = new XmlTextWriter(path, System.Text.Encoding.UTF8);
                xw.Formatting = Formatting.Indented;
                xw.WriteStartDocument();
                xw.WriteStartElement("MiniOverlayConfig");

                xw.WriteElementString("Layout", currentLayout.ToString());
                xw.WriteElementString("Opacity", currentOpacityPct.ToString());
                xw.WriteElementString("LocationX", this.Location.X.ToString());
                xw.WriteElementString("LocationY", this.Location.Y.ToString());
                xw.WriteElementString("Width", this.Size.Width.ToString());
                xw.WriteElementString("Height", this.Size.Height.ToString());
                string dpsH = "";
                string hpsH = "";
                string dtpsH = "";
                if (txtDPSHandle != null && txtDPSHandle.Text != PLACEHOLDER_DPS)
                    dpsH = txtDPSHandle.Text ?? "";
                if (txtHPSHandle != null && txtHPSHandle.Text != PLACEHOLDER_HPS)
                    hpsH = txtHPSHandle.Text ?? "";
                if (txtDTPSHandle != null && txtDTPSHandle.Text != PLACEHOLDER_DTPS)
                    dtpsH = txtDTPSHandle.Text ?? "";
                xw.WriteElementString("DPSHandle", dpsH);
                xw.WriteElementString("HPSHandle", hpsH);
                xw.WriteElementString("DTPSHandle", dtpsH);

                if (tileOrder != null)
                {
                    foreach (TileInfo ti in tileOrder)
                        xw.WriteElementString("Tile_" + ti.Name.Replace(" ", "_"), ti.Visible.ToString());
                }

                for (int i = 0; i < tileFontSizes.Length; i++)
                    xw.WriteElementString("FontSize_" + i, tileFontSizes[i].ToString());

                xw.WriteElementString("MaxRowsDPS", maxRowsDPS.ToString());
                xw.WriteElementString("MaxRowsHPS", maxRowsHPS.ToString());
                xw.WriteElementString("MaxRowsDTPS", maxRowsDTPS.ToString());
                xw.WriteElementString("MaxRowsTrackedDPS", maxRowsTrackedDPS.ToString());
                xw.WriteElementString("MaxRowsTrackedHPS", maxRowsTrackedHPS.ToString());
                xw.WriteElementString("MaxRowsTrackedDTPS", maxRowsTrackedDTPS.ToString());

                xw.WriteElementString("ChildCount", childOverlays.Count.ToString());
                for (int i = 0; i < childOverlays.Count; i++)
                {
                    ChildOverlay c = childOverlays[i];
                    xw.WriteStartElement("Child_" + i);
                    xw.WriteElementString("Mode", c.Mode.ToString());
                    xw.WriteElementString("X", c.Location.X.ToString());
                    xw.WriteElementString("Y", c.Location.Y.ToString());
                    xw.WriteElementString("W", c.Size.Width.ToString());
                    xw.WriteElementString("H", c.Size.Height.ToString());
                    xw.WriteElementString("DPSHandle", c.DPSHandleValue ?? "");
                    xw.WriteElementString("HPSHandle", c.HPSHandleValue ?? "");
                    xw.WriteElementString("RowLimit", c.RowLimit.ToString());
                    xw.WriteEndElement();
                }

                xw.WriteEndElement();
                xw.WriteEndDocument();
                xw.Close();
            }
            catch { }
        }

        private void LoadSettingsFrom(string path)
        {
            try
            {
                if (!File.Exists(path)) return;

                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlNode root = doc.SelectSingleNode("MiniOverlayConfig");
                if (root == null) return;

                XmlNode n;
                n = root.SelectSingleNode("Layout");
                if (n != null)
                {
                    if (n.InnerText == "Vertical") currentLayout = LayoutMode.Vertical;
                    else if (n.InnerText == "Horizontal") currentLayout = LayoutMode.Horizontal;
                    else currentLayout = LayoutMode.Main;
                }

                n = root.SelectSingleNode("Opacity");
                if (n != null) { int op; if (int.TryParse(n.InnerText, out op)) SetOpacitySafe(op / 100.0); }

                int lx = this.Location.X, ly = this.Location.Y;
                n = root.SelectSingleNode("LocationX"); if (n != null) int.TryParse(n.InnerText, out lx);
                n = root.SelectSingleNode("LocationY"); if (n != null) int.TryParse(n.InnerText, out ly);
                this.Location = new Point(lx, ly);

                int w = this.Size.Width, h = this.Size.Height;
                n = root.SelectSingleNode("Width"); if (n != null) int.TryParse(n.InnerText, out w);
                n = root.SelectSingleNode("Height"); if (n != null) int.TryParse(n.InnerText, out h);
                this.Size = new Size(w, h);

                n = root.SelectSingleNode("DPSHandle");
                if (n != null && !string.IsNullOrEmpty(n.InnerText)) SetDPSHandle(n.InnerText);
                n = root.SelectSingleNode("HPSHandle");
                if (n != null && !string.IsNullOrEmpty(n.InnerText)) SetHPSHandle(n.InnerText);
                n = root.SelectSingleNode("DTPSHandle");
                if (n != null && !string.IsNullOrEmpty(n.InnerText)) SetDTPSHandle(n.InnerText);

                if (tileOrder != null)
                {
                    foreach (TileInfo ti in tileOrder)
                    {
                        n = root.SelectSingleNode("Tile_" + ti.Name.Replace(" ", "_"));
                        if (n != null) { bool v; if (bool.TryParse(n.InnerText, out v)) ti.Visible = v; }
                    }
                }

                for (int i = 0; i < tileFontSizes.Length; i++)
                {
                    n = root.SelectSingleNode("FontSize_" + i);
                    if (n != null)
                    {
                        float sz;
                        if (float.TryParse(n.InnerText, out sz))
                        {
                            if (sz <= 24f) sz = 48f;
                            ApplyFontSize(i, sz);
                        }
                    }
                }

                int mr;
                n = root.SelectSingleNode("MaxRowsDPS"); if (n != null && int.TryParse(n.InnerText, out mr)) maxRowsDPS = mr;
                n = root.SelectSingleNode("MaxRowsHPS"); if (n != null && int.TryParse(n.InnerText, out mr)) maxRowsHPS = mr;
                n = root.SelectSingleNode("MaxRowsDTPS"); if (n != null && int.TryParse(n.InnerText, out mr)) maxRowsDTPS = mr;
                n = root.SelectSingleNode("MaxRowsTrackedDPS"); if (n != null && int.TryParse(n.InnerText, out mr)) maxRowsTrackedDPS = mr;
                n = root.SelectSingleNode("MaxRowsTrackedHPS"); if (n != null && int.TryParse(n.InnerText, out mr)) maxRowsTrackedHPS = mr;
                n = root.SelectSingleNode("MaxRowsTrackedDTPS"); if (n != null && int.TryParse(n.InnerText, out mr)) maxRowsTrackedDTPS = mr;

                ApplyLayout(currentLayout);

                this.Size = new Size(w, h);
                this.Location = new Point(lx, ly);

                foreach (ChildOverlay c in new List<ChildOverlay>(childOverlays))
                    c.Close();
                childOverlays.Clear();

                n = root.SelectSingleNode("ChildCount");
                int childCount = 0;
                if (n != null) int.TryParse(n.InnerText, out childCount);
                for (int ci = 0; ci < childCount; ci++)
                {
                    XmlNode childNode = root.SelectSingleNode("Child_" + ci);
                    if (childNode == null) continue;

                    ChildOverlay.ChildMode mode = ChildOverlay.ChildMode.Both;
                    XmlNode mn = childNode.SelectSingleNode("Mode");
                    if (mn != null)
                    {
                        if (mn.InnerText == "DPSOnly") mode = ChildOverlay.ChildMode.DPSOnly;
                        else if (mn.InnerText == "HPSOnly") mode = ChildOverlay.ChildMode.HPSOnly;
                    }

                    ChildOverlay child = new ChildOverlay(this, mode);
                    child.StartPosition = FormStartPosition.Manual;

                    int cx = this.Location.X + 30, cy = this.Location.Y + 30;
                    int cw = 500, ch = 300;
                    XmlNode cxn = childNode.SelectSingleNode("X"); if (cxn != null) int.TryParse(cxn.InnerText, out cx);
                    XmlNode cyn = childNode.SelectSingleNode("Y"); if (cyn != null) int.TryParse(cyn.InnerText, out cy);
                    XmlNode cwn = childNode.SelectSingleNode("W"); if (cwn != null) int.TryParse(cwn.InnerText, out cw);
                    XmlNode chn = childNode.SelectSingleNode("H"); if (chn != null) int.TryParse(chn.InnerText, out ch);
                    child.Location = new Point(cx, cy);
                    child.Size = new Size(cw, ch);

                    string cdps = "", chps = "";
                    XmlNode cdn = childNode.SelectSingleNode("DPSHandle"); if (cdn != null) cdps = cdn.InnerText;
                    XmlNode chn2 = childNode.SelectSingleNode("HPSHandle"); if (chn2 != null) chps = chn2.InnerText;
                    child.SetHandles(cdps, chps);

                    XmlNode rln = childNode.SelectSingleNode("RowLimit");
                    int rl;
                    if (rln != null && int.TryParse(rln.InnerText, out rl)) child.RowLimit = rl;

                    child.FormClosed += (s2, ev2) => { childOverlays.Remove(child); RenumberChildOverlays(); };
                    childOverlays.Add(child);
                    child.Show();
                    child.Opacity = this.Opacity;
                }
                RenumberChildOverlays();
            }
            catch { }
        }

        private void ShowOpacityPopup(Control anchor) { ShowOpacityPopup(anchor.PointToScreen(new Point(0, anchor.Height))); }
        private void ShowOpacityPopup(Point location)
        {
            Form popup = new Form();
            popup.FormBorderStyle = FormBorderStyle.None;
            popup.StartPosition = FormStartPosition.Manual;
            popup.BackColor = Color.FromArgb(50, 50, 50);
            popup.TopMost = true;
            popup.ShowInTaskbar = false;
            popup.MinimumSize = new Size(1, 1);
            popup.Location = location;
            popup.Deactivate += (s, ev) => { popup.Close(); };

            TrackBar slider = new TrackBar();
            slider.Minimum = 40;
            slider.Maximum = 100;
            slider.Value = currentOpacityPct;
            slider.TickStyle = TickStyle.None;
            slider.Dock = DockStyle.Fill;
            slider.BackColor = Color.FromArgb(50, 50, 50);
            slider.ValueChanged += (s, ev) =>
            {
                SetOpacitySafe(slider.Value / 100.0);
            };

            popup.Controls.Add(slider);
            popup.Size = new Size(200, 45);
            popup.Show();
        }

        private void ShowRowsPopup(Point location)
        {
            Form popup = new Form();
            popup.FormBorderStyle = FormBorderStyle.None;
            popup.StartPosition = FormStartPosition.Manual;
            popup.BackColor = Color.FromArgb(50, 50, 50);
            popup.TopMost = true;
            popup.ShowInTaskbar = false;
            popup.MinimumSize = new Size(1, 1);
            popup.Location = location;
            popup.Deactivate += (s, ev) => { popup.Close(); };

            List<string> labels = new List<string> { "DPS", "HPS", "DTPS", "Tracked DPS", "Tracked HPS", "Tracked DTPS" };
            List<Func<int>> getters = new List<Func<int>>
            {
                () => maxRowsDPS,
                () => maxRowsHPS,
                () => maxRowsDTPS,
                () => maxRowsTrackedDPS,
                () => maxRowsTrackedHPS,
                () => maxRowsTrackedDTPS
            };
            List<Action<int>> setters = new List<Action<int>>
            {
                v => maxRowsDPS = v,
                v => maxRowsHPS = v,
                v => maxRowsDTPS = v,
                v => maxRowsTrackedDPS = v,
                v => maxRowsTrackedHPS = v,
                v => maxRowsTrackedDTPS = v
            };

            foreach (ChildOverlay c in childOverlays)
            {
                ChildOverlay co = c;
                if (co.Mode == ChildOverlay.ChildMode.DPSOnly)
                    labels.Add("Tracked DPS " + co.TileNumber);
                else if (co.Mode == ChildOverlay.ChildMode.HPSOnly)
                    labels.Add("Tracked HPS " + co.TileNumber);
                else if (co.Mode == ChildOverlay.ChildMode.DTPSOnly)
                    labels.Add("Tracked DTPS " + co.TileNumber);
                else
                    labels.Add("Tracked " + co.TileNumber);
                getters.Add(() => co.RowLimit);
                setters.Add(v => co.RowLimit = v);
            }

            int y = 6;
            for (int i = 0; i < labels.Count; i++)
            {
                Label lbl = new Label();
                lbl.Text = labels[i];
                lbl.ForeColor = TEXT_COLOR;
                lbl.Font = new Font("Segoe UI", 8F);
                lbl.Location = new Point(6, y + 4);
                lbl.Size = new Size(110, 18);
                popup.Controls.Add(lbl);

                TextBox txt = new TextBox();
                txt.BorderStyle = BorderStyle.FixedSingle;
                txt.BackColor = Color.FromArgb(40, 40, 40);
                txt.ForeColor = TEXT_COLOR;
                txt.Location = new Point(120, y + 2);
                txt.Size = new Size(70, 20);
                txt.MaxLength = 3;
                int currentVal = getters[i]();
                if (currentVal > 0)
                {
                    txt.Text = currentVal.ToString();
                    txt.Font = new Font("Consolas", 8F, FontStyle.Regular);
                }
                else
                {
                    txt.Text = PLACEHOLDER_ROWS;
                    txt.ForeColor = PLACEHOLDER_COLOR;
                    txt.Font = new Font("Consolas", 8F, FontStyle.Italic);
                }
                Action<int> setter = setters[i];
                txt.Enter += (s, ev) =>
                {
                    if (txt.Text == PLACEHOLDER_ROWS)
                    {
                        txt.Text = "";
                        txt.ForeColor = TEXT_COLOR;
                        txt.Font = new Font("Consolas", 8F, FontStyle.Regular);
                    }
                };
                txt.Leave += (s, ev) =>
                {
                    if (string.IsNullOrEmpty(txt.Text))
                    {
                        txt.Text = PLACEHOLDER_ROWS;
                        txt.ForeColor = PLACEHOLDER_COLOR;
                        txt.Font = new Font("Consolas", 8F, FontStyle.Italic);
                    }
                };
                txt.KeyPress += (s, ev) =>
                {
                    if (!char.IsDigit(ev.KeyChar) && ev.KeyChar != (char)Keys.Back) ev.Handled = true;
                };
                txt.TextChanged += (s, ev) =>
                {
                    if (txt.Text == PLACEHOLDER_ROWS) return;
                    int v = 0;
                    int.TryParse(txt.Text, out v);
                    if (v < 0) v = 0;
                    setter(v);
                };
                popup.Controls.Add(txt);
                y += 26;
            }

            popup.Size = new Size(200, y + 6);
            popup.Show();
        }

        private void ShowTilesPopup(Control anchor) { ShowTilesPopup(anchor.PointToScreen(new Point(0, anchor.Height))); }
        private void ShowTilesPopup(Point location)
        {
            if (tilesPopup != null && !tilesPopup.IsDisposed)
            {
                tilesPopup.Close();
                tilesPopup = null;
                return;
            }

            tilesPopup = new Form();
            tilesPopup.FormBorderStyle = FormBorderStyle.None;
            tilesPopup.StartPosition = FormStartPosition.Manual;
            tilesPopup.BackColor = Color.FromArgb(50, 50, 50);
            tilesPopup.Size = new Size(200, tileOrder.Count * 28 + 4);
            tilesPopup.TopMost = true;
            tilesPopup.ShowInTaskbar = false;
            tilesPopup.Location = location;

            tilesPopup.Deactivate += (s, ev) => { tilesPopup.Close(); };

            RebuildTilesPopup();
            tilesPopup.Show();
        }

        private void RebuildTilesPopup()
        {
            if (tilesPopup == null || tilesPopup.IsDisposed) return;
            tilesPopup.SuspendLayout();
            tilesPopup.Controls.Clear();

            int y = 2;
            for (int idx = 0; idx < tileOrder.Count; idx++)
            {
                TileInfo ti = tileOrder[idx];

                CheckBox chk = new CheckBox();
                chk.Text = ti.Name;
                chk.Checked = ti.Visible;
                chk.ForeColor = TEXT_COLOR;
                chk.Font = new Font("Segoe UI", 8F);
                chk.Location = new Point(8, y + 3);
                chk.Size = new Size(180, 20);
                chk.CheckedChanged += (s, ev) =>
                {
                    ti.Visible = chk.Checked;
                    ApplyLayout(currentLayout);
                };

                tilesPopup.Controls.Add(chk);
                y += 26;
            }

            tilesPopup.Size = new Size(200, y + 4);
            tilesPopup.ResumeLayout(true);
        }

        private void ApplyLayout(LayoutMode mode)
        {
            currentLayout = mode;
            this.SuspendLayout();

            this.Controls.Clear();
            panelTopRow.Controls.Clear();
            panelBottomRow.Controls.Clear();
            panelHandleBar.Controls.Clear();
            panelDPSHandleSingle.Controls.Clear();
            panelHPSHandleSingle.Controls.Clear();

            panelDPS.Controls.Clear();
            panelDPS.Controls.Add(lblDPSContent);
            panelDPS.Controls.Add(lblDPSEncTitle);
            panelDPS.Controls.Add(lblDPSHeader);

            panelHPS.Controls.Clear();
            panelHPS.Controls.Add(lblHPSContent);
            panelHPS.Controls.Add(lblHPSEncTitle);
            panelHPS.Controls.Add(lblHPSHeader);

            panelMyDPS.Controls.Clear();
            panelMyDPS.Controls.Add(lblMyDPSContent);
            panelMyDPS.Controls.Add(lblMyDPSHeader);

            panelMyHPS.Controls.Clear();
            panelMyHPS.Controls.Add(lblMyHPSContent);
            panelMyHPS.Controls.Add(lblMyHPSHeader);

            panelMend.Controls.Clear();
            panelMend.Controls.Add(lblMendContent);
            panelMend.Controls.Add(mendEmoticonWrapper);
            panelMend.Controls.Add(lblMendHeader);

            panelDTPS.Controls.Clear();
            panelDTPS.Controls.Add(lblDTPSContent);
            panelDTPS.Controls.Add(lblDTPSEncTitle);
            panelDTPS.Controls.Add(lblDTPSHeader);

            panelMyDTPS.Controls.Clear();
            panelMyDTPS.Controls.Add(lblMyDTPSContent);
            panelMyDTPS.Controls.Add(lblMyDTPSHeader);

            panelDTPSHandleSingle.Controls.Clear();
            panelDTPSHandleSingle.Controls.Add(txtDTPSHandle);
            txtDTPSHandle.Dock = DockStyle.Fill;

            if (IsTileVisible(panelMyDTPS))
            {
                panelMyDTPS.Controls.Add(panelDTPSHandleSingle);
                panelMyDTPS.Controls.Remove(lblMyDTPSHeader);
                panelMyDTPS.Controls.Add(lblMyDTPSHeader);
            }

            panelDPSHandleSingle.Controls.Clear();
            panelDPSHandleSingle.Controls.Add(txtDPSHandle);
            txtDPSHandle.Dock = DockStyle.Fill;

            panelHPSHandleSingle.Controls.Clear();
            panelHPSHandleSingle.Controls.Add(txtHPSHandle);
            txtHPSHandle.Dock = DockStyle.Fill;

            if (IsTileVisible(panelMyDPS))
            {
                panelMyDPS.Controls.Add(panelDPSHandleSingle);
                panelMyDPS.Controls.Remove(lblMyDPSHeader);
                panelMyDPS.Controls.Add(lblMyDPSHeader);
            }
            if (IsTileVisible(panelMyHPS))
            {
                panelMyHPS.Controls.Add(panelHPSHandleSingle);
                panelMyHPS.Controls.Remove(lblMyHPSHeader);
                panelMyHPS.Controls.Add(lblMyHPSHeader);
            }

            switch (mode)
            {
                case LayoutMode.Main:
                    ApplyMainLayout();
                    break;
                case LayoutMode.Vertical:
                    ApplyVerticalLayout();
                    break;
                case LayoutMode.Horizontal:
                    ApplyHorizontalLayout();
                    break;
            }

            this.Controls.Add(panelButtonBar);

            this.ResumeLayout(true);
            DoMainLayout();
            try
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    DoMainLayout();
                    panelButtonBar.Invalidate(true);
                    this.Invalidate(true);
                    this.Refresh();
                });
            }
            catch { }
        }

        private void ApplyMainLayout()
        {
            this.MinimumSize = new Size(115, 400);
            Size desired = new Size(PANEL_WIDTH * 2 + DIVIDER_WIDTH + 60, 900);
            if (this.Size.Width < desired.Width || this.Size.Height < desired.Height)
                this.Size = desired;

            bool showD = IsTileVisible(panelDPS);
            bool showH = IsTileVisible(panelHPS);
            bool showDT = IsTileVisible(panelDTPS);
            bool showM = IsTileVisible(panelMend);
            bool showTD = IsTileVisible(panelMyDPS);
            bool showTH = IsTileVisible(panelMyHPS);
            bool showTDT = IsTileVisible(panelMyDTPS);

            bool hasTopRow = showD || showH;
            bool hasBottomRow = showTD || showTH;

            if (hasTopRow)
            {
                if (showD && showH)
                {
                    panelDPS.Dock = DockStyle.Left;
                    panelHPS.Dock = DockStyle.Fill;
                    Panel divTop = new Panel { Dock = DockStyle.Left, Width = DIVIDER_WIDTH, BackColor = DIVIDER_COLOR };
                    panelTopRow.Controls.Add(panelHPS);
                    panelTopRow.Controls.Add(divTop);
                    panelTopRow.Controls.Add(panelDPS);
                }
                else if (showD) { panelDPS.Dock = DockStyle.Fill; panelTopRow.Controls.Add(panelDPS); }
                else { panelHPS.Dock = DockStyle.Fill; panelTopRow.Controls.Add(panelHPS); }

                panelTopRow.Dock = DockStyle.Top;
                panelTopRow.Height = 120;
            }

            if (hasBottomRow)
            {
                if (showTD && showTH)
                {
                    panelMyDPS.Dock = DockStyle.Left;
                    panelMyHPS.Dock = DockStyle.Fill;
                    Panel divBot = new Panel { Dock = DockStyle.Left, Width = DIVIDER_WIDTH, BackColor = DIVIDER_COLOR };
                    panelBottomRow.Controls.Add(panelMyHPS);
                    panelBottomRow.Controls.Add(divBot);
                    panelBottomRow.Controls.Add(panelMyDPS);
                }
                else if (showTD) { panelMyDPS.Dock = DockStyle.Fill; panelBottomRow.Controls.Add(panelMyDPS); }
                else { panelMyHPS.Dock = DockStyle.Fill; panelBottomRow.Controls.Add(panelMyHPS); }

                panelBottomRow.Dock = showTDT ? DockStyle.Top : DockStyle.Fill;
                if (showTDT && panelBottomRow.Height < 50) panelBottomRow.Height = 200;
            }

            if (showM)
            {
                panelMend.Dock = DockStyle.Top;
                panelMend.Height = 60;
            }

            if (showDT)
            {
                panelDTPS.Dock = DockStyle.Top;
                panelDTPS.Height = 120;
            }

            if (showTDT)
            {
                panelMyDTPS.Dock = DockStyle.Fill;
            }

            if (showTDT) this.Controls.Add(panelMyDTPS);
            if (hasBottomRow && showTDT)
                this.Controls.Add(new Panel { Dock = DockStyle.Top, Height = DIVIDER_WIDTH, BackColor = DIVIDER_COLOR });
            if (hasBottomRow) this.Controls.Add(panelBottomRow);
            if (showM && (hasBottomRow || showTDT))
                this.Controls.Add(new Panel { Dock = DockStyle.Top, Height = DIVIDER_WIDTH, BackColor = DIVIDER_COLOR });
            if (showM) this.Controls.Add(panelMend);
            if (showDT && (showM || hasBottomRow || showTDT))
                this.Controls.Add(new Panel { Dock = DockStyle.Top, Height = DIVIDER_WIDTH, BackColor = DIVIDER_COLOR });
            if (showDT) this.Controls.Add(panelDTPS);
            if (hasTopRow && (showDT || showM || hasBottomRow || showTDT))
                this.Controls.Add(new Panel { Dock = DockStyle.Top, Height = DIVIDER_WIDTH, BackColor = DIVIDER_COLOR });
            if (hasTopRow) this.Controls.Add(panelTopRow);
        }

        private void ApplyVerticalLayout()
        {
            this.MinimumSize = new Size(115, 300);
            this.Size = new Size(PANEL_WIDTH + 40, 900);

            List<Panel> visible = GetVisibleTiles();
            if (visible.Count == 0) return;

            panelTopRow.Controls.Clear();
            panelBottomRow.Controls.Clear();

            for (int i = 0; i < visible.Count; i++)
                visible[i].Dock = (i == visible.Count - 1) ? DockStyle.Fill : DockStyle.Top;

            for (int i = visible.Count - 1; i >= 0; i--)
            {
                this.Controls.Add(visible[i]);
                if (i > 0)
                    this.Controls.Add(new Panel { Dock = DockStyle.Top, Height = DIVIDER_WIDTH, BackColor = DIVIDER_COLOR });
            }
        }

        private void ApplyHorizontalLayout()
        {
            this.MinimumSize = new Size(115, 200);
            int tilesCount = GetVisibleTiles().Count;
            if (tilesCount < 1) tilesCount = 1;
            this.Size = new Size(PANEL_WIDTH * tilesCount + DIVIDER_WIDTH * (tilesCount - 1), 350);

            List<Panel> visible = GetVisibleTiles();
            if (visible.Count == 0) return;

            panelTopRow.Controls.Clear();
            panelBottomRow.Controls.Clear();

            for (int i = 0; i < visible.Count; i++)
                visible[i].Dock = (i == visible.Count - 1) ? DockStyle.Fill : DockStyle.Left;

            for (int i = visible.Count - 1; i >= 0; i--)
            {
                this.Controls.Add(visible[i]);
                if (i > 0)
                    this.Controls.Add(new Panel { Dock = DockStyle.Left, Width = DIVIDER_WIDTH, BackColor = DIVIDER_COLOR });
            }
        }

        private List<Panel> GetVisibleTiles()
        {
            List<Panel> tiles = new List<Panel>();
            if (tileOrder == null) return tiles;
            foreach (TileInfo ti in tileOrder)
            {
                if (ti.Visible) tiles.Add(ti.Panel);
            }
            return tiles;
        }

        private bool IsTileVisible(Panel p)
        {
            if (tileOrder == null) return true;
            foreach (TileInfo ti in tileOrder)
                if (ti.Panel == p && ti.Visible) return true;
            return false;
        }

        internal EncounterData GetCurrentEncounter()
        {
            try
            {
                if (ActGlobals.oFormActMain.ZoneList.Count > 0)
                {
                    var lastZone = ActGlobals.oFormActMain.ZoneList[ActGlobals.oFormActMain.ZoneList.Count - 1];
                    if (lastZone.Items.Count > 0)
                    {
                        return lastZone.Items[lastZone.Items.Count - 1];
                    }
                }
            }
            catch { }
            return null;
        }

        private const int RESIZE_BORDER = 48;
        private const int WM_NCHITTEST = 0x84;
        private const int WM_SIZE = 0x0005;
        private const int HTBOTTOMRIGHT = 17;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                Point pos = this.PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16));
                if (pos.X >= this.ClientSize.Width - RESIZE_BORDER && pos.Y >= this.ClientSize.Height - RESIZE_BORDER)
                {
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                    return;
                }
            }
            base.WndProc(ref m);
            if (m.Msg == WM_SIZE)
            {
                try { DoMainLayout(); } catch { }
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle &= ~0x20;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED - double buffer all child controls
                return cp;
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            DoMainLayout();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoMainLayout();
        }

        private void DoMainLayout()
        {
            if (panelDPS == null || panelMyDPS == null) return;

            switch (currentLayout)
            {
                case LayoutMode.Main:
                    int halfWidth = (this.ClientSize.Width - DIVIDER_WIDTH) / 2;
                    if (IsTileVisible(panelDPS) && IsTileVisible(panelHPS)) panelDPS.Width = halfWidth;
                    if (IsTileVisible(panelMyDPS) && IsTileVisible(panelMyHPS)) panelMyDPS.Width = halfWidth;

                    bool hasTop = IsTileVisible(panelDPS) || IsTileVisible(panelHPS);
                    bool hasBot = IsTileVisible(panelMyDPS) || IsTileVisible(panelMyHPS);
                    bool hasDT = IsTileVisible(panelDTPS);
                    bool hasTDT = IsTileVisible(panelMyDTPS);
                    float units = (hasTop ? 1f : 0f) + (hasDT ? 1f : 0f) + (IsTileVisible(panelMend) ? 0.5f : 0f) + (hasBot ? 1f : 0f) + (hasTDT ? 1f : 0f);
                    if (units > 0)
                    {
                        int fixedHeight = panelButtonBar.Height + DIVIDER_WIDTH * 2;
                        int availHeight = this.ClientSize.Height - fixedHeight;
                        int unitH = (int)(availHeight / units);
                        if (hasTop) panelTopRow.Height = unitH;
                        if (hasDT) panelDTPS.Height = unitH;
                        if (IsTileVisible(panelMend)) panelMend.Height = unitH / 2 + HEADER_HEIGHT;
                        if (hasBot && hasTDT) panelBottomRow.Height = unitH;
                    }
                    break;

                case LayoutMode.Vertical:
                    List<Panel> vTiles = GetVisibleTiles();
                    int vCount = vTiles.Count;
                    if (vCount > 0)
                    {
                        int vFixedH = panelButtonBar.Height + lblHandleTracking.Height + panelHandleBar.Height + DIVIDER_WIDTH * (vCount - 1);
                        int vAvail = this.ClientSize.Height - vFixedH;
                        int tileH = vAvail / vCount;
                        for (int i = 0; i < vCount - 1; i++) vTiles[i].Height = tileH;
                    }
                    break;

                case LayoutMode.Horizontal:
                    List<Panel> hTiles = GetVisibleTiles();
                    int hCount = hTiles.Count;
                    if (hCount > 0)
                    {
                        int hFixedW = DIVIDER_WIDTH * (hCount - 1);
                        int hAvail = this.ClientSize.Width - hFixedW;
                        int tileW = hAvail / hCount;
                        for (int i = 0; i < hCount - 1; i++) hTiles[i].Width = tileW;
                    }
                    break;
            }

            AutoFitAllFonts();
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(DIVIDER_COLOR, 2))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            }

            int gripSize = 12;
            int x = this.Width - gripSize - 2;
            int y = this.Height - gripSize - 2;
            using (Pen gripPen = new Pen(Color.FromArgb(200, 180, 180, 180), 1))
            {
                for (int i = 0; i < 3; i++)
                {
                    int offset = i * 4;
                    e.Graphics.DrawLine(gripPen,
                        x + offset + gripSize, y,
                        x, y + offset + gripSize);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                SaveSettings();
                this.Hide();
                return;
            }
            if (updateTimer != null)
            {
                updateTimer.Stop();
                updateTimer.Dispose();
            }
            base.OnFormClosing(e);
        }

        public void Shutdown()
        {
            SaveSettings();
            foreach (ChildOverlay child in new List<ChildOverlay>(childOverlays))
                child.Close();
            childOverlays.Clear();
            if (updateTimer != null)
            {
                updateTimer.Stop();
                updateTimer.Dispose();
                updateTimer = null;
            }
            this.Close();
            this.Dispose();
        }

        private string GetPluginDir()
        {
            string path = ResolveLoadedPluginPath();
            if (!string.IsNullOrEmpty(path))
            {
                try { return Path.GetDirectoryName(path); } catch { }
            }
            return Environment.CurrentDirectory;
        }

        private string GetSettingsPath()
        {
            return Path.Combine(GetPluginDir(), "CO_MiniOverlay.config.xml");
        }

        public void SaveSettings()
        {
            try
            {
                XmlTextWriter xw = new XmlTextWriter(GetSettingsPath(), System.Text.Encoding.UTF8);
                xw.Formatting = Formatting.Indented;
                xw.WriteStartDocument();
                xw.WriteStartElement("MiniOverlayConfig");

                xw.WriteElementString("Layout", currentLayout.ToString());
                xw.WriteElementString("Opacity", currentOpacityPct.ToString());
                xw.WriteElementString("LocationX", this.Location.X.ToString());
                xw.WriteElementString("LocationY", this.Location.Y.ToString());
                xw.WriteElementString("Width", this.Size.Width.ToString());
                xw.WriteElementString("Height", this.Size.Height.ToString());
                string dpsH = "";
                string hpsH = "";
                string dtpsH = "";
                if (txtDPSHandle != null && txtDPSHandle.Text != PLACEHOLDER_DPS)
                    dpsH = txtDPSHandle.Text ?? "";
                if (txtHPSHandle != null && txtHPSHandle.Text != PLACEHOLDER_HPS)
                    hpsH = txtHPSHandle.Text ?? "";
                if (txtDTPSHandle != null && txtDTPSHandle.Text != PLACEHOLDER_DTPS)
                    dtpsH = txtDTPSHandle.Text ?? "";
                xw.WriteElementString("DPSHandle", dpsH);
                xw.WriteElementString("HPSHandle", hpsH);
                xw.WriteElementString("DTPSHandle", dtpsH);

                if (tileOrder != null)
                {
                    foreach (TileInfo ti in tileOrder)
                        xw.WriteElementString("Tile_" + ti.Name.Replace(" ", "_"), ti.Visible.ToString());
                }

                for (int i = 0; i < tileFontSizes.Length; i++)
                    xw.WriteElementString("FontSize_" + i, tileFontSizes[i].ToString());

                xw.WriteElementString("MaxRowsDPS", maxRowsDPS.ToString());
                xw.WriteElementString("MaxRowsHPS", maxRowsHPS.ToString());
                xw.WriteElementString("MaxRowsDTPS", maxRowsDTPS.ToString());
                xw.WriteElementString("MaxRowsTrackedDPS", maxRowsTrackedDPS.ToString());
                xw.WriteElementString("MaxRowsTrackedHPS", maxRowsTrackedHPS.ToString());
                xw.WriteElementString("MaxRowsTrackedDTPS", maxRowsTrackedDTPS.ToString());

                xw.WriteElementString("LastPreset", lastPresetName ?? "");

                xw.WriteEndElement();
                xw.WriteEndDocument();
                xw.Close();
            }
            catch { }
        }

        public void LoadSettings()
        {
            try
            {
                string path = GetSettingsPath();
                if (!File.Exists(path)) return;

                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlNode root = doc.SelectSingleNode("MiniOverlayConfig");
                if (root == null) return;

                XmlNode n;
                n = root.SelectSingleNode("Layout");
                if (n != null)
                {
                    if (n.InnerText == "Vertical") currentLayout = LayoutMode.Vertical;
                    else if (n.InnerText == "Horizontal") currentLayout = LayoutMode.Horizontal;
                    else currentLayout = LayoutMode.Main;
                }

                n = root.SelectSingleNode("Opacity");
                if (n != null) { int op; if (int.TryParse(n.InnerText, out op)) SetOpacitySafe(op / 100.0); }

                int lx = this.Location.X, ly = this.Location.Y;
                n = root.SelectSingleNode("LocationX"); if (n != null) int.TryParse(n.InnerText, out lx);
                n = root.SelectSingleNode("LocationY"); if (n != null) int.TryParse(n.InnerText, out ly);
                this.Location = new Point(lx, ly);

                int w = this.Size.Width, h = this.Size.Height;
                n = root.SelectSingleNode("Width"); if (n != null) int.TryParse(n.InnerText, out w);
                n = root.SelectSingleNode("Height"); if (n != null) int.TryParse(n.InnerText, out h);
                this.Size = new Size(w, h);

                n = root.SelectSingleNode("DPSHandle");
                if (n != null && !string.IsNullOrEmpty(n.InnerText)) SetDPSHandle(n.InnerText);
                n = root.SelectSingleNode("HPSHandle");
                if (n != null && !string.IsNullOrEmpty(n.InnerText)) SetHPSHandle(n.InnerText);
                n = root.SelectSingleNode("DTPSHandle");
                if (n != null && !string.IsNullOrEmpty(n.InnerText)) SetDTPSHandle(n.InnerText);

                if (tileOrder != null)
                {
                    foreach (TileInfo ti in tileOrder)
                    {
                        n = root.SelectSingleNode("Tile_" + ti.Name.Replace(" ", "_"));
                        if (n != null) { bool v; if (bool.TryParse(n.InnerText, out v)) ti.Visible = v; }
                    }
                }

                for (int i = 0; i < tileFontSizes.Length; i++)
                {
                    n = root.SelectSingleNode("FontSize_" + i);
                    if (n != null)
                    {
                        float sz;
                        if (float.TryParse(n.InnerText, out sz))
                        {
                            if (sz <= 24f) sz = 48f;
                            ApplyFontSize(i, sz);
                        }
                    }
                }

                int mr;
                n = root.SelectSingleNode("MaxRowsDPS"); if (n != null && int.TryParse(n.InnerText, out mr)) maxRowsDPS = mr;
                n = root.SelectSingleNode("MaxRowsHPS"); if (n != null && int.TryParse(n.InnerText, out mr)) maxRowsHPS = mr;
                n = root.SelectSingleNode("MaxRowsDTPS"); if (n != null && int.TryParse(n.InnerText, out mr)) maxRowsDTPS = mr;
                n = root.SelectSingleNode("MaxRowsTrackedDPS"); if (n != null && int.TryParse(n.InnerText, out mr)) maxRowsTrackedDPS = mr;
                n = root.SelectSingleNode("MaxRowsTrackedHPS"); if (n != null && int.TryParse(n.InnerText, out mr)) maxRowsTrackedHPS = mr;
                n = root.SelectSingleNode("MaxRowsTrackedDTPS"); if (n != null && int.TryParse(n.InnerText, out mr)) maxRowsTrackedDTPS = mr;

                ApplyLayout(currentLayout);

                this.Size = new Size(w, h);
                this.Location = new Point(lx, ly);

                n = root.SelectSingleNode("LastPreset");
                if (n != null && !string.IsNullOrEmpty(n.InnerText))
                {
                    lastPresetName = n.InnerText;
                    string presetPath = Path.Combine(GetPresetsDir(), n.InnerText + ".preset.xml");
                    if (File.Exists(presetPath))
                        LoadSettingsFrom(presetPath);
                }

                string savedDPS = "";
                string savedHPS = "";
                string savedDTPS = "";
                n = root.SelectSingleNode("DPSHandle");
                if (n != null && !string.IsNullOrEmpty(n.InnerText)) savedDPS = n.InnerText;
                n = root.SelectSingleNode("HPSHandle");
                if (n != null && !string.IsNullOrEmpty(n.InnerText)) savedHPS = n.InnerText;
                n = root.SelectSingleNode("DTPSHandle");
                if (n != null && !string.IsNullOrEmpty(n.InnerText)) savedDTPS = n.InnerText;

                if (!string.IsNullOrEmpty(savedDPS) || !string.IsNullOrEmpty(savedHPS) || !string.IsNullOrEmpty(savedDTPS))
                {
                    Timer handleTimer = new Timer();
                    handleTimer.Interval = 200;
                    handleTimer.Tick += (s2, e2) =>
                    {
                        handleTimer.Stop();
                        handleTimer.Dispose();
                        if (!string.IsNullOrEmpty(savedDPS)) SetDPSHandle(savedDPS);
                        if (!string.IsNullOrEmpty(savedHPS)) SetHPSHandle(savedHPS);
                        if (!string.IsNullOrEmpty(savedDTPS)) SetDTPSHandle(savedDTPS);
                    };
                    handleTimer.Start();
                }
            }
            catch { }
        }
    }

    public class ChildOverlay : Form
    {
        public enum ChildMode { DPSOnly, HPSOnly, Both, DTPSOnly }
        public ChildMode Mode { get; private set; }
        public string DPSHandleValue { get { return dpsHandle; } }
        public string HPSHandleValue { get { return hpsHandle; } }

        public int TileNumber { get; set; }
        public int RowLimit { get; set; }
        private Label lblTileNumber;
        public void UpdateTileNumberLabel()
        {
            if (lblTileNumber != null)
                lblTileNumber.Text = TileNumber.ToString();
            UpdateTaskbarIcon();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            MiniOverlay.SetAppUserModelId(this.Handle, "CO.MiniParse.Overlay");
        }

        private void UpdateTaskbarIcon()
        {
            try
            {
                Color iconColor;
                if (Mode == ChildMode.HPSOnly) iconColor = MY_HPS_ACCENT;
                else if (Mode == ChildMode.DTPSOnly) iconColor = MY_DTPS_ACCENT;
                else iconColor = MY_DPS_ACCENT;

                int sz = 64;
                Bitmap bmp = new Bitmap(sz, sz);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(30, 30, 30));
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    Font numFont = new Font("Consolas", 36F, FontStyle.Bold);
                    string txt = TileNumber.ToString();
                    SizeF sizef = g.MeasureString(txt, numFont);
                    float x = (sz - sizef.Width) / 2;
                    float y = (sz - sizef.Height) / 2;
                    using (Brush b = new SolidBrush(iconColor))
                        g.DrawString(txt, numFont, b, x, y);
                }
                this.Icon = Icon.FromHandle(bmp.GetHicon());

                string modeName = Mode == ChildMode.DPSOnly ? "Tracked DPS" : (Mode == ChildMode.HPSOnly ? "Tracked HPS" : (Mode == ChildMode.DTPSOnly ? "Tracked DTPS" : "Tracked"));
                this.Text = modeName + " " + TileNumber;
            }
            catch { }
        }

        public void SetHandles(string dps, string hps)
        {
            if (!string.IsNullOrEmpty(dps) && txtDPSHandle != null)
            {
                dpsHandle = dps;
                txtDPSHandle.Text = dps;
                txtDPSHandle.ForeColor = TEXT_COLOR;
                txtDPSHandle.Font = new Font("Consolas", 8F, FontStyle.Regular);
            }
            if (!string.IsNullOrEmpty(hps) && txtHPSHandle != null)
            {
                hpsHandle = hps;
                txtHPSHandle.Text = hps;
                txtHPSHandle.ForeColor = TEXT_COLOR;
                txtHPSHandle.Font = new Font("Consolas", 8F, FontStyle.Regular);
            }
        }

        private MiniOverlay parent;
        private Timer updateTimer;
        private Panel panelDPS, panelHPS;
        private Label lblDPSHeader, lblHPSHeader;
        private Label lblDPSContent, lblHPSContent;
        private TextBox txtDPSHandle, txtHPSHandle;
        private string dpsHandle = "";
        private string hpsHandle = "";
        private bool isDragging = false;
        private Point dragOffset;

        private const string PLACEHOLDER_DPS = "name or @handle...";
        private const string PLACEHOLDER_HPS = "name or @handle...";

        private static readonly Color BG_COLOR = Color.FromArgb(30, 30, 30);
        private static readonly Color HEADER_BG = Color.FromArgb(50, 50, 50);
        private static readonly Color DIVIDER_COLOR = Color.FromArgb(70, 70, 70);
        private static readonly Color TEXT_COLOR = Color.FromArgb(230, 230, 230);
        private static readonly Color PLACEHOLDER_COLOR = Color.FromArgb(120, 120, 120);
        private static readonly Color MY_DPS_ACCENT = Color.FromArgb(255, 100, 100);
        private static readonly Color MY_HPS_ACCENT = Color.FromArgb(120, 230, 120);
        private static readonly Color MY_DTPS_ACCENT = Color.FromArgb(120, 240, 240);

        public ChildOverlay(MiniOverlay parent, ChildMode mode = ChildMode.Both)
        {
            this.parent = parent;
            this.Mode = mode;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = BG_COLOR;
            this.TopMost = true;
            this.ShowInTaskbar = true;
            this.DoubleBuffered = true;
            this.Text = mode == ChildMode.DPSOnly ? "Tracked DPS" : (mode == ChildMode.HPSOnly ? "Tracked HPS" : (mode == ChildMode.DTPSOnly ? "Tracked DTPS" : "Tracked"));

            bool showDPS = mode == ChildMode.DPSOnly || mode == ChildMode.Both || mode == ChildMode.DTPSOnly;
            bool showHPS = mode == ChildMode.HPSOnly || mode == ChildMode.Both;

            if (mode == ChildMode.Both)
            {
                this.Size = new Size(500, 300);
                this.MinimumSize = new Size(200, 150);
            }
            else
            {
                this.Size = new Size(250, 300);
                this.MinimumSize = new Size(150, 150);
            }

            List<Control> dragTargets = new List<Control>();

            if (showDPS)
            {
                panelDPS = new Panel();
                panelDPS.BackColor = Color.Transparent;

                lblDPSHeader = new Label();
                lblDPSHeader.Text = mode == ChildMode.DTPSOnly ? "Tracked DTPS" : "Tracked DPS";
                lblDPSHeader.Dock = DockStyle.Top;
                lblDPSHeader.Height = 20;
                lblDPSHeader.BackColor = HEADER_BG;
                lblDPSHeader.ForeColor = mode == ChildMode.DTPSOnly ? MY_DTPS_ACCENT : MY_DPS_ACCENT;
                lblDPSHeader.Font = new Font("Consolas", 9F, FontStyle.Bold);
                lblDPSHeader.TextAlign = ContentAlignment.MiddleCenter;

                txtDPSHandle = CreateHandleTextBox(PLACEHOLDER_DPS, true);

                lblDPSContent = new MiniOverlay.MultiColorLabel();
                lblDPSContent.Dock = DockStyle.Fill;
                lblDPSContent.ForeColor = TEXT_COLOR;
                lblDPSContent.Font = new Font("Consolas", 8.5f);
                lblDPSContent.BackColor = Color.Transparent;
                lblDPSContent.Text = "  No data";
                lblDPSContent.Padding = new Padding(4, 4, 4, 4);

                panelDPS.Controls.Add(lblDPSContent);
                panelDPS.Controls.Add(txtDPSHandle);
                panelDPS.Controls.Add(lblDPSHeader);

                dragTargets.Add(panelDPS);
                dragTargets.Add(lblDPSHeader);
                dragTargets.Add(lblDPSContent);
            }

            if (showHPS)
            {
                panelHPS = new Panel();
                panelHPS.BackColor = Color.Transparent;

                lblHPSHeader = new Label();
                lblHPSHeader.Text = "Tracked HPS";
                lblHPSHeader.Dock = DockStyle.Top;
                lblHPSHeader.Height = 20;
                lblHPSHeader.BackColor = HEADER_BG;
                lblHPSHeader.ForeColor = MY_HPS_ACCENT;
                lblHPSHeader.Font = new Font("Consolas", 9F, FontStyle.Bold);
                lblHPSHeader.TextAlign = ContentAlignment.MiddleCenter;

                txtHPSHandle = CreateHandleTextBox(PLACEHOLDER_HPS, false);

                lblHPSContent = new MiniOverlay.MultiColorLabel();
                lblHPSContent.Dock = DockStyle.Fill;
                lblHPSContent.ForeColor = TEXT_COLOR;
                lblHPSContent.Font = new Font("Consolas", 8.5f);
                lblHPSContent.BackColor = Color.Transparent;
                lblHPSContent.Text = "  No data";
                lblHPSContent.Padding = new Padding(4, 4, 4, 4);

                panelHPS.Controls.Add(lblHPSContent);
                panelHPS.Controls.Add(txtHPSHandle);
                panelHPS.Controls.Add(lblHPSHeader);

                dragTargets.Add(panelHPS);
                dragTargets.Add(lblHPSHeader);
                dragTargets.Add(lblHPSContent);
            }

            Button btnClose = new Button();
            btnClose.Text = "X";
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderColor = Color.FromArgb(200, 80, 80);
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.BackColor = Color.FromArgb(60, 60, 60);
            btnClose.ForeColor = Color.FromArgb(200, 80, 80);
            btnClose.Font = new Font("Consolas", 8F, FontStyle.Bold);
            btnClose.Size = new Size(22, 20);
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, ev) => { this.Close(); };

            Panel closePanel = new Panel();
            closePanel.Dock = DockStyle.Top;
            closePanel.Height = 22;
            closePanel.BackColor = HEADER_BG;
            btnClose.Dock = DockStyle.Right;
            closePanel.Controls.Add(btnClose);

            lblTileNumber = new Label();
            lblTileNumber.Text = "";
            Color tileNumColor;
            if (mode == ChildMode.HPSOnly) tileNumColor = MY_HPS_ACCENT;
            else if (mode == ChildMode.DTPSOnly) tileNumColor = MY_DTPS_ACCENT;
            else tileNumColor = MY_DPS_ACCENT;
            lblTileNumber.ForeColor = tileNumColor;
            lblTileNumber.Font = new Font("Consolas", 9F, FontStyle.Bold);
            lblTileNumber.Dock = DockStyle.Left;
            lblTileNumber.Width = 30;
            lblTileNumber.TextAlign = ContentAlignment.MiddleCenter;
            closePanel.Controls.Add(lblTileNumber);

            Panel gripPanel = new Panel();
            gripPanel.Size = new Size(16, 16);
            gripPanel.BackColor = Color.FromArgb(45, 45, 45);
            gripPanel.Cursor = Cursors.SizeNWSE;
            gripPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            bool isResizing = false;
            Point resizeStart = Point.Empty;
            Size resizeOrigSize = Size.Empty;
            gripPanel.MouseDown += (s, ev) =>
            {
                if (ev.Button == MouseButtons.Left)
                {
                    isResizing = true;
                    resizeStart = gripPanel.PointToScreen(ev.Location);
                    resizeOrigSize = this.Size;
                }
            };
            gripPanel.MouseMove += (s, ev) =>
            {
                if (isResizing)
                {
                    Point cur = gripPanel.PointToScreen(ev.Location);
                    int newW = resizeOrigSize.Width + (cur.X - resizeStart.X);
                    int newH = resizeOrigSize.Height + (cur.Y - resizeStart.Y);
                    if (newW < this.MinimumSize.Width) newW = this.MinimumSize.Width;
                    if (newH < this.MinimumSize.Height) newH = this.MinimumSize.Height;
                    this.Size = new Size(newW, newH);
                }
            };
            gripPanel.MouseUp += (s, ev) => { isResizing = false; };
            typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(gripPanel, true, null);
            gripPanel.Resize += (s, ev) => { gripPanel.Invalidate(); };
            gripPanel.Paint += (s, pe) =>
            {
                int gx = gripPanel.Width - 16;
                int gy = 2;
                using (Pen p = new Pen(Color.FromArgb(140, 140, 140), 2))
                {
                    for (int line = 0; line < 4; line++)
                    {
                        pe.Graphics.DrawLine(p, gx + line * 4, gripPanel.Height - 2, gripPanel.Width - 2, gy + line * 4);
                    }
                }
            };

            if (mode == ChildMode.Both)
            {
                Panel divider = new Panel();
                divider.Dock = DockStyle.Left;
                divider.Width = 2;
                divider.BackColor = DIVIDER_COLOR;

                panelDPS.Dock = DockStyle.Left;
                panelDPS.Width = this.ClientSize.Width / 2 - 1;
                panelHPS.Dock = DockStyle.Fill;

                this.Controls.Add(panelHPS);
                this.Controls.Add(divider);
                this.Controls.Add(panelDPS);

                this.Resize += (s, ev) => { panelDPS.Width = this.ClientSize.Width / 2 - 1; };
            }
            else if (mode == ChildMode.DPSOnly || mode == ChildMode.DTPSOnly)
            {
                panelDPS.Dock = DockStyle.Fill;
                this.Controls.Add(panelDPS);
            }
            else
            {
                panelHPS.Dock = DockStyle.Fill;
                this.Controls.Add(panelHPS);
            }

            gripPanel.Location = new Point(this.ClientSize.Width - 16, this.ClientSize.Height - 16);
            this.Controls.Add(gripPanel);
            gripPanel.BringToFront();
            this.Controls.Add(closePanel);
            this.Resize += (s2, ev2) => { gripPanel.Location = new Point(this.ClientSize.Width - 16, this.ClientSize.Height - 16); AutoFitFonts(); };

            foreach (Control c in dragTargets)
            {
                c.MouseDown += (s, ev) =>
                {
                    if (ev.Button == MouseButtons.Left)
                    {
                        isDragging = true;
                        dragOffset = ev.Location;
                        closePanel.Focus();
                    }
                };
                c.MouseMove += (s, ev) => { if (isDragging) { Point p = PointToScreen(ev.Location); this.Location = new Point(p.X - dragOffset.X, p.Y - dragOffset.Y); } };
                c.MouseUp += (s, ev) => { isDragging = false; };
            }

            updateTimer = new Timer();
            updateTimer.Interval = 1000;
            updateTimer.Tick += (s, ev) => UpdateData();
            updateTimer.Start();
        }

        private TextBox CreateHandleTextBox(string placeholder, bool isDPS)
        {
            TextBox txt = new TextBox();
            txt.Dock = DockStyle.Top;
            txt.Height = 20;
            txt.BackColor = Color.FromArgb(40, 40, 40);
            txt.ForeColor = PLACEHOLDER_COLOR;
            txt.Font = new Font("Consolas", 8F, FontStyle.Italic);
            txt.Text = placeholder;
            txt.BorderStyle = BorderStyle.FixedSingle;

            txt.Enter += (s, ev) =>
            {
                if (txt.Text == placeholder)
                {
                    txt.Text = "";
                    txt.ForeColor = TEXT_COLOR;
                    txt.Font = new Font("Consolas", 8F, FontStyle.Regular);
                }
            };
            txt.Leave += (s, ev) =>
            {
                if (string.IsNullOrEmpty(txt.Text))
                {
                    txt.Text = placeholder;
                    txt.ForeColor = PLACEHOLDER_COLOR;
                    txt.Font = new Font("Consolas", 8F, FontStyle.Italic);
                }
            };
            txt.TextChanged += (s, ev) =>
            {
                if (txt.Text != placeholder)
                {
                    if (isDPS) dpsHandle = txt.Text;
                    else hpsHandle = txt.Text;
                }
            };

            return txt;
        }

        private void UpdateData()
        {
            if (!this.Visible || parent == null) return;

            EncounterData encounter = parent.GetCurrentEncounter();
            if (encounter == null)
            {
                if (lblDPSContent != null) lblDPSContent.Text = "  No data";
                if (lblHPSContent != null) lblHPSContent.Text = "  No data";
                return;
            }

            List<CombatantData> combatants = new List<CombatantData>();
            try
            {
                foreach (CombatantData cd in encounter.Items.Values)
                    combatants.Add(cd);
            }
            catch { }

            if (lblDPSContent != null)
            {
                CombatantData dpsChar = parent.FindMyCharacter(combatants, dpsHandle);
                if (dpsChar != null)
                {
                    if (Mode == ChildMode.DTPSOnly)
                    {
                        lblDPSHeader.Text = dpsChar.Name.Trim() + "'s Tracked DTPS";
                        lblDPSContent.Text = parent.FormatPowerBreakdown(dpsChar, MiniOverlay.PowerBreakdownMode.DTPS, encounter, lblDPSContent.Width, RowLimit);
                    }
                    else
                    {
                        lblDPSHeader.Text = dpsChar.Name.Trim() + "'s Tracked DPS";
                        lblDPSContent.Text = parent.FormatPowerBreakdown(dpsChar, true, encounter, lblDPSContent.Width, RowLimit);
                    }
                }
                else
                {
                    lblDPSHeader.Text = Mode == ChildMode.DTPSOnly ? "Tracked DTPS" : "Tracked DPS";
                    string hint = string.IsNullOrEmpty(dpsHandle) ? (Mode == ChildMode.DTPSOnly ? " (set DTPS handle)" : " (set DPS handle)") : "";
                    lblDPSContent.Text = "  No char found" + hint;
                }
            }

            if (lblHPSContent != null)
            {
                CombatantData hpsChar = parent.FindMyCharacter(combatants, hpsHandle);
                if (hpsChar != null)
                {
                    lblHPSHeader.Text = hpsChar.Name.Trim() + "'s Tracked HPS";
                    lblHPSContent.Text = parent.FormatPowerBreakdown(hpsChar, false, encounter, lblHPSContent.Width, RowLimit);
                }
                else
                {
                    lblHPSHeader.Text = "Tracked HPS";
                    string hint = string.IsNullOrEmpty(hpsHandle) ? " (set HPS handle)" : "";
                    lblHPSContent.Text = "  No char found" + hint;
                }
            }

            AutoFitFonts();
        }

        private float maxFontSize = 48f;

        private void AutoFitLabel(Label lbl, bool isDPS)
        {
            if (lbl == null || string.IsNullOrEmpty(lbl.Text)) return;
            int rawWidth = lbl.Width - lbl.Padding.Left - lbl.Padding.Right;
            int availWidth = rawWidth - 20;
            if (availWidth < 20) { lbl.Font = new Font("Consolas", maxFontSize); return; }

            string longestLine = "";
            foreach (string line in lbl.Text.Split('\n'))
                if (line.Length > longestLine.Length) longestLine = line;
            if (longestLine.Length == 0) { lbl.Font = new Font("Consolas", maxFontSize); return; }

            float bestSize = 3f;
            try
            {
                for (float sz = maxFontSize; sz >= 3f; sz -= 0.5f)
                {
                    using (Font f = new Font("Consolas", sz))
                    {
                        int w = TextRenderer.MeasureText(longestLine, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                        if (w <= availWidth) { bestSize = sz; break; }
                    }
                }
            }
            catch { bestSize = maxFontSize; }
            if (Math.Abs(lbl.Font.Size - bestSize) > 0.1f)
                lbl.Font = new Font("Consolas", bestSize);
        }

        private void AutoFitFonts()
        {
            AutoFitLabel(lblDPSContent, true);
            AutoFitLabel(lblHPSContent, false);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (updateTimer != null) { updateTimer.Stop(); updateTimer.Dispose(); }
            base.OnFormClosing(e);
        }
    }
}
