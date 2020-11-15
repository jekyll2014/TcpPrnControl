using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TcpPrnControl.Properties;

namespace TcpPrnControl
{
    public partial class Form1 : Form
    {
        private TcpClient _clientSocket = new TcpClient();
        private static NetworkStream serverStream;

        private int SendComing;

        private TextLogger.TextLogger _logger;

        private enum DataDirection
        {
            Received,
            Sent,
            Info,
            Error
        }

        private readonly Dictionary<byte, string> _directions = new Dictionary<byte, string>
        {
            {(byte) DataDirection.Received, "<<"},
            {(byte) DataDirection.Sent, ">>"},
            {(byte) DataDirection.Info, "**"},
            {(byte) DataDirection.Error, "!!"}
        };

        public Form1()
        {
            InitializeComponent();
            ToolTipTerminal.SetToolTip(textBox_terminal, "Press left mouse button to read data from printer");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            checkBox_hexCommand.Checked = Settings.Default.checkBox_hexCommand;
            textBox_command.Text = Settings.Default.textBox_command;
            checkBox_hexParam.Checked = Settings.Default.checkBox_hexParam;
            textBox_param.Text = Settings.Default.textBox_param;
            textBox_ipAddress.Text = Settings.Default.DefaultAddress;
            textBox_port.Text = Settings.Default.DefaultPort;
            timer1.Interval = Settings.Default.TCPDataRefreshInterval;

            _logger = new TextLogger.TextLogger(this)
            {
                Channels = _directions,
                FilterZeroChar = false
            };
            textBox_terminal.DataBindings.Add("Text", _logger, "Text", false, DataSourceUpdateMode.OnPropertyChanged);

            _logger.LineTimeLimit = Settings.Default.LineBreakTimeout;
            _logger.LineLimit = Settings.Default.LogLinesLimit;
            _logger.AutoSave = checkBox_saveInput.Checked;
            _logger.LogFileName = textBox_saveTo.Text;

            _logger.DefaultTextFormat = checkBox_hexTerminal.Checked
                ? TextLogger.TextLogger.TextFormat.Hex
                : TextLogger.TextLogger.TextFormat.AutoReplaceHex;

            _logger.DefaultTimeFormat =
                checkBox_saveTime.Checked
                    ? TextLogger.TextLogger.TimeFormat.LongTime
                    : TextLogger.TextLogger.TimeFormat.None;

            _logger.DefaultDateFormat =
                checkBox_saveTime.Checked
                    ? TextLogger.TextLogger.DateFormat.ShortDate
                    : TextLogger.TextLogger.DateFormat.None;

            _logger.AutoScroll = checkBox_autoscroll.Checked;

            CheckBox_autoscroll_CheckedChanged(null, EventArgs.Empty);
        }

        private void Button_OPEN_Click(object sender, EventArgs e)
        {
            if (!_clientSocket.Connected)
                try
                {
                    _clientSocket = new TcpClient();
                    _clientSocket.Connect(textBox_ipAddress.Text, int.Parse(textBox_port.Text));
                    _clientSocket.ReceiveTimeout = 500;
                    _clientSocket.SendTimeout = 500;
                    _clientSocket.Client.ReceiveTimeout = 500;
                    _clientSocket.Client.SendTimeout = 500;
                    serverStream = _clientSocket.GetStream();
                    _logger.AddText("Port opened: " + textBox_ipAddress.Text + ":" + textBox_port.Text,
                        (byte) DataDirection.Info, DateTime.Now, TextLogger.TextLogger.TextFormat.PlainText);
                    button_Open.Enabled = false;
                    textBox_ipAddress.Enabled = false;
                    textBox_port.Enabled = false;
                    button_closeport.Enabled = true;
                    button_Send.Enabled = true;
                    button_sendFile.Enabled = true;
                    timer1.Enabled = true;
                    TextBox_fileName_TextChanged(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    _logger.AddText("Port open failure: " + ex.Message, (byte) DataDirection.Error, DateTime.Now,
                        TextLogger.TextLogger.TextFormat.PlainText);
                }
            else
                _logger.AddText("Port already connected", (byte) DataDirection.Info, DateTime.Now,
                    TextLogger.TextLogger.TextFormat.PlainText);
        }

        private void Button_CLOSE_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            try
            {
                _clientSocket.Client.Disconnect(false);
                serverStream.Close();
                _clientSocket.Close();
                _clientSocket = new TcpClient();
                _logger.AddText("Port closed", (byte) DataDirection.Info, DateTime.Now,
                    TextLogger.TextLogger.TextFormat.PlainText);
                button_Open.Enabled = true;
                button_closeport.Enabled = false;
                button_Send.Enabled = false;
                button_sendFile.Enabled = false;
                textBox_ipAddress.Enabled = true;
                textBox_port.Enabled = true;
            }
            catch (Exception ex)
            {
                _logger.AddText("Port close failure: " + ex.Message, (byte) DataDirection.Error, DateTime.Now,
                    TextLogger.TextLogger.TextFormat.PlainText);
            }
        }

