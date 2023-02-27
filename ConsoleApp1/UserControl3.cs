using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ZKTecoFingerPrintScanner_Implementation
{
    public partial class UserControl3 : Form
    {
        public UserControl3()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Maximized;
            //Properties.Settings.Default.momo = "c";
            // Properties.Settings.Default.Save();

            //MessageBox.Show(Properties.Settings.Default.served);
            Properties.Settings.Default.PropertyChanged += Settings_PropertyChanged;
            String f = "C:\\Users\\Louie\\Documents\\Lord Huron - The Night We Met (Official Lyric Video).mp4";
            axWindowsMediaPlayer1.URL = f;

        }


        public Label serverdLable()
        {
            //Properties.Settings.Default.momo;
            return label5;
        }




        private void UserControl3_Load(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {
            
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Update the label's text property

            label5.Text = Properties.Settings.Default.served;
            label6.Text = Properties.Settings.Default.now;
            label7.Text = Properties.Settings.Default.next;
        
        }


            public void ShowMessage(string message)
        {
            MessageBox.Show(message);
        }

        private void axWindowsMediaPlayer1_Enter(object sender, EventArgs e)
        {

        }
    }
}
