using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
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
        private Panel dividerMend;

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

        public string DPSHandle { get; set; }
        public string HPSHandle { get; set; }
        // Keep CharacterHandle for backward compat with COParser settings
        public string CharacterHandle
        {
            get { return DPSHandle; }
            set { DPSHandle = value; HPSHandle = value; }
        }
        public event EventHandler HandleChanged;
        private void OnHandleChanged()
        {
            if (HandleChanged != null) HandleChanged(this, EventArgs.Empty);
        }
        public void SetHandle(string handle)
        {
            SetDPSHandle(handle);
            SetHPSHandle(handle);
        }
        private const string PLACEHOLDER_DPS = "type DPS handle here...";
        private const string PLACEHOLDER_HPS = "type HPS handle here...";
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

        private const int PANEL_WIDTH = 220;
        private const int HEADER_HEIGHT = 20;
        private const int DIVIDER_WIDTH = 2;
        private const int UPDATE_INTERVAL_MS = 1000;

        private static readonly Color BG_COLOR = Color.FromArgb(220, 30, 30, 30);
        private static readonly Color HEADER_BG = Color.FromArgb(255, 50, 50, 50);
        private static readonly Color DIVIDER_COLOR = Color.FromArgb(255, 70, 70, 70);
        private static readonly Color TEXT_COLOR = Color.FromArgb(255, 230, 230, 230);
        private static readonly Color HEADER_TEXT = Color.FromArgb(255, 200, 200, 200);
        private static readonly Color DPS_ACCENT = Color.FromArgb(255, 220, 60, 60);
        private static readonly Color HPS_ACCENT = Color.FromArgb(255, 80, 200, 80);
        private static readonly Color BAR_DPS_COLOR = Color.FromArgb(80, 220, 150, 50);
        private static readonly Color BAR_HPS_COLOR = Color.FromArgb(80, 80, 200, 80);
        private static readonly Color ENC_TITLE_COLOR = Color.FromArgb(255, 160, 160, 160);
        private static readonly Color MY_DPS_ACCENT = Color.FromArgb(255, 255, 100, 100);
        private static readonly Color MY_HPS_ACCENT = Color.FromArgb(255, 120, 230, 120);

        public MiniOverlay()
        {
            InitializeOverlay();
            SetupTimer();
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
            this.Opacity = 1.0;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.Text = "CO Mini Parse";

            try
            {
                Guid IID_IPropertyStore = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
                object store;
                SHGetPropertyStoreForWindow(this.Handle, ref IID_IPropertyStore, out store);
                if (store != null)
                {
                    PropVariant pv = new PropVariant("CO.MiniParse.Overlay");
                    PropertyKey pk = new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
                    ((IPropertyStore)store).SetValue(ref pk, ref pv);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(store);
                }
            }
            catch { }

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
            this.MinimumSize = new Size(420, 400);


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
            dividerTop.BackColor = DIVIDER_COLOR;

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
            lblMendEmoticon.Height = 50;
            lblMendEmoticon.TextAlign = ContentAlignment.BottomLeft;
            lblMendEmoticon.Padding = new Padding(4, 0, 0, 2);
            lblMendEmoticon.BackColor = Color.Transparent;
            lblMendEmoticon.Visible = false;

            panelMend.Controls.Add(lblMendContent);
            panelMend.Controls.Add(lblMendEmoticon);
            panelMend.Controls.Add(lblMendHeader);

            dividerMend = new Panel();
            dividerMend.Dock = DockStyle.Top;
            dividerMend.Height = DIVIDER_WIDTH;
            dividerMend.BackColor = DIVIDER_COLOR;

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
            btnMaximize.Click += (s, ev) =>
            {
                if (this.WindowState == FormWindowState.Maximized)
                    this.WindowState = FormWindowState.Normal;
                else
                    this.WindowState = FormWindowState.Maximized;
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
                new TileInfo("Mend Tracker", panelMend),
                new TileInfo("Tracked DPS", panelMyDPS),
                new TileInfo("Tracked HPS", panelMyHPS)
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

            this.MinimumSize = new Size(420, 200);

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
            Label lbl = new Label();
            lbl.Text = "";
            lbl.ForeColor = TEXT_COLOR;
            lbl.Font = new Font("Consolas", 8.5F, FontStyle.Regular);
            lbl.TextAlign = ContentAlignment.TopLeft;
            lbl.Padding = new Padding(4, 2, 4, 2);
            lbl.AutoSize = false;
            return lbl;
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
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private void SetOpacitySafe(double opacity)
        {
            if (opacity < 0.4) opacity = 0.4;
            if (opacity > 1.0) opacity = 1.0;
            currentOpacityPct = (int)(opacity * 100);
            this.Opacity = opacity;
        }
        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid,
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Interface)] out object propertyStore);

        [System.Runtime.InteropServices.ComImport]
        [System.Runtime.InteropServices.Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PropertyKey pkey);
            void GetValue(ref PropertyKey key, out PropVariant pv);
            void SetValue(ref PropertyKey key, ref PropVariant pv);
            void Commit();
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
        private struct PropertyKey
        {
            public Guid fmtid;
            public uint pid;
            public PropertyKey(Guid fmtid, uint pid) { this.fmtid = fmtid; this.pid = pid; }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PropVariant
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

        private void UpdateOverlayData()
        {
            if (!this.Visible) return;

            EncounterData encounter = GetCurrentEncounter();
            if (encounter == null)
            {
                lblDPSEncTitle.Text = "";
                lblHPSEncTitle.Text = "";
                lblDPSContent.Text = "  No encounter data";
                lblHPSContent.Text = "  No encounter data";
                lblMyDPSContent.Text = "  No data";
                lblMyHPSContent.Text = "  No data";
                lblMendContent.Text = "  No Mend detected";
                if (lblMendEmoticon != null) lblMendEmoticon.Visible = false;
                return;
            }

            string encTitle = encounter.Title;
            string duration = encounter.DurationS;
            string titleText = encTitle + " (" + duration + ")";
            lblDPSEncTitle.Text = titleText;
            lblHPSEncTitle.Text = titleText;

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

            string dpsText = FormatCombatantList(dpsSorted, true, lblDPSContent);
            string hpsText = FormatCombatantList(hpsSorted, false, lblHPSContent);
            lblDPSContent.Text = dpsText;
            lblHPSContent.Text = hpsText;

            CombatantData dpsChar = FindMyCharacter(combatants, DPSHandle);
            if (dpsChar != null)
            {
                lblMyDPSHeader.Text = dpsChar.Name.Trim() + "'s Tracked DPS";
                lblMyDPSContent.Text = FormatPowerBreakdown(dpsChar, true, encounter, lblMyDPSContent.Width);
            }
            else
            {
                lblMyDPSHeader.Text = "Tracked DPS";
                string hint = string.IsNullOrEmpty(DPSHandle) ? " (set DPS handle above)" : "";
                lblMyDPSContent.Text = "  No char found" + hint;
            }

            CombatantData hpsChar = FindMyCharacter(combatants, HPSHandle);
            if (hpsChar != null)
            {
                lblMyHPSHeader.Text = hpsChar.Name.Trim() + "'s Tracked HPS";
                lblMyHPSContent.Text = FormatPowerBreakdown(hpsChar, false, encounter, lblMyHPSContent.Width);
            }
            else
            {
                lblMyHPSHeader.Text = "Tracked HPS";
                string hint = string.IsNullOrEmpty(HPSHandle) ? " (set HPS handle above)" : "";
                lblMyHPSContent.Text = "  No char found" + hint;
            }

            UpdateMendTracker(encounter);
        }

        private void UpdateMendTracker(EncounterData encounter)
        {
            Dictionary<string, COParser.MendData> mendData = COParser.GetMendData();
            if (mendData.Count == 0)
            {
                lblMendContent.Text = "  No Mend detected";
                if (lblMendEmoticon != null) lblMendEmoticon.Visible = false;
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

            int mendPanelWidth = lblMendContent.Width;
            int wHps = 6, wMax = 6, wMin = 6;

            try
            {
                Label measLabel = lblMendContent;
                Font f = measLabel.Font;
                int availWidth = mendPanelWidth - 10;
                using (Graphics g = measLabel.CreateGraphics())
                {
                    while (nameW > 3)
                    {
                        string test = " " + new string('X', nameW) + "\u2502" + new string('X', wHps) + "\u2502" + new string('X', wMax) + "\u2502" + new string('X', wMin);
                        if (g.MeasureString(test, f).Width <= availWidth) break;
                        nameW--;
                    }
                    while (wMin > 3)
                    {
                        string test = " " + new string('X', nameW) + "\u2502" + new string('X', wHps) + "\u2502" + new string('X', wMax) + "\u2502" + new string('X', wMin);
                        if (g.MeasureString(test, f).Width <= availWidth) break;
                        wMin--;
                    }
                    while (wMax > 3)
                    {
                        string test = " " + new string('X', nameW) + "\u2502" + new string('X', wHps) + "\u2502" + new string('X', wMax) + "\u2502" + new string('X', wMin);
                        if (g.MeasureString(test, f).Width <= availWidth) break;
                        wMax--;
                    }
                    while (wHps > 3)
                    {
                        string test = " " + new string('X', nameW) + "\u2502" + new string('X', wHps) + "\u2502" + new string('X', wMax) + "\u2502" + new string('X', wMin);
                        if (g.MeasureString(test, f).Width <= availWidth) break;
                        wHps--;
                    }
                }
            }
            catch { }
            if (nameW < 3) nameW = 3;

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

            lblMendContent.Text = result;

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
            }
        }

        private CombatantData FindMyCharacter(List<CombatantData> combatants, string handle)
        {
            if (!string.IsNullOrEmpty(handle))
            {
                string displayName = COParser.GetNameForHandle(handle);
                if (displayName != null)
                {
                    foreach (CombatantData cd in combatants)
                    {
                        if (cd.Name.Trim().Equals(displayName.Trim(), StringComparison.OrdinalIgnoreCase))
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

        private string FormatPowerBreakdown(CombatantData myData, bool isDPS, EncounterData encounter, int panelWidth)
        {
            string result = "";
            List<PowerEntry> powers = new List<PowerEntry>();
            double encDuration = encounter.Duration.TotalSeconds;
            if (encDuration < 1) encDuration = 1;

            try
            {
                string typeKey = isDPS ? "Outgoing Damage" : "Healing (Out)";

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

                    int hits = at.Hits;
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
                    pe.Hits = hits;
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
            foreach (PowerEntry pe in powers)
                total += pe.Value;
            if (total == 0) total = 1;

            string psLabel = isDPS ? "DPS" : "HPS";

            int longestName = 5;
            foreach (PowerEntry pe in powers)
                if (pe.Name.Length > longestName) longestName = pe.Name.Length;
            int nameWidth = Math.Min(longestName, 30);

            int wPs = 5;
            int wPct = 4;
            int wMax = 6;
            int wLow = 6;

            try
            {
                Label measLabel = isDPS ? lblMyDPSContent : lblMyHPSContent;
                Font f = measLabel.Font;
                int availWidth = panelWidth - 10;

                using (Graphics g = measLabel.CreateGraphics())
                {
                    while (nameWidth > 3)
                    {
                        string test = " " + new string('X', nameWidth) + "\u2502" + new string('X', wPs) + "\u2502" + new string('X', wPct) + "\u2502" + new string('X', wMax) + "\u2502" + new string('X', wLow);
                        if (g.MeasureString(test, f).Width <= availWidth) break;
                        nameWidth--;
                    }
                    while (wLow > 3)
                    {
                        string test = " " + new string('X', nameWidth) + "\u2502" + new string('X', wPs) + "\u2502" + new string('X', wPct) + "\u2502" + new string('X', wMax) + "\u2502" + new string('X', wLow);
                        if (g.MeasureString(test, f).Width <= availWidth) break;
                        wLow--;
                    }
                    while (wMax > 3)
                    {
                        string test = " " + new string('X', nameWidth) + "\u2502" + new string('X', wPs) + "\u2502" + new string('X', wPct) + "\u2502" + new string('X', wMax) + "\u2502" + new string('X', wLow);
                        if (g.MeasureString(test, f).Width <= availWidth) break;
                        wMax--;
                    }
                    while (wPct > 2)
                    {
                        string test = " " + new string('X', nameWidth) + "\u2502" + new string('X', wPs) + "\u2502" + new string('X', wPct) + "\u2502" + new string('X', wMax) + "\u2502" + new string('X', wLow);
                        if (g.MeasureString(test, f).Width <= availWidth) break;
                        wPct--;
                    }
                    while (wPs > 3)
                    {
                        string test = " " + new string('X', nameWidth) + "\u2502" + new string('X', wPs) + "\u2502" + new string('X', wPct) + "\u2502" + new string('X', wMax) + "\u2502" + new string('X', wLow);
                        if (g.MeasureString(test, f).Width <= availWidth) break;
                        wPs--;
                    }
                }
            }
            catch { }
            if (nameWidth < 3) nameWidth = 3;

            string hdrPower = ClipText("Power", nameWidth);
            string hdrPs = ClipText(psLabel, wPs);
            string hdrPct = ClipText("%", wPct);
            string hdrMaxHit = ClipText("MaxHit", wMax);
            string hdrLowHit = ClipText("LowHit", wLow);

            result += String.Format(" {0,-" + nameWidth + "}\u2502{1," + wPs + "}\u2502{2," + wPct + "}\u2502{3," + wMax + "}\u2502{4," + wLow + "}\r\n",
                hdrPower, hdrPs, hdrPct, hdrMaxHit, hdrLowHit);
            result += " " + new string('\u2500', nameWidth) + "\u253C" + new string('\u2500', wPs) + "\u253C" + new string('\u2500', wPct) + "\u253C" + new string('\u2500', wMax) + "\u253C" + new string('\u2500', wLow) + "\r\n";

            int maxRows = 15;
            int count = 0;
            foreach (PowerEntry pe in powers)
            {
                if (count >= maxRows) break;

                string name = ClipText(pe.Name, nameWidth);
                double pct = (pe.Value * 100.0) / total;

                result += String.Format(" {0,-" + nameWidth + "}\u2502{1," + wPs + "}\u2502{2," + wPct + "}\u2502{3," + wMax + "}\u2502{4," + wLow + "}\r\n",
                    name,
                    ClipText(FormatNumber(pe.PerSecond), wPs),
                    ClipText(((int)pct).ToString() + "%", wPct),
                    ClipText(FormatNumber(pe.HighestTick), wMax),
                    ClipText(FormatNumber(pe.LowestTick), wLow));
                count++;
            }

            return result;
        }

        private class PowerEntry
        {
            public string Name;
            public long Value;
            public double PerSecond;
            public int Hits;
            public int HighestTick;
            public int LowestTick;
        }

        private string FormatCombatantList(List<CombatantData> combatants, bool isDPS, Label contentLabel)
        {
            if (combatants.Count == 0)
                return "  No data";

            long total = 0;
            foreach (CombatantData cd in combatants)
            {
                if (isDPS)
                    total += cd.Damage;
                else if (cd.Healed > 0)
                    total += cd.Healed;
            }
            if (total == 0) total = 1;

            int wName = 10, wVal = 6, wPct = 4;
            try
            {
                Font f = contentLabel.Font;
                int availWidth = contentLabel.Width - 10;
                using (Graphics g = contentLabel.CreateGraphics())
                {
                    while (wName > 3)
                    {
                        string test = " " + new string('X', wName) + " " + new string('X', wVal) + " " + new string('X', wPct);
                        if (g.MeasureString(test, f).Width <= availWidth) break;
                        wName--;
                    }
                    while (wVal > 3)
                    {
                        string test = " " + new string('X', wName) + " " + new string('X', wVal) + " " + new string('X', wPct);
                        if (g.MeasureString(test, f).Width <= availWidth) break;
                        wVal--;
                    }
                }
            }
            catch { }
            if (wName < 3) wName = 3;

            string result = "";
            int maxRows = 20;
            int count = 0;

            foreach (CombatantData cd in combatants)
            {
                if (count >= maxRows) break;

                string name = ClipText(cd.Name, wName);

                if (isDPS)
                {
                    double dps = cd.EncDPS;
                    double pct = (cd.Damage * 100.0) / total;
                    result += String.Format(" {0,-" + wName + "} {1," + wVal + "} {2," + (wPct - 1) + ":0}%\r\n",
                        name,
                        ClipText(FormatNumber(dps), wVal),
                        pct);
                }
                else
                {
                    long healed = cd.Healed;
                    double hps = cd.EncHPS;

                    if (healed <= 0) continue;

                    double pct = (healed * 100.0) / total;
                    result += String.Format(" {0,-" + wName + "} {1," + wVal + "} {2," + (wPct - 1) + ":0}%\r\n",
                        name,
                        ClipText(FormatNumber(hps), wVal),
                        pct);
                }
                count++;
            }

            return result;
        }

        private static string TruncLine(string line, int maxChars)
        {
            return line.Length > maxChars ? line.Substring(0, maxChars) : line;
        }

        private int CalcContentHeight(string text, int headerHeight)
        {
            if (string.IsNullOrEmpty(text)) return headerHeight + 20;
            int lines = 1;
            foreach (char c in text) { if (c == '\n') lines++; }
            return headerHeight + (lines * 15) + 6;
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

        private float[] tileFontSizes = { 8.5f, 8.5f, 8.5f, 8.5f, 8.5f };

        private void ApplyFontSize(int tileIdx, float size)
        {
            tileFontSizes[tileIdx] = size;
            Label[] contentLabels = { lblDPSContent, lblHPSContent, lblMendContent, lblMyDPSContent, lblMyHPSContent };
            contentLabels[tileIdx].Font = new Font("Consolas", size, FontStyle.Regular);
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
                slider.Minimum = 6;
                slider.Maximum = 16;
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
            popup.Deactivate += (s, ev) => { popup.Close(); };

            Button[] buttons = new Button[] {
                CreateSettingsButton("Layout", Color.FromArgb(255, 220, 150, 50)),
                CreateSettingsButton("Tiles", Color.FromArgb(255, 100, 180, 220)),
                CreateSettingsButton("Opacity", Color.FromArgb(255, 80, 200, 80)),
                CreateSettingsButton("Text Size", Color.FromArgb(255, 180, 130, 255)),
                CreateSettingsButton("Presets", Color.FromArgb(255, 50, 80, 160))
            };

            buttons[0].Click += (s, ev) =>
            {
                if (currentLayout == LayoutMode.Main) ApplyLayout(LayoutMode.Vertical);
                else if (currentLayout == LayoutMode.Vertical) ApplyLayout(LayoutMode.Horizontal);
                else ApplyLayout(LayoutMode.Main);
            };
            buttons[1].Click += (s, ev) => { Point loc = popup.Location; popup.Close(); ShowTilesPopup(loc); };
            buttons[2].Click += (s, ev) => { Point loc = popup.Location; popup.Close(); ShowOpacityPopup(loc); };
            buttons[3].Click += (s, ev) => { Point loc = popup.Location; popup.Close(); ShowTextSizePopup(loc); };
            buttons[4].Click += (s, ev) => { Point loc = popup.Location; popup.Close(); ShowPresetsPopup(loc); };

            int y = 4;
            int btnW = 90;
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
                if (txtDPSHandle != null && txtDPSHandle.Text != PLACEHOLDER_DPS)
                    dpsH = txtDPSHandle.Text ?? "";
                if (txtHPSHandle != null && txtHPSHandle.Text != PLACEHOLDER_HPS)
                    hpsH = txtHPSHandle.Text ?? "";
                xw.WriteElementString("DPSHandle", dpsH);
                xw.WriteElementString("HPSHandle", hpsH);

                if (tileOrder != null)
                {
                    foreach (TileInfo ti in tileOrder)
                        xw.WriteElementString("Tile_" + ti.Name.Replace(" ", "_"), ti.Visible.ToString());
                }

                for (int i = 0; i < tileFontSizes.Length; i++)
                    xw.WriteElementString("FontSize_" + i, tileFontSizes[i].ToString());

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
                            ApplyFontSize(i, sz);
                    }
                }

                ApplyLayout(currentLayout);

                this.Size = new Size(w, h);
                this.Location = new Point(lx, ly);
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
            popup.Size = new Size(200, 45);
            popup.TopMost = true;
            popup.ShowInTaskbar = false;
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
            panelMend.Controls.Add(lblMendEmoticon);
            panelMend.Controls.Add(lblMendHeader);

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
            OnResize(EventArgs.Empty);
        }

        private void ApplyMainLayout()
        {
            this.MinimumSize = new Size(420, 400);
            this.Size = new Size(PANEL_WIDTH * 2 + DIVIDER_WIDTH + 60, 700);

            bool showD = IsTileVisible(panelDPS);
            bool showH = IsTileVisible(panelHPS);
            bool showM = IsTileVisible(panelMend);
            bool showTD = IsTileVisible(panelMyDPS);
            bool showTH = IsTileVisible(panelMyHPS);

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

                panelBottomRow.Dock = DockStyle.Fill;
            }

            if (showM)
            {
                panelMend.Dock = DockStyle.Top;
                panelMend.Height = 60;
            }

            if (hasBottomRow) this.Controls.Add(panelBottomRow);
            if (showM && hasBottomRow)
                this.Controls.Add(new Panel { Dock = DockStyle.Top, Height = DIVIDER_WIDTH, BackColor = DIVIDER_COLOR });
            if (showM) this.Controls.Add(panelMend);
            if (hasTopRow && (showM || hasBottomRow))
                this.Controls.Add(new Panel { Dock = DockStyle.Top, Height = DIVIDER_WIDTH, BackColor = DIVIDER_COLOR });
            if (hasTopRow) this.Controls.Add(panelTopRow);
        }

        private void ApplyVerticalLayout()
        {
            this.MinimumSize = new Size(420, 300);
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
            this.MinimumSize = new Size(420, 200);
            this.Size = new Size(PANEL_WIDTH * 5 + DIVIDER_WIDTH * 4, 350);

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

        private EncounterData GetCurrentEncounter()
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
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
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
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // Ensure WS_EX_TRANSPARENT is never set (prevents click-through)
                cp.ExStyle &= ~0x20;
                return cp;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (panelDPS == null || panelMyDPS == null) return;

            switch (currentLayout)
            {
                case LayoutMode.Main:
                    int halfWidth = (this.ClientSize.Width - DIVIDER_WIDTH) / 2;
                    if (IsTileVisible(panelDPS) && IsTileVisible(panelHPS)) panelDPS.Width = halfWidth;
                    if (IsTileVisible(panelMyDPS) && IsTileVisible(panelMyHPS)) panelMyDPS.Width = halfWidth;

                    bool hasTop = IsTileVisible(panelDPS) || IsTileVisible(panelHPS);
                    bool hasBot = IsTileVisible(panelMyDPS) || IsTileVisible(panelMyHPS);
                    float units = (hasTop ? 1f : 0f) + (IsTileVisible(panelMend) ? 0.5f : 0f) + (hasBot ? 1f : 0f);
                    if (units > 0)
                    {
                        int fixedHeight = panelButtonBar.Height + lblHandleTracking.Height + panelHandleBar.Height + DIVIDER_WIDTH * 2;
                        int availHeight = this.ClientSize.Height - fixedHeight;
                        int unitH = (int)(availHeight / units);
                        if (hasTop) panelTopRow.Height = unitH;
                        if (IsTileVisible(panelMend)) panelMend.Height = unitH / 2 + HEADER_HEIGHT;
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
            if (updateTimer != null)
            {
                updateTimer.Stop();
                updateTimer.Dispose();
                updateTimer = null;
            }
            this.Close();
            this.Dispose();
        }

        public Point OverlayLocation
        {
            get { return this.Location; }
            set { this.Location = value; }
        }

        public Size OverlaySize
        {
            get { return this.Size; }
            set { this.Size = value; }
        }

        private string GetPluginDir()
        {
            foreach (ActPluginData plugin in ActGlobals.oFormActMain.ActPlugins)
            {
                if (plugin.pluginFile != null && plugin.pluginFile.Name.Contains("CO-ACTLib"))
                    return plugin.pluginFile.DirectoryName;
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
                if (txtDPSHandle != null && txtDPSHandle.Text != PLACEHOLDER_DPS)
                    dpsH = txtDPSHandle.Text ?? "";
                if (txtHPSHandle != null && txtHPSHandle.Text != PLACEHOLDER_HPS)
                    hpsH = txtHPSHandle.Text ?? "";
                xw.WriteElementString("DPSHandle", dpsH);
                xw.WriteElementString("HPSHandle", hpsH);

                if (tileOrder != null)
                {
                    foreach (TileInfo ti in tileOrder)
                        xw.WriteElementString("Tile_" + ti.Name.Replace(" ", "_"), ti.Visible.ToString());
                }

                for (int i = 0; i < tileFontSizes.Length; i++)
                    xw.WriteElementString("FontSize_" + i, tileFontSizes[i].ToString());

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
                            ApplyFontSize(i, sz);
                    }
                }

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
                n = root.SelectSingleNode("DPSHandle");
                if (n != null && !string.IsNullOrEmpty(n.InnerText)) savedDPS = n.InnerText;
                n = root.SelectSingleNode("HPSHandle");
                if (n != null && !string.IsNullOrEmpty(n.InnerText)) savedHPS = n.InnerText;

                if (!string.IsNullOrEmpty(savedDPS) || !string.IsNullOrEmpty(savedHPS))
                {
                    Timer handleTimer = new Timer();
                    handleTimer.Interval = 200;
                    handleTimer.Tick += (s2, e2) =>
                    {
                        handleTimer.Stop();
                        handleTimer.Dispose();
                        if (!string.IsNullOrEmpty(savedDPS)) SetDPSHandle(savedDPS);
                        if (!string.IsNullOrEmpty(savedHPS)) SetHPSHandle(savedHPS);
                    };
                    handleTimer.Start();
                }
            }
            catch { }
        }
    }
}