        private void Button_WRITE_Click(object sender, EventArgs e)
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
                    _logger.AddText(Accessory.ConvertHexToString(outStr), (byte) DataDirection.Sent, DateTime.Now);
                    textBox_command.AutoCompleteCustomSource.Add(textBox_command.Text);
                    textBox_param.AutoCompleteCustomSource.Add(textBox_param.Text);
                    var outStream = Accessory.ConvertHexToByteArray(outStr);
                    WriteTCP(outStream);
                }
            }

            Timer1_Tick(this, EventArgs.Empty);
        }

        private void CheckBox_hexCommand_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_hexCommand.Checked) textBox_command.Text = Accessory.ConvertStringToHex(textBox_command.Text);
            else textBox_command.Text = Accessory.ConvertHexToString(textBox_command.Text);
        }

        private void TextBox_command_Leave(object sender, EventArgs e)
        {
            if (checkBox_hexCommand.Checked) textBox_command.Text = Accessory.CheckHexString(textBox_command.Text);
        }

        private void TextBox_param_Leave(object sender, EventArgs e)
        {
            if (checkBox_hexParam.Checked) textBox_param.Text = Accessory.CheckHexString(textBox_param.Text);
        }

        private void CheckBox_hexParam_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_hexParam.Checked) textBox_param.Text = Accessory.ConvertStringToHex(textBox_param.Text);
            else textBox_param.Text = Accessory.ConvertHexToString(textBox_param.Text);
        }

        private void Button_Clear_Click(object sender, EventArgs e)
        {
            textBox_terminal.Clear();
        }

        private void CheckBox_saveTo_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_saveInput.Checked) textBox_saveTo.Enabled = false;
            else textBox_saveTo.Enabled = true;
        }

        private async void Button_sendFile_ClickAsync(object sender, EventArgs e)
        {
            if (SendComing > 0)
            {
                SendComing++;
            }
            else if (SendComing == 0)
            {
                timer1.Enabled = false;

                if (textBox_fileName.Text != "" && textBox_sendNum.Text != "" &&
                    ushort.TryParse(textBox_sendNum.Text, out var repeat) &&
                    ushort.TryParse(textBox_delay.Text, out var delay) &&
                    ushort.TryParse(textBox_strDelay.Text, out var strDelay))
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
                    for (var n = 0; n < repeat; n++)
                    {
                        var outStr = "";
                        var outErr = "";
                        long length = 0;
                        if (repeat > 1)
                            _logger.AddText(" Send cycle " + (n + 1) + "/" + repeat + ">> ", (byte) DataDirection.Info,
                                DateTime.Now, TextLogger.TextLogger.TextFormat.PlainText);

                        try
                        {
                            length = new FileInfo(textBox_fileName.Text).Length;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("\r\nError opening file " + textBox_fileName.Text + ": " + ex.Message);
                        }

                        if (!checkBox_hexFileOpen.Checked) //binary data read
                        {
                            if (radioButton_byByte.Checked) //byte-by-byte
                            {
                                var tmpBuffer = new byte[length];
                                try
                                {
                                    tmpBuffer = File.ReadAllBytes(textBox_fileName.Text);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("\r\nError reading file " + textBox_fileName.Text + ": " +
                                                    ex.Message);
                                }

                                for (var m = 0; m < tmpBuffer.Length; m++)
                                {
                                    byte[] outByte = {tmpBuffer[m]};
                                    if (WriteTCP(outByte))
                                    {
                                        progressBar1.Value = (n * tmpBuffer.Length + m) * 100 /
                                                             (repeat * tmpBuffer.Length);
                                        if (strDelay > 0) await TaskEx.Delay(strDelay);
                                        var inStream = ReadTCP();
                                        if (inStream.Length > 0)
                                        {
                                            if (checkBox_saveInput.Checked)
                                            {
                                                if (checkBox_hexTerminal.Checked)
                                                    File.AppendAllText(textBox_saveTo.Text,
                                                        Accessory.ConvertByteArrayToHex(inStream, inStream.Length),
                                                        Encoding.GetEncoding(Settings.Default.CodePage));
                                                else
                                                    File.AppendAllText(textBox_saveTo.Text,
                                                        Encoding.GetEncoding(Settings.Default.CodePage)
                                                            .GetString(inStream),
                                                        Encoding.GetEncoding(Settings.Default.CodePage));
                                            }

                                            _logger.AddText(
                                                Encoding.GetEncoding(Settings.Default.CodePage).GetString(inStream),
                                                (byte) DataDirection.Received, DateTime.Now);
                                        }
                                    }
                                    else
                                    {
                                        _logger.AddText("Write Failure", (byte) DataDirection.Error, DateTime.Now,
                                            TextLogger.TextLogger.TextFormat.PlainText);
                                    }

                                    outStr = Encoding.GetEncoding(Settings.Default.CodePage).GetString(tmpBuffer);
                                    _logger.AddText(outStr, (byte) DataDirection.Sent, DateTime.Now);

                                    if (SendComing > 1) m = tmpBuffer.Length;
                                }
                            }
                            else //stream
                            {
                                var tmpBuffer = new byte[length];
                                try
                                {
                                    tmpBuffer = File.ReadAllBytes(textBox_fileName.Text);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("\r\nError reading file " + textBox_fileName.Text + ": " +
                                                    ex.Message);
                                }

                                if (WriteTCP(tmpBuffer))
                                {
                                    var inStream = ReadTCP();
                                    if (inStream.Length > 0)
                                    {
                                        if (checkBox_saveInput.Checked)
                                        {
                                            if (checkBox_hexTerminal.Checked)
                                                File.AppendAllText(textBox_saveTo.Text,
                                                    Accessory.ConvertByteArrayToHex(inStream, inStream.Length),
                                                    Encoding.GetEncoding(Settings.Default.CodePage));
                                            else
                                                File.AppendAllText(textBox_saveTo.Text,
                                                    Encoding.GetEncoding(Settings.Default.CodePage).GetString(inStream),
                                                    Encoding.GetEncoding(Settings.Default.CodePage));
                                        }

                                        _logger.AddText(
                                            Encoding.GetEncoding(Settings.Default.CodePage).GetString(inStream),
                                            (byte) DataDirection.Received, DateTime.Now);
                                    }
                                }
                                else
                                {
                                    outErr = "Write Failure";
                                }

                                outStr = Encoding.GetEncoding(Settings.Default.CodePage).GetString(tmpBuffer);
                                _logger.AddText(outStr, (byte) DataDirection.Sent, DateTime.Now);
                                _logger.AddText(outErr, (byte) DataDirection.Error, DateTime.Now,
                                    TextLogger.TextLogger.TextFormat.PlainText);
                                progressBar1.Value = n * 100 / (repeat * tmpBuffer.Length);
                            }
                        }
                        else //hex text read
                        {
                            if (radioButton_byString.Checked) //String-by-string
                            {
                                string[] tmpBuffer = { };
                                try
                                {
                                    length = new FileInfo(textBox_fileName.Text).Length;
                                    tmpBuffer = File.ReadAllText(textBox_fileName.Text).Replace("\n", "").Split('\r');
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("\r\nError reading file " + textBox_fileName.Text + ": " +
                                                    ex.Message);
                                }

                                for (var m = 0; m < tmpBuffer.Length; m++)
                                {
                                    tmpBuffer[m] = Accessory.CheckHexString(tmpBuffer[m]);
                                    if (WriteTCP(Accessory.ConvertHexToByteArray(tmpBuffer[m])))
                                    {
                                        if (checkBox_hexTerminal.Checked) outStr = tmpBuffer[m];
                                        else outStr = Accessory.ConvertHexToString(tmpBuffer[m]);
                                        if (strDelay > 0) await TaskEx.Delay(strDelay);
                                        var inStream = ReadTCP();
                                        if (inStream.Length > 0)
                                        {
                                            if (checkBox_saveInput.Checked)
                                            {
                                                if (checkBox_hexTerminal.Checked)
                                                    File.AppendAllText(textBox_saveTo.Text,
                                                        Accessory.ConvertByteArrayToHex(inStream, inStream.Length),
                                                        Encoding.GetEncoding(Settings.Default.CodePage));
                                                else
                                                    File.AppendAllText(textBox_saveTo.Text,
                                                        Encoding.GetEncoding(Settings.Default.CodePage)
                                                            .GetString(inStream),
                                                        Encoding.GetEncoding(Settings.Default.CodePage));
                                            }

                                            _logger.AddText(
                                                Encoding.GetEncoding(Settings.Default.CodePage).GetString(inStream),
                                                (byte) DataDirection.Received, DateTime.Now);
                                        }
                                    }
                                    else //??????????????
                                    {
                                        outErr = "Write failure";
                                    }

                                    if (SendComing > 1) m = tmpBuffer.Length;
                                    _logger.AddText(outStr, (byte) DataDirection.Sent, DateTime.Now);
                                    _logger.AddText(outErr, (byte) DataDirection.Error, DateTime.Now,
                                        TextLogger.TextLogger.TextFormat.PlainText);
                                    progressBar1.Value = (n * tmpBuffer.Length + m) * 100 / (repeat * tmpBuffer.Length);
                                }
                            }
                            else if (radioButton_byByte.Checked) //byte-by-byte
                            {
                                var tmpBuffer = "";
                                try
                                {
                                    length = new FileInfo(textBox_fileName.Text).Length;
                                    tmpBuffer = File.ReadAllText(textBox_fileName.Text);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("\r\nError reading file " + textBox_fileName.Text + ": " +
                                                    ex.Message);
                                }

                                tmpBuffer = Accessory.CheckHexString(tmpBuffer);
                                for (var m = 0; m < tmpBuffer.Length; m += 3)
                                {
                                    if (WriteTCP(Accessory.ConvertHexToByteArray(tmpBuffer.Substring(m, 3))))
                                    {
                                        if (checkBox_hexTerminal.Checked) outStr = tmpBuffer.Substring(m, 3);
                                        else outStr = Accessory.ConvertHexToString(tmpBuffer.Substring(m, 3));
                                        if (strDelay > 0) await TaskEx.Delay(strDelay);
                                        var inStream = ReadTCP();
                                        if (inStream.Length > 0)
                                        {
                                            if (checkBox_saveInput.Checked)
                                            {
                                                if (checkBox_hexTerminal.Checked)
                                                    File.AppendAllText(textBox_saveTo.Text,
                                                        Accessory.ConvertByteArrayToHex(inStream, inStream.Length),
                                                        Encoding.GetEncoding(Settings.Default.CodePage));
                                                else
                                                    File.AppendAllText(textBox_saveTo.Text,
                                                        Encoding.GetEncoding(Settings.Default.CodePage)
                                                            .GetString(inStream),
                                                        Encoding.GetEncoding(Settings.Default.CodePage));
                                            }

                                            _logger.AddText(
                                                Encoding.GetEncoding(Settings.Default.CodePage).GetString(inStream),
                                                (byte) DataDirection.Received, DateTime.Now);
                                        }
                                    }
                                    else
                                    {
                                        outErr += "Write Failure\r\n";
                                    }

                                    if (SendComing > 1) m = tmpBuffer.Length;
                                    _logger.AddText(outStr, (byte) DataDirection.Sent, DateTime.Now);
                                    _logger.AddText(outErr, (byte) DataDirection.Error, DateTime.Now,
                                        TextLogger.TextLogger.TextFormat.PlainText);
                                    progressBar1.Value = (n * tmpBuffer.Length + m) * 100 / (repeat * tmpBuffer.Length);
                                }
                            }
                            else //stream
                            {
                                var tmpBuffer = "";
                                try
                                {
                                    length = new FileInfo(textBox_fileName.Text).Length;
                                    tmpBuffer = Accessory.CheckHexString(File.ReadAllText(textBox_fileName.Text));
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("\r\nError reading file " + textBox_fileName.Text + ": " +
                                                    ex.Message);
                                }

                                if (WriteTCP(Accessory.ConvertHexToByteArray(tmpBuffer)))
                                {
                                    var inStream = ReadTCP();
                                    if (inStream.Length > 0)
                                    {
                                        if (checkBox_saveInput.Checked)
                                        {
                                            if (checkBox_hexTerminal.Checked)
                                                File.AppendAllText(textBox_saveTo.Text,
                                                    Accessory.ConvertByteArrayToHex(inStream, inStream.Length),
                                                    Encoding.GetEncoding(Settings.Default.CodePage));
                                            else
                                                File.AppendAllText(textBox_saveTo.Text,
                                                    Encoding.GetEncoding(Settings.Default.CodePage).GetString(inStream),
                                                    Encoding.GetEncoding(Settings.Default.CodePage));
                                        }

                                        _logger.AddText(
                                            Encoding.GetEncoding(Settings.Default.CodePage).GetString(inStream),
                                            (byte) DataDirection.Received, DateTime.Now);
                                    }
                                }
                                else
                                {
                                    _logger.AddText("Write Failure\r\n", (byte) DataDirection.Error, DateTime.Now,
                                        TextLogger.TextLogger.TextFormat.PlainText);
                                }

                                outStr = Accessory.ConvertHexToString(tmpBuffer);
                                _logger.AddText(outStr, (byte) DataDirection.Sent, DateTime.Now);

                                progressBar1.Value = n * 100 / (repeat * tmpBuffer.Length);
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

        private void OpenFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            textBox_fileName.Text = openFileDialog1.FileName;
        }

        private void Button_openFile_Click(object sender, EventArgs e)
        {
            if (checkBox_hexFileOpen.Checked)
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

        private void CheckBox_hexFileOpen_CheckedChanged(object sender, EventArgs e)
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
            if (_clientSocket.Client.Connected)
            {
                _clientSocket.Client.Disconnect(false);
                _clientSocket.Close();
            }

            Settings.Default.checkBox_hexCommand = checkBox_hexCommand.Checked;
            Settings.Default.textBox_command = textBox_command.Text;
            Settings.Default.checkBox_hexParam = checkBox_hexParam.Checked;
            Settings.Default.textBox_param = textBox_param.Text;
            Settings.Default.Save();
        }

        private void RadioButton_stream_CheckedChanged(object sender, EventArgs e)
        {
            textBox_strDelay.Enabled = !radioButton_stream.Checked;
        }

        private void TextBox_fileName_TextChanged(object sender, EventArgs e)
        {
            if (textBox_fileName.Text != "" && button_closeport.Enabled) button_sendFile.Enabled = true;
            else button_sendFile.Enabled = false;
        }

        private byte[] ReadTCP()
        {
            if (IsClientConnected())
            {
                if (serverStream.DataAvailable)
                    try
                    {
                        var inStream = new byte[_clientSocket.Available];
                        serverStream.Read(inStream, 0, inStream.Length);
                        return inStream;
                    }
                    catch (Exception ex)
                    {
                        _logger.AddText("Read failure: " + ex.Message, (byte) DataDirection.Error, DateTime.Now,
                            TextLogger.TextLogger.TextFormat.PlainText);
                    }

                return new byte[0];
            }

            _logger.AddText("Port not connected", (byte) DataDirection.Error, DateTime.Now,
                TextLogger.TextLogger.TextFormat.PlainText);
            Button_CLOSE_Click(this, EventArgs.Empty);
            return new byte[0];
        }

        private bool WriteTCP(byte[] outStream)
        {
            if (IsClientConnected())
            {
                if (outStream.Length > 0)
                    try
                    {
                        serverStream.Write(outStream, 0, outStream.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.AddText("Write failure: " + ex.Message, (byte) DataDirection.Error, DateTime.Now,
                            TextLogger.TextLogger.TextFormat.PlainText);
                        return false;
                    }
            }
            else
            {
                _logger.AddText("Port not connected", (byte) DataDirection.Error, DateTime.Now,
                    TextLogger.TextLogger.TextFormat.PlainText);
                Button_CLOSE_Click(this, EventArgs.Empty);
                return false;
            }

            return true;
        }

        private bool IsClientConnected()
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnections = ipProperties.GetActiveTcpConnections();
            foreach (var c in tcpConnections)
            {
                var stateOfConnection = c.State;
                if (c.LocalEndPoint.Equals(_clientSocket.Client.LocalEndPoint) &&
                    c.RemoteEndPoint.Equals(_clientSocket.Client.RemoteEndPoint))
                {
                    if (stateOfConnection == TcpState.Established) return true;
                    return false;
                }
            }

            return false;
        }

        private void CheckBox_saveTime_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_saveTime.Checked)
            {
                _logger.DefaultDateFormat = TextLogger.TextLogger.DateFormat.ShortDate;
                _logger.DefaultTimeFormat = TextLogger.TextLogger.TimeFormat.LongTime;
            }
            else
            {
                _logger.DefaultDateFormat = TextLogger.TextLogger.DateFormat.None;
                _logger.DefaultTimeFormat = TextLogger.TextLogger.TimeFormat.None;
            }
        }

        private void CheckBox_hexTerminal_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_hexTerminal.Checked)
                _logger.DefaultTextFormat = TextLogger.TextLogger.TextFormat.Hex;
            else
                _logger.DefaultTextFormat = TextLogger.TextLogger.TextFormat.AutoReplaceHex;
        }

        private void CheckBox_autoscroll_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_autoscroll.Checked)
            {
                _logger.AutoScroll = true;
                textBox_terminal.TextChanged += TextBox_terminal_TextChanged;
            }
            else
            {
                _logger.AutoScroll = false;
                textBox_terminal.TextChanged -= TextBox_terminal_TextChanged;
            }
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            if (IsClientConnected())
            {
                var inStream = ReadTCP();
                if (inStream.Length > 0)
                    _logger.AddText(Encoding.GetEncoding(Settings.Default.CodePage).GetString(inStream),
                        (byte) DataDirection.Received, DateTime.Now);
            }
            else
            {
                timer1.Enabled = false;
                Button_CLOSE_Click(this, EventArgs.Empty);
            }
        }

        private void TextBox_saveTo_Leave(object sender, EventArgs e)
        {
            _logger.LogFileName = textBox_saveTo.Text;
        }

        private void TextBox_terminal_TextChanged(object sender, EventArgs e)
        {
            if (checkBox_autoscroll.Checked)
            {
                textBox_terminal.SelectionStart = textBox_terminal.Text.Length;
                textBox_terminal.ScrollToCaret();
            }
        }
    }
}
