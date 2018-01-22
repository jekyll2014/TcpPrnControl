using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TcpPrnControl
{
    public partial class Form1 : Form
    {
        TcpClient clientSocket = new TcpClient();
        public static NetworkStream serverStream;

        int SendComing = 0;
        int txtOutState = 0;
        long oldTicks = DateTime.Now.Ticks, limitTick = 0;
        public const byte Port1DataIn = 11;
        public const byte Port1DataOut = 12;
        public const byte Port1Error = 13;

        private object threadLock = new object();
        public void collectBuffer(string tmpBuffer, int state, string time)
        {
            if (tmpBuffer != "")
            {
                lock (threadLock)
                {
                    if (!(txtOutState == state && (DateTime.Now.Ticks - oldTicks) < limitTick && state != Port1DataOut))
                    {
                        if (state == Port1DataIn)
                        {
                            tmpBuffer = "<< " + tmpBuffer;
                        }
                        else if (state == Port1DataOut)
                        {
                            tmpBuffer = ">> " + tmpBuffer;
                        }
                        else if (state == Port1Error)
                        {
                            tmpBuffer = "!! " + tmpBuffer;
                        }

                        if (checkBox_saveTime.Checked == true) tmpBuffer = time + " " + tmpBuffer;
                        tmpBuffer = "\r\n" + tmpBuffer;
                        txtOutState = state;
                    }
                    if ((checkBox_saveInput.Checked == true && state == Port1DataIn) || (checkBox_saveOutput.Checked == true && state == Port1DataOut))
                    {
                        try
                        {
                            File.AppendAllText(textBox_saveTo.Text, tmpBuffer, Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("\r\nError opening file " + textBox_saveTo.Text + ": " + ex.Message);
                        }
                    }
                    if (checkBox_autoscroll.Checked)
                    {
                        textBox_terminal.SelectionStart = textBox_terminal.TextLength;
                        textBox_terminal.SelectedText = tmpBuffer;
                    }
                    oldTicks = DateTime.Now.Ticks;
                }
            }
        }

        public Form1()
        {
            InitializeComponent();
            ToolTipTerminal.SetToolTip(textBox_terminal, "Press left mouse button to read data from printer");
        }

        private void button_OPEN_Click(object sender, EventArgs e)
        {
            if (!clientSocket.Connected)
            {
                try
                {
                    clientSocket = new TcpClient();
                    clientSocket.Connect(textBox_ipAddress.Text, int.Parse(textBox_port.Text));
                    clientSocket.ReceiveTimeout = 500;
                    clientSocket.SendTimeout = 500;
                    clientSocket.Client.ReceiveTimeout = 500;
                    clientSocket.Client.SendTimeout = 500;
                    serverStream = clientSocket.GetStream();
                    collectBuffer("Port opened: " + textBox_ipAddress.Text + ":" + textBox_port.Text, Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                    button_Open.Enabled = false;
                    textBox_ipAddress.Enabled = false;
                    textBox_port.Enabled = false;
                    button_closeport.Enabled = true;
                    button_Send.Enabled = true;
                    button_sendFile.Enabled = true;
                    timer1.Enabled = true;
                    textBox_fileName_TextChanged(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    collectBuffer("Port open failure: " + ex.Message, Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                }
            }
            else collectBuffer("Port already connected", Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
        }

        private void button_CLOSE_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            if (clientSocket.Connected)
            {
                try
                {
                    clientSocket.Client.Disconnect(false);
                    serverStream.Close();
                    clientSocket.Close();
                    clientSocket = new TcpClient();
                    collectBuffer("Port closed", Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                    button_Open.Enabled = true;
                    button_closeport.Enabled = false;
                    button_Send.Enabled = false;
                    button_sendFile.Enabled = false;
                    textBox_ipAddress.Enabled = true;
                    textBox_port.Enabled = true;
                }
                catch (Exception ex)
                {
                    collectBuffer("Port close failure: " + ex.Message, Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                }
            }
            else collectBuffer("Port already disconnected", Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
        }

        private void button_WRITE_Click(object sender, EventArgs e)
        {
            if (textBox_command.Text + textBox_param.Text != "")
            {
                if (textBox_command.Text + textBox_param.Text != "")
                {
                    string outStr;
                    if (checkBox_hexCommand.Checked) outStr = textBox_command.Text;
                    else outStr = Accessory.ConvertStringToHex(textBox_command.Text);
                    if (checkBox_hexParam.Checked) outStr += textBox_param.Text;
                    else outStr += Accessory.ConvertStringToHex(textBox_param.Text);
                    if (outStr != "")
                    {
                        if (checkBox_hexTerminal.Checked) collectBuffer(outStr, Port1DataOut, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                        else collectBuffer(Accessory.ConvertHexToString(outStr), Port1DataOut, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                        textBox_command.AutoCompleteCustomSource.Add(textBox_command.Text);
                        textBox_param.AutoCompleteCustomSource.Add(textBox_param.Text);
                        byte[] outStream = Accessory.ConvertHexToByteArray(outStr);
                        WriteTCP(outStream);
                    }
                }
                byte[] inStream = ReadTCP();
                if (inStream.Length > 0)
                {
                    if (checkBox_saveInput.Checked)
                    {
                        if (checkBox_hexTerminal.Checked) File.AppendAllText(textBox_saveTo.Text, Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                        else File.AppendAllText(textBox_saveTo.Text, Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                    }
                    if (checkBox_hexTerminal.Checked) collectBuffer(Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                    else collectBuffer(Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                }
            }
        }

        private void checkBox_hexCommand_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_hexCommand.Checked) textBox_command.Text = Accessory.ConvertStringToHex(textBox_command.Text);
            else textBox_command.Text = Accessory.ConvertHexToString(textBox_command.Text);
        }

        private void textBox_command_Leave(object sender, EventArgs e)
        {
            if (checkBox_hexCommand.Checked) textBox_command.Text = Accessory.CheckHexString(textBox_command.Text);
        }

        private void textBox_param_Leave(object sender, EventArgs e)
        {
            if (checkBox_hexParam.Checked) textBox_param.Text = Accessory.CheckHexString(textBox_param.Text);
        }

        private void checkBox_hexParam_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_hexParam.Checked) textBox_param.Text = Accessory.ConvertStringToHex(textBox_param.Text);
            else textBox_param.Text = Accessory.ConvertHexToString(textBox_param.Text);
        }

        private void button_Clear_Click(object sender, EventArgs e)
        {
            textBox_terminal.Clear();
        }

        private void checkBox_saveTo_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_saveInput.Checked || checkBox_saveOutput.Checked) textBox_saveTo.Enabled = false;
            else textBox_saveTo.Enabled = true;
        }

        private async void button_sendFile_ClickAsync(object sender, EventArgs e)
        {
            if (SendComing > 0)
            {
                SendComing++;
            }
            else if (SendComing == 0)
            {
                timer1.Enabled = false;
                UInt16 repeat = 1, delay = 1, strDelay = 1;

                if (textBox_fileName.Text != "" && textBox_sendNum.Text != "" && UInt16.TryParse(textBox_sendNum.Text, out repeat) && UInt16.TryParse(textBox_delay.Text, out delay) && UInt16.TryParse(textBox_strDelay.Text, out strDelay))
                {
                    SendComing = 1;
                    button_Send.Enabled = false;
                    button_closeport.Enabled = false;
                    button_openFile.Enabled = false;
                    button_sendFile.Text = "Stop";
                    textBox_fileName.Enabled = false;
                    textBox_sendNum.Enabled = false;
                    textBox_delay.Enabled = false;
                    textBox_strDelay.Enabled = false;
                    for (int n = 0; n < repeat; n++)
                    {
                        string outStr = "";
                        string outErr = "";
                        long length = 0;
                        if (repeat > 1) collectBuffer(" Send cycle " + (n + 1).ToString() + "/" + repeat.ToString() + ">> ", 0, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                        try
                        {
                            length = new FileInfo(textBox_fileName.Text).Length;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("\r\nError opening file " + textBox_fileName.Text + ": " + ex.Message);
                        }

                        if (!checkBox_hexFileOpen.Checked)  //binary data read
                        {
                            if (radioButton_byByte.Checked) //byte-by-byte
                            {
                                byte[] tmpBuffer = new byte[length];
                                try
                                {
                                    tmpBuffer = File.ReadAllBytes(textBox_fileName.Text);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("\r\nError reading file " + textBox_fileName.Text + ": " + ex.Message);
                                }
                                for (int m = 0; m < tmpBuffer.Length; m++)
                                {
                                    byte[] outByte = { tmpBuffer[m] };
                                    if (WriteTCP(outByte))
                                    {
                                        progressBar1.Value = (n * tmpBuffer.Length + m) * 100 / (repeat * tmpBuffer.Length);
                                        await TaskEx.Delay(strDelay);
                                        byte[] inStream = ReadTCP();
                                        if (inStream.Length > 0)
                                        {
                                            if (checkBox_saveInput.Checked)
                                            {
                                                if (checkBox_hexTerminal.Checked) File.AppendAllText(textBox_saveTo.Text, Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                                                else File.AppendAllText(textBox_saveTo.Text, Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                                            }
                                            if (checkBox_hexTerminal.Checked) collectBuffer(Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                            else collectBuffer(Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                        }
                                    }
                                    else
                                    {
                                        collectBuffer("Write Failure", Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                    }

                                    if (checkBox_hexTerminal.Checked) outStr = Accessory.ConvertByteArrayToHex(tmpBuffer, tmpBuffer.Length);
                                    //else outStr = ConvertHexToString(ConvertByteArrToHex(tmpBuffer, tmpBuffer.Length));
                                    else outStr = tmpBuffer.ToString();
                                    collectBuffer(outStr, Port1DataOut, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                    if (SendComing > 1) m = tmpBuffer.Length;
                                }
                            }
                            else //stream
                            {
                                byte[] tmpBuffer = new byte[length];
                                try
                                {
                                    tmpBuffer = File.ReadAllBytes(textBox_fileName.Text);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("\r\nError reading file " + textBox_fileName.Text + ": " + ex.Message);
                                }
                                if (WriteTCP(tmpBuffer))
                                {
                                    byte[] inStream = ReadTCP();
                                    if (inStream.Length > 0)
                                    {
                                        if (checkBox_saveInput.Checked)
                                        {
                                            if (checkBox_hexTerminal.Checked) File.AppendAllText(textBox_saveTo.Text, Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                                            else File.AppendAllText(textBox_saveTo.Text, Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                                        }
                                        if (checkBox_hexTerminal.Checked) collectBuffer(Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                        else collectBuffer(Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                    }
                                }
                                else outErr = "Write Failure";
                                if (checkBox_hexTerminal.Checked) outStr = Accessory.ConvertByteArrayToHex(tmpBuffer, tmpBuffer.Length);
                                //else outStr += ConvertHexToString(ConvertByteArrToHex(tmpBuffer, tmpBuffer.Length));
                                else outStr = tmpBuffer.ToString();
                                collectBuffer(outStr, Port1DataOut, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                collectBuffer(outErr, Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                progressBar1.Value = (n * 100) / (repeat * tmpBuffer.Length);
                            }
                        }
                        else  //hex text read
                        {
                            if (radioButton_byString.Checked) //String-by-string
                            {
                                String[] tmpBuffer = { };
                                try
                                {
                                    length = new FileInfo(textBox_fileName.Text).Length;
                                    tmpBuffer = File.ReadAllText(textBox_fileName.Text).Replace("\n", "").Split('\r');
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("\r\nError reading file " + textBox_fileName.Text + ": " + ex.Message);
                                }
                                for (int m = 0; m < tmpBuffer.Length; m++)
                                {
                                    tmpBuffer[m] = Accessory.CheckHexString(tmpBuffer[m]);
                                    if (WriteTCP(Accessory.ConvertHexToByteArray(tmpBuffer[m])))
                                    {
                                        if (checkBox_hexTerminal.Checked) outStr = tmpBuffer[m];
                                        else outStr = Accessory.ConvertHexToString(tmpBuffer[m]);
                                        await TaskEx.Delay(strDelay);
                                        byte[] inStream = ReadTCP();
                                        if (inStream.Length > 0)
                                        {
                                            if (checkBox_saveInput.Checked)
                                            {
                                                if (checkBox_hexTerminal.Checked) File.AppendAllText(textBox_saveTo.Text, Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                                                else File.AppendAllText(textBox_saveTo.Text, Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                                            }
                                            if (checkBox_hexTerminal.Checked) collectBuffer(Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                            else collectBuffer(Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                        }
                                    }
                                    else  //??????????????
                                    {
                                        outErr = "Write failure";
                                    }

                                    if (SendComing > 1) m = tmpBuffer.Length;
                                    collectBuffer(outStr, Port1DataOut, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                    collectBuffer(outErr, Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                    progressBar1.Value = (n * tmpBuffer.Length + m) * 100 / (repeat * tmpBuffer.Length);
                                }
                            }
                            else if (radioButton_byByte.Checked) //byte-by-byte
                            {
                                String tmpBuffer = "";
                                try
                                {
                                    length = new FileInfo(textBox_fileName.Text).Length;
                                    tmpBuffer = File.ReadAllText(textBox_fileName.Text);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("\r\nError reading file " + textBox_fileName.Text + ": " + ex.Message);
                                }
                                tmpBuffer = Accessory.CheckHexString(tmpBuffer);
                                for (int m = 0; m < tmpBuffer.Length; m += 3)
                                {
                                    if (WriteTCP(Accessory.ConvertHexToByteArray(tmpBuffer.Substring(m, 3))))
                                    {
                                        if (checkBox_hexTerminal.Checked) outStr = tmpBuffer.Substring(m, 3);
                                        else outStr = Accessory.ConvertHexToString(tmpBuffer.Substring(m, 3));
                                        await TaskEx.Delay(strDelay);
                                        byte[] inStream = ReadTCP();
                                        if (inStream.Length > 0)
                                        {
                                            if (checkBox_saveInput.Checked)
                                            {
                                                if (checkBox_hexTerminal.Checked) File.AppendAllText(textBox_saveTo.Text, Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                                                else File.AppendAllText(textBox_saveTo.Text, Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                                            }
                                            if (checkBox_hexTerminal.Checked) collectBuffer(Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                            else collectBuffer(Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                        }
                                    }
                                    else
                                    {
                                        outErr += "Write Failure\r\n";
                                    }

                                    if (SendComing > 1) m = tmpBuffer.Length;
                                    collectBuffer(outStr, Port1DataOut, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                    collectBuffer(outErr, Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                    progressBar1.Value = (n * tmpBuffer.Length + m) * 100 / (repeat * tmpBuffer.Length);
                                }
                            }
                            else //stream
                            {
                                string tmpBuffer = "";
                                try
                                {
                                    length = new FileInfo(textBox_fileName.Text).Length;
                                    tmpBuffer = Accessory.CheckHexString(File.ReadAllText(textBox_fileName.Text));
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("\r\nError reading file " + textBox_fileName.Text + ": " + ex.Message);
                                }
                                if (WriteTCP(Accessory.ConvertHexToByteArray(tmpBuffer)))
                                {
                                    byte[] inStream = ReadTCP();
                                    if (inStream.Length > 0)
                                    {
                                        if (checkBox_saveInput.Checked)
                                        {
                                            if (checkBox_hexTerminal.Checked) File.AppendAllText(textBox_saveTo.Text, Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                                            else File.AppendAllText(textBox_saveTo.Text, Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                                        }
                                        if (checkBox_hexTerminal.Checked) collectBuffer(Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                        else collectBuffer(Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                    }
                                }
                                else collectBuffer("Write Failure\r\n", Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                if (checkBox_hexTerminal.Checked) outStr = tmpBuffer;
                                else outStr = Accessory.ConvertHexToString(tmpBuffer);
                                collectBuffer(outStr, Port1DataOut, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                                progressBar1.Value = (n * 100) / (repeat * tmpBuffer.Length);
                            }
                        }

                        if (repeat > 1) await TaskEx.Delay(delay);
                        if (SendComing > 1) n = repeat;
                    }
                    button_Send.Enabled = true;
                    button_closeport.Enabled = true;
                    button_openFile.Enabled = true;
                    button_sendFile.Text = "Send file";
                    textBox_fileName.Enabled = true;
                    textBox_sendNum.Enabled = true;
                    textBox_delay.Enabled = true;
                    textBox_strDelay.Enabled = true;
                }
                SendComing = 0;
                timer1.Enabled = true;
            }
        }

        private void openFileDialog1_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            textBox_fileName.Text = openFileDialog1.FileName;
        }

        private void button_openFile_Click(object sender, EventArgs e)
        {
            if (checkBox_hexFileOpen.Checked == true)
            {
                openFileDialog1.FileName = "";
                openFileDialog1.Title = "Open file";
                openFileDialog1.DefaultExt = "txt";
                openFileDialog1.Filter = "HEX files|*.hex|Text files|*.txt|All files|*.*";
                openFileDialog1.ShowDialog();
            }
            else
            {
                openFileDialog1.FileName = "";
                openFileDialog1.Title = "Open file";
                openFileDialog1.DefaultExt = "bin";
                openFileDialog1.Filter = "BIN files|*.bin|PRN files|*.prn|All files|*.*";
                openFileDialog1.ShowDialog();
            }
        }

        private void checkBox_hexFileOpen_CheckedChanged(object sender, EventArgs e)
        {
            if (!checkBox_hexFileOpen.Checked)
            {
                radioButton_byString.Enabled = false;
                if (radioButton_byString.Checked) radioButton_byByte.Checked = true;
                checkBox_hexFileOpen.Text = "binary data";
            }
            else
            {
                radioButton_byString.Enabled = true;
                checkBox_hexFileOpen.Text = "hex text data";
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer1.Enabled = false;
            if (clientSocket.Client.Connected)
            {
                clientSocket.Client.Disconnect(false);
                clientSocket.Close();
            }
            Properties.Settings.Default.checkBox_hexCommand = checkBox_hexCommand.Checked;
            Properties.Settings.Default.textBox_command = textBox_command.Text;
            Properties.Settings.Default.checkBox_hexParam = checkBox_hexParam.Checked;
            Properties.Settings.Default.textBox_param = textBox_param.Text;
            Properties.Settings.Default.Save();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            checkBox_hexCommand.Checked = Properties.Settings.Default.checkBox_hexCommand;
            textBox_command.Text = Properties.Settings.Default.textBox_command;
            checkBox_hexParam.Checked = Properties.Settings.Default.checkBox_hexParam;
            textBox_param.Text = Properties.Settings.Default.textBox_param;
            textBox_ipAddress.Text = Properties.Settings.Default.DefaultAddress;
            textBox_port.Text = Properties.Settings.Default.DefaultPort;
            timer1.Interval = Properties.Settings.Default.TCPDataRefreshInterval;
        }

        private void radioButton_stream_CheckedChanged(object sender, EventArgs e)
        {
            textBox_strDelay.Enabled = !radioButton_stream.Checked;
        }

        private void textBox_fileName_TextChanged(object sender, EventArgs e)
        {
            if (textBox_fileName.Text != "" && button_closeport.Enabled == true) button_sendFile.Enabled = true;
            else button_sendFile.Enabled = false;
        }

        private byte[] ReadTCP()
        {
            if (isClientConnected())
            {
                if (serverStream.DataAvailable)
                {
                    try
                    {
                        byte[] inStream = new byte[clientSocket.Available];
                        serverStream.Read(inStream, 0, inStream.Length);
                        return inStream;
                    }
                    catch (Exception ex)
                    {
                        collectBuffer("Read failure: " + ex.Message, Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                    }
                }
                return new byte[0];
            }
            else
            {
                collectBuffer("Port not connected", Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                button_CLOSE_Click(this, EventArgs.Empty);
                return new byte[0];
            }
        }

        private bool WriteTCP(byte[] outStream)
        {
            if (isClientConnected())
            {
                if (outStream.Length > 0)
                {
                    try
                    {
                        serverStream.Write(outStream, 0, outStream.Length);
                    }
                    catch (Exception ex)
                    {
                        collectBuffer("Write failure: " + ex.Message, Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                        return false;
                    }
                }
            }
            else
            {
                collectBuffer("Port not connected", Port1Error, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                button_CLOSE_Click(this, EventArgs.Empty);
                return false;
            }
            return true;
        }

        public bool isClientConnected()
        {
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnections = ipProperties.GetActiveTcpConnections();
            foreach (TcpConnectionInformation c in tcpConnections)
            {
                TcpState stateOfConnection = c.State;
                if (c.LocalEndPoint.Equals(clientSocket.Client.LocalEndPoint) && c.RemoteEndPoint.Equals(clientSocket.Client.RemoteEndPoint))
                {
                    if (stateOfConnection == TcpState.Established)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return false;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (isClientConnected())
            {
                if (isClientConnected())
                {
                    byte[] inStream = ReadTCP();
                    if (inStream.Length > 0)
                    {
                        if (checkBox_saveInput.Checked)
                        {
                            if (checkBox_hexTerminal.Checked) File.AppendAllText(textBox_saveTo.Text, Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                            else File.AppendAllText(textBox_saveTo.Text, Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Encoding.GetEncoding(Properties.Settings.Default.CodePage));
                        }
                        if (checkBox_hexTerminal.Checked) collectBuffer(Accessory.ConvertByteArrayToHex(inStream, inStream.Length), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                        else collectBuffer(Encoding.GetEncoding(Properties.Settings.Default.CodePage).GetString(inStream), Port1DataIn, DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3"));
                    }
                }
                else
                {
                    timer1.Enabled = false;
                    button_CLOSE_Click(this, EventArgs.Empty);
                }
            }
            else
            {
                timer1.Enabled = false;
                button_CLOSE_Click(this, EventArgs.Empty);
            }
        }
    }
}
