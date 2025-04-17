using System;
using System.Drawing;
using System.Linq;
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
                if (e.Contains("Successfully"))
                {
                    if (e.Contains("enabled")) richTextBox1.SelectionColor = Color.Green;
                    else if (e.Contains("disabled")) richTextBox1.SelectionColor = Color.Red;
                }
                else if (e.Contains("already")) richTextBox1.SelectionColor = Color.Olive;

                richTextBox1.AppendText(e + "\n");
                richTextBox1.SelectionColor = Color.Black;
            }));
        }

        private void HandleException(string message)
        {
            var msg = message.Split(new string[] { "Details: " }, StringSplitOptions.None).LastOrDefault();

            if (msg == "NVAPI_PROFILE_NOT_FOUND")
            {
                MessageBox.Show("Minecraft profile doesn't exist! Please re-install NVIDIA GPU driver!");
                return;
            }

            MessageBox.Show(message);
        }

        private void EnableButton_Click(object sender, EventArgs e)
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
                    HandleException(ex.Message);
                }
            });
        }

        private void DisableButton_Click(object sender, EventArgs e)
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
                    HandleException(ex.Message);
                }
            });
        }
    }
}
