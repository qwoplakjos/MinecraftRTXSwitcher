using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MinecraftRTXSwitcher
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            NvidiaDriverEditor.Output += NvidiaDriverEditor_Output;
        }

        private void NvidiaDriverEditor_Output(object sender, string e)
        {
            Invoke(new Action(() =>
            {

                if (e.Contains("Successfully") && e.Contains("enabled")) richTextBox1.SelectionColor = Color.Green;
                else if (e.Contains("Successfully") && e.Contains("disabled")) richTextBox1.SelectionColor = Color.Red;
                else if (e.Contains("already")) richTextBox1.SelectionColor = Color.Olive;

                richTextBox1.AppendText(e + "\n");
                richTextBox1.SelectionColor = Color.Black;
            }));
        }

        private void enableButton_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();
            Task.Run(() =>
            {
                try
                {
                    NvidiaDriverEditor.ChangeSetting(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            });
        }

        private void disableButton_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();
            Task.Run(() =>
            {
                try
                {
                    NvidiaDriverEditor.ChangeSetting(false);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            });
        }
    }
}
