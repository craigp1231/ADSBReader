using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ADSBReader
{
    public partial class FormMain : MetroFramework.Forms.MetroForm
    {
        public ConcurrentDictionary<string, Aircraft> Aircraft;

        private List<Color> colorLimits;

        public delegate void DeviceSelectionDelegate(int index);
        public event DeviceSelectionDelegate DeviceSelectionChanged;

        //private DateTime debugTime;

        public FormMain()
        {
            InitializeComponent();

            // Colours by percent
            colorLimits = new List<Color>();
            colorLimits.Add(Color.FromArgb(112, 212, 59));
            colorLimits.Add(Color.FromArgb(227, 220, 0));
            colorLimits.Add(Color.FromArgb(214, 63, 44));

            // Devices
            DeviceDisplay[] activeDevices = DeviceDisplay.GetActiveDevices();
            this.cboDevices.Items.Clear();
            this.cboDevices.Items.AddRange(activeDevices);
            if (this.cboDevices.Items.Count > 0)
                this.cboDevices.SelectedIndex = 0;


            //debugTime = DateTime.Now;
        }

        public void UpdateFPS(float fps)
        {
            if (lblFPS.InvokeRequired)
            {
                lblFPS.Invoke(new MethodInvoker(delegate() { UpdateFPS(fps); }));
            }
            else
            {
                lblFPS.Text = string.Format("{0:0}", fps);
            }
        }

        private void updateTimer_Tick(object sender, EventArgs e)
        {
            var acs = from a in Aircraft
                      where (DateTime.Now - a.Value.LastSeen).TotalSeconds <= 30
                      orderby a.Value.HexAddress
                      select a.Value;

            metroGrid1.Rows.Clear();

            foreach (Aircraft a in acs)
            {
                metroGrid1.Rows.Add();
                int idx = metroGrid1.Rows.Count - 1;
                metroGrid1.Rows[idx].Cells[0].Value = a.HexAddress;
                metroGrid1.Rows[idx].Cells[1].Value = a.Squawk;
                metroGrid1.Rows[idx].Cells[2].Value = a.Callsign;

                metroGrid1.Rows[idx].Cells[3].Value = a.AircraftReg;
                metroGrid1.Rows[idx].Cells[4].Value = a.AircraftType;

                if (a.AltitudeTime != DateTime.MinValue)
                {
                    string vsign = string.Empty;

                    if (a.VerticalSpeed > -32768)
                    {
                        if (a.VerticalSpeed < -100)
                        {
                            vsign = "↓";
                        }
                        else if (a.VerticalSpeed > 100)
                        {
                            vsign = "↑";
                        }
                    }

                    metroGrid1.Rows[idx].Cells[5].Value = string.Format("{0}{1}", a.Altitude, vsign);
                    metroGrid1.Rows[idx].Cells[5].Style.ForeColor = ColorForTime(a.AltitudeTime);
                }

                if (a.MCPAltitudeTime != DateTime.MinValue)
                {
                    metroGrid1.Rows[idx].Cells[6].Value = string.Format("{0}{1}", a.MCPAltitude, a.MCPAltChanged ? "*" : string.Empty);
                    metroGrid1.Rows[idx].Cells[6].Style.ForeColor = ColorForTime(a.MCPAltitudeTime);
                }

                if (a.SpeedTime != DateTime.MinValue)
                {
                    metroGrid1.Rows[idx].Cells[7].Value = string.Format("{0} ({1})", a.Speed, a.IAS);
                    metroGrid1.Rows[idx].Cells[7].Style.ForeColor = ColorForTime(a.SpeedTime);
                }

                if (a.HeadingTime != DateTime.MinValue)
                {
                    metroGrid1.Rows[idx].Cells[8].Value = string.Format("{0:000}", a.Heading);
                    metroGrid1.Rows[idx].Cells[8].Style.ForeColor = ColorForTime(a.HeadingTime);
                }
                
                if (a.Position != null)
                {
                    metroGrid1.Rows[idx].Cells[9].Value = string.Format("{0:f3}", a.Position.Latitude);
                    metroGrid1.Rows[idx].Cells[10].Value = string.Format("{0:f3}", a.Position.Longitude);

                    metroGrid1.Rows[idx].Cells[9].Style.ForeColor = ColorForTime(a.PositionTime);
                    metroGrid1.Rows[idx].Cells[10].Style.ForeColor = ColorForTime(a.PositionTime);
                }

                metroGrid1.Rows[idx].Cells[11].Value = a.MessageCount;
                metroGrid1.Rows[idx].Cells[12].Value = (int)(DateTime.Now - a.LastSeen).TotalSeconds;

                metroGrid1.Rows[idx].Cells[1].Style.ForeColor = ColorForTime(a.SquawkTime);
                metroGrid1.Rows[idx].Cells[2].Style.ForeColor = ColorForTime(a.CallsignTime);

                metroGrid1.Rows[idx].Cells[12].Style.ForeColor = ColorForTime(a.LastSeen);
            }

            //this.label1.BackColor = ColorForTime(debugTime);
        }

        private Color ColorForTime(DateTime t)
        {
            if (t == null) return Color.Black;
            if (colorLimits.Count == 0) return Color.Black;

            double sec = (DateTime.Now - t).TotalSeconds;

            double p = sec / 30d;

            if (p > 1) p = 1;
            else if (p < 0) p = 0;

            int clrCount = colorLimits.Count - 1;

            int lidx = (int)Math.Floor(clrCount * p);
            int hidx = (int)Math.Ceiling(clrCount * p);

            //p -= lidx / clrCount;
            p *= clrCount;
            while (p > 1) p -= 1;

            //System.Diagnostics.Debug.WriteLine(string.Format("P: {0}, L: {1}, H: {2}", p, lidx, hidx));


            Color c1 = colorLimits[lidx];
            Color c2 = colorLimits[hidx];

            int rd = c1.R - c2.R;
            int gd = c1.G - c2.G;
            int bd = c1.B - c2.B;

            int nr = (int)(c1.R - (double)(rd * p));
            int ng = (int)(c1.G - (double)(gd * p));
            int nb = (int)(c1.B - (double)(bd * p));

            Color c3 = Color.FromArgb(nr, ng, nb);

            return c3;
        }

        private void metroGrid1_SelectionChanged(object sender, EventArgs e)
        {
            metroGrid1.ClearSelection();
        }

        private void cboDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            DeviceDisplay selectedItem = (DeviceDisplay)this.cboDevices.SelectedItem;
            if (selectedItem != null)
            {
                try
                {
                    if (DeviceSelectionChanged != null)
                        DeviceSelectionChanged(this.cboDevices.SelectedIndex);
                }
                catch (Exception exception)
                {
                    this.cboDevices.SelectedIndex = -1;
                    MessageBox.Show(this, exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }

            }
        }
    }
}
