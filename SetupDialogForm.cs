using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ASCOM.Utilities;

namespace ASCOM.MyMinEq
{
    [ComVisible(false)]					// Form not registered for COM!
    public partial class SetupDialogForm : Form
    {
        private Util utilities;
        private TraceLogger tl;

        public SetupDialogForm()
        {
            tl = new TraceLogger("", "MyMinEq - Setup");
            InitializeComponent();
            // Initialise current values of user settings from the ASCOM Profile
            utilities = new Util(); //Initialise util object
            InitUI();


        }

        private void cmdOK_Click(object sender, EventArgs e) // OK button event handler
        {
            // Place any validation constraint checks here
            // Update the state variables with results from the dialogue
            Telescope.comPort = (string)comboBoxComPort.SelectedItem;
            Telescope.tl.Enabled = chkTrace.Checked;

            Telescope.rightAscension = utilities.HMSToHours(textBoxRA.Text);
            Telescope.declination = utilities.DMSToDegrees(textBoxDec.Text);

            Telescope.pwmLow = int.Parse(textBoxPwmLow.Text);
            Telescope.pwmHigh = int.Parse(textBoxPwmHigh.Text);
        }

        private void cmdCancel_Click(object sender, EventArgs e) // Cancel button event handler
        {
            Close();
        }

        private void BrowseToAscom(object sender, EventArgs e) // Click on ASCOM logo event handler
        {
            try
            {
                System.Diagnostics.Process.Start("http://ascom-standards.org/");
            }
            catch (System.ComponentModel.Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (System.Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

        private void InitUI()
        {
            chkTrace.Checked = Telescope.tl.Enabled;
            // set the list of com ports to those that are currently available
            comboBoxComPort.Items.Clear();
            comboBoxComPort.Items.AddRange(System.IO.Ports.SerialPort.GetPortNames());      // use System.IO because it's static
            // select the current port if possible
            if (comboBoxComPort.Items.Contains(Telescope.comPort))
            {
                comboBoxComPort.SelectedItem = Telescope.comPort;
            }

            textBoxRA.Text = utilities.HoursToHMS(Telescope.rightAscension);
            textBoxDec.Text = utilities.DegreesToDMS(Telescope.declination);

            textBoxPwmLow.Text = Telescope.pwmLow.ToString();
            textBoxPwmHigh.Text = Telescope.pwmHigh.ToString();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            tl.Enabled = chkTrace.Checked;
            using (
            var serial = new Serial()
            {
                PortName = (string)comboBoxComPort.SelectedItem,
                Speed = SerialSpeed.ps9600
            })
            {
                serial.Connected = true;

                var trackingRate = 159.115175711434;

                textBoxPwmLow.Text = calibratePwm(serial, trackingRate * 0.5).ToString();
                textBoxPwmHigh.Text = calibratePwm(serial, trackingRate * 1.5).ToString();
                calibratePwm(serial, trackingRate);
            }

        }

        private int calibratePwm(Serial serial, double trackingRate)
        {
            serial.Transmit($"t {trackingRate}\r\n");
            // wait for ack
            while (serial.ReceiveTerminated("\r\n") != "ack\r\n") ;
            // wait until it tracks
            int pwm = 0;
            char mode = ' ';
            do
            {
                var line = serial.ReceiveTerminated("\r\n");
                var match = Regex.Match(line, @"^(?<mode>[ots])(?<rate>[0-9]+) (?<pwm>[0-9]+)\r\n$");
                if (match.Success)
                {
                    mode = match.Groups["mode"].Value[0];
                    pwm = int.Parse(match.Groups["pwm"].Value);
                }
            } while (mode != 't' && !(mode == 's' && (pwm == 0 || pwm == 255)));
            return pwm;
        }
    }
}