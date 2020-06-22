using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;

/*Private include ---------------------------------------------------------------------------------------------------------*/
using System.IO.Ports;
using ZedGraph;
using Export2Excelx;
using UartCommunication;

/*Main code ---------------------------------------------------------------------------------------------------------------*/
namespace SerialGUI
{
    public partial class FormSerialPort : Form
    {

        /*Private variables ---------------------------------------------------------------------------------------------------*/
        public enum GraphStatus { GraphRun = 1, GraphStop = 0 };
        public enum GraphScroll { Scroll = 0, Compact = 1 };

        string OutBuffer;
        byte[] InByte = new byte[7];
        byte[] InByte_16 = new byte[16];

        double Realtime = 0;                                    //Khai báo biến thời gian để vẽ đồ thị
        double Realtimestep = 0.02;
        double Setpoint = 0;                                       //Khai báo biến dữ liệu thứ nhất để vẽ đồ thị
        double Measure = 0;                                      //Khai báo biến dữ liệu thứ 2 để vẽ đồ thị
        double PWM = 0;

        GraphStatus status = GraphStatus.GraphRun;
        GraphScroll enScroll = GraphScroll.Scroll;



        DataTable logTable = new DataTable();


        /*Form methods --------------------------------------------------------------------------------------------------------*/
        public FormSerialPort()
        {
            InitializeComponent();
        }

        private void FormSerialPort_Load(object sender, EventArgs e)
        {
            string[] port = SerialPort.GetPortNames();
            cBoxPortName.Items.AddRange(port);

            logTable.Columns.Add("Setpoint", typeof(float));
            logTable.Columns.Add("Measure", typeof(float));

            // Khởi tạo ZedGraph            
            GraphPane myPane = zGrphPlotData.GraphPane;      //Tác động các thành phần của Control, (GraphPane)
            myPane.Title.Text = "Giá trị đặt - Giá trị đo";
            myPane.XAxis.Title.Text = "Thời gian (s)";
            myPane.YAxis.Title.Text = "Dữ liệu";

            RollingPointPairList list = new RollingPointPairList(60000);        //Tạo mới danh sách dữ liệu 60000 phần tử, có khả năng cuốn chiếu
            LineItem curve = myPane.AddCurve("Giá trị đặt", list, Color.Red, SymbolType.None);         //Tạo mới đường cong của đồ thị trên GraphPane dựa vào danh sách dữ liệu
            RollingPointPairList list2 = new RollingPointPairList(60000);
            LineItem curve2 = myPane.AddCurve("Giá trị đo", list2, Color.MediumSlateBlue, SymbolType.None);

            myPane.XAxis.Scale.Min = 0;                         //Đặt giới hạn đồ thị
            myPane.XAxis.Scale.Max = 6;
            myPane.XAxis.Scale.MinorStep = 0.1;                   //Đặt các bước độ chia
            myPane.XAxis.Scale.MajorStep = 1;
            myPane.YAxis.Scale.Min = 0;                      //Tương tự cho trục y
            myPane.YAxis.Scale.Max = 2500;

            myPane.AxisChange();

            // Khởi tạo ZedGraph            
            GraphPane myPanePara = zGraphParameters.GraphPane;      //Tác động các thành phần của Control, (GraphPane)
            myPanePara.Title.Text = "Tham số";
            myPanePara.XAxis.Title.Text = "Thời gian (s)";
            myPanePara.YAxis.Title.Text = "Dữ liệu";

            RollingPointPairList listPara = new RollingPointPairList(60000);        //Tạo mới danh sách dữ liệu 60000 phần tử, có khả năng cuốn chiếu
            LineItem curvePara = myPanePara.AddCurve("a1", listPara, Color.Red, SymbolType.None);         //Tạo mới đường cong của đồ thị trên GraphPane dựa vào danh sách dữ liệu
            RollingPointPairList list2Para = new RollingPointPairList(60000);
            LineItem curve2Para = myPanePara.AddCurve("a2", list2Para, Color.MediumSlateBlue, SymbolType.None);

            myPanePara.XAxis.Scale.Min = 0;                         //Đặt giới hạn đồ thị
            myPanePara.XAxis.Scale.Max = 6;
            myPanePara.XAxis.Scale.MinorStep = 0.1;                   //Đặt các bước độ chia
            myPanePara.XAxis.Scale.MajorStep = 1;
            myPanePara.YAxis.Scale.Min = 0;                      //Tương tự cho trục y
            myPanePara.YAxis.Scale.Max = 10;

            myPanePara.AxisChange();

        }



        /*Button methods -------------------------------------------------------------------------------------------------*/
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                sPort.PortName = cBoxPortName.Text;
                sPort.BaudRate = Int32.Parse(cBoxBaudRate.Text);
                sPort.DataBits = Int32.Parse(cBoxDataBit.Text);
                sPort.Parity = (Parity)Enum.Parse(typeof(Parity), cBoxParity.Text); //có kiểu enum parity chứa các giá trị có sẵn của parity
                sPort.StopBits = (StopBits)Enum.Parse(typeof(StopBits), cBoxStopBit.Text);
                //sPort.ReadBufferSize = 32

                sPort.Open();
                progressBar1.Value = 100;

                btnConnect.Enabled = false;
                btnDisCon.Enabled = true;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw;
            }

        }

        private void btnDisCon_Click(object sender, EventArgs e)
        {
            try
            {
                sPort.Close();
                progressBar1.Value = 0;

                btnDisCon.Enabled = false;
                btnConnect.Enabled = true;

                Realtime = 0;                                    //Khai báo biến thời gian để vẽ đồ thị
                Setpoint = 0;                                       //Khai báo biến dữ liệu thứ nhất để vẽ đồ thị
                Measure = 0;                                      //Khai báo biến dữ liệu thứ 2 để vẽ đồ thị
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw;
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (sPort.IsOpen)
            {
                try
                {
                    sPort.WriteTimeout = 5000;
                    OutBuffer = tBoxDisplaySend.Text;
                    sPort.Write(OutBuffer);
                }
                catch (Exception err)
                {
                    MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    //throw;
                }
            }
            else
            {
                MessageBox.Show("Connect Serial port first!", "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnClearGet_Click(object sender, EventArgs e)
        {
            tBoxDisplayGet.Text = null;
        }

        private void btnGraph_Click(object sender, EventArgs e)
        {
            switch (status)
            {
                case GraphStatus.GraphRun:
                    status = GraphStatus.GraphStop;
                    btnGraph.Text = "Graph";
                    break;
                case GraphStatus.GraphStop:
                    status = GraphStatus.GraphRun;
                    btnGraph.Text = "NotGraph";
                    break;
                default:
                    break;
            }
        }

        private void btnCompact_Click(object sender, EventArgs e)
        {
            switch (enScroll)
            {
                case GraphScroll.Scroll:
                    enScroll = GraphScroll.Compact;
                    btnCompact.Text = "Scroll";
                    break;
                case GraphScroll.Compact:
                    enScroll = GraphScroll.Scroll;
                    btnCompact.Text = "Compact";
                    break;
                default:
                    break;
            }
        }

        private void btnMotorRun_Click(object sender, EventArgs e)
        {
            try
            {
                clearGraph();

                byte[] setPointBuffer;
                if (UartCom.TxHandshake(UartCom.ControlHeader.Run, "0", out setPointBuffer))
                {
                    sPort.Write(setPointBuffer, 0, 7);
                }
                ReTrans.Enabled = true;
                
                //btnMotorRun.Enabled = false;
                //btnMotorStop.Enabled = true;

                //gBoxWhatToMeasure.Enabled = false;
                //gBoxPID.Enabled = false;
                //tBoxSetPoint.Enabled = false;

            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw;
            }
        }

        private void btnMotorStop_Click(object sender, EventArgs e)
        {
            try
            {
                byte[] setPointBuffer;
                if (UartCom.TxHandshake(UartCom.ControlHeader.Stop, "0", out setPointBuffer))
                {
                    sPort.Write(setPointBuffer, 0, 7);
                }
                ReTrans.Enabled = true;

                //btnMotorRun.Enabled = true;
                //btnMotorStop.Enabled = false;

                //gBoxWhatToMeasure.Enabled = true;
                //gBoxPID.Enabled = true;
                //tBoxSetPoint.Enabled = true;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw;
            }
        }

        private void btnRequest_Click(object sender, EventArgs e)
        {
            try
            {
                //byte[] temp = new byte[1];
                //temp[0] = (byte)UartCom.controlHeader.Stop;
                //sPort.Write(temp, 0, 1);
                //byte[] setPointBuffer = UartCom.TxHandshake(UartCom.controlHeader.Request, "0");
                //sPort.Write(setPointBuffer, 0, 5);
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw;
            }
        }

        private void btnAbout_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Nhóm tác giả: \n" +
                "\t Lý Ngọc Trân Châu\n" +
                "\t Nguyễn Hùng Sơn \n" +
                "\t Lê Xuân Thuyên \n" +
                "\n" +
                "Thông số calib bộ điều khiển PID vị trí: \n" +
                "\t Kp = 0.01 \n" +
                "\t Ki = 0.001 \n" +
                "\t Kd = 0.0004 \n" +
                "\t Vận tốc tối đa: 2300 xung / 20ms \n", "About...", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                Export2Excel export = new Export2Excel();
                export.table = logTable;
                export.SaveToExcel();
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }





        /*Serial Port methods -----------------------------------------------------------------------------------------------------------------*/
        private void sPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                //readBuffer(ref inByte);//inBuffer);
                //sPort.Read(inByte, 0, 5);
                InByte[0] = Convert.ToByte(sPort.ReadByte());
                if ((UartCom.FrameHeader)InByte[0] == UartCom.FrameHeader.STX7)
                {
                    for (int ii = 1; ii < 7; ii++)
                    {
                        InByte[ii] = Convert.ToByte(sPort.ReadByte());
                    }
                    this.Invoke(new EventHandler(saveData));
                }
                else if ((UartCom.FrameHeader)InByte[0] == UartCom.FrameHeader.STX16)
                {
                    InByte_16[0] = InByte[0];
                    for (int ii = 1; ii < 16; ii++)
                    {
                        InByte_16[ii] = Convert.ToByte(sPort.ReadByte());
                    }
                    this.Invoke(new EventHandler(saveAndSendAck));
                }
                
                
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw;
            }
        }

        private void saveAndSendAck(object sender, EventArgs e)
        {
            byte[] instruction, dataWithoutHeader_1, dataWithoutHeader_2, dataWithoutHeader_3;

            if (!UartCom.RxHandshake_16byte(InByte_16, out instruction))
                return;
            UartCom.DataHeader header = UartCom.classifyHeader_16byte(instruction, out dataWithoutHeader_1, out dataWithoutHeader_2, out dataWithoutHeader_3);
            float displayValue1 = UartCom.uARTBytetoFloat(dataWithoutHeader_1); //realtime
            float displayValue2 = UartCom.uARTBytetoFloat(dataWithoutHeader_2); //measure
            float displayValue3 = UartCom.uARTBytetoFloat(dataWithoutHeader_3); //PWM

            Realtime = displayValue1;
            Measure = displayValue2;
            PWM = displayValue3;

            if (status == GraphStatus.GraphRun)
            {
                graphUpdate();
            }

            if (cBoxLog.Checked == true)
            {
                logTable.Rows.Add(Setpoint, Measure);
            }
        }

        //private void readBuffer(ref byte[] outBuffer)
        //{
        //    for (int ii = 0; ii <= 4; ii++)
        //    {
        //        outBuffer[ii] = Convert.ToByte(sPort.ReadByte());
        //    }
        //}

        private void saveData(object sender, EventArgs e)
        {
            byte[] instruction, dataWithoutHeader;

            if (!UartCom.RxHandshake(InByte, out instruction))
                return;
            UartCom.DataHeader header = UartCom.classifyHeader(instruction, out dataWithoutHeader);
            float displayValue = UartCom.uARTBytetoFloat(dataWithoutHeader);

            switch (header)
            {
                case UartCom.DataHeader.Realtime:
                    //realtimestep = displayValue;
                    break;
                case UartCom.DataHeader.Setpoint:
                    Setpoint = displayValue;
                    tBoxDisplayGet.Text += UartCom.motorMessage[4];
                    tBoxDisplayGet.Text += displayValue.ToString();
                    tBoxDisplayGet.Text += Environment.NewLine;
                    break;
                case UartCom.DataHeader.Measure:
                    Measure = displayValue;
                    Realtime += Realtimestep;
                    break;
                case UartCom.DataHeader.Run:
                    tBoxDisplayGet.Text += UartCom.motorMessage[0];
                    tBoxDisplayGet.Text += Environment.NewLine;
                    break;
                case UartCom.DataHeader.Stop:
                    tBoxDisplayGet.Text += UartCom.motorMessage[1];
                    tBoxDisplayGet.Text += Environment.NewLine;
                    break;
                case UartCom.DataHeader.Velocity:
                    tBoxDisplayGet.Text += UartCom.motorMessage[2];
                    tBoxDisplayGet.Text += Environment.NewLine;
                    break;
                case UartCom.DataHeader.Position:
                    tBoxDisplayGet.Text += UartCom.motorMessage[3];
                    tBoxDisplayGet.Text += Environment.NewLine;
                    break;
                case UartCom.DataHeader.Kp:
                    tBoxDisplayGet.Text += UartCom.motorMessage[5];
                    tBoxDisplayGet.Text += displayValue.ToString();
                    tBoxDisplayGet.Text += Environment.NewLine;
                    break;
                case UartCom.DataHeader.Ki:
                    tBoxDisplayGet.Text += UartCom.motorMessage[6];
                    tBoxDisplayGet.Text += displayValue.ToString();
                    tBoxDisplayGet.Text += Environment.NewLine;
                    break;
                case UartCom.DataHeader.Kd:
                    tBoxDisplayGet.Text += UartCom.motorMessage[7];
                    tBoxDisplayGet.Text += displayValue.ToString();
                    tBoxDisplayGet.Text += Environment.NewLine;
                    break;
                case UartCom.DataHeader.Data:
                    char transChar = (char)displayValue;
                    tBoxDisplayGet.Text += Convert.ToString(transChar);
                    break;
                case UartCom.DataHeader.Floattype:
                    tBoxDisplayGet.Text += displayValue.ToString();
                    tBoxDisplayGet.Text += Environment.NewLine;
                    break;
                case UartCom.DataHeader.Calib:
                    tBoxDisplayGet.Text += UartCom.motorMessage[8];
                    tBoxDisplayGet.Text += displayValue.ToString();
                    tBoxDisplayGet.Text += Environment.NewLine;
                    break;
                default:
                    break;
            }

            if (status == GraphStatus.GraphRun)
            {
                graphUpdate();
            }

            if (cBoxLog.Checked == true)
            {
                logTable.Rows.Add(Setpoint, Measure);
            }
        }

        // Vẽ đồ thị
        private void graphUpdate()
        {
            LineItem curve = zGrphPlotData.GraphPane.CurveList[0] as LineItem;   //Khai báo đường cong từ danh sách đường cong đồ thị (kế thừa từ heap của dữ liệu ở Form_load)
            if (curve == null)
                return;
            LineItem curve2 = zGrphPlotData.GraphPane.CurveList[1] as LineItem;
            if (curve2 == null)
                return;
            IPointListEdit list = curve.Points as IPointListEdit;   //Khai báo danh sách dữ liệu cho đường cong đồ thị
            if (list == null)
                return;
            IPointListEdit list2 = curve2.Points as IPointListEdit;
            if (list2 == null)
                return;
            list.Add(Realtime, Setpoint);                          // Thêm điểm trên đồ thị
            list2.Add(Realtime, Measure);                        // Thêm điểm trên đồ thị

            Scale xScale = zGrphPlotData.GraphPane.XAxis.Scale;  //Giới hạn của đồ thị
            Scale yScale = zGrphPlotData.GraphPane.YAxis.Scale;

            if (enScroll == GraphScroll.Scroll)
            {
                // Tự động Scale theo trục x
                if (Realtime > xScale.Max - xScale.MajorStep)       //Nếu realtime lớn hơn Max x trừ đi 1 MajorStep (2 vạch lớn)
                {
                    xScale.Min = xScale.Min + Realtime - (xScale.Max - xScale.MajorStep);
                    xScale.Max = Realtime + xScale.MajorStep;       //Tự dời đồ thị qua 1 MajorStep 
                    //xScale.Min = xScale.Max - 6;
                }
                // Tự động Scale theo trục y
                if (Setpoint > yScale.Max - yScale.MajorStep)          //Nếu datas vượt quá giới hạn trừ 1 MajorStep
                {
                    yScale.Max = Setpoint + yScale.MajorStep;          //Thì tăng giới hạn thêm 1 MajorStep
                }
                else if (Setpoint < yScale.Min + yScale.MajorStep)
                {
                    yScale.Min = Setpoint - yScale.MajorStep;
                }
            }


            LineItem curvePara = zGraphParameters.GraphPane.CurveList[0] as LineItem;   //Khai báo đường cong từ danh sách đường cong đồ thị (kế thừa từ heap của dữ liệu ở Form_load)
            if (curvePara == null)
                return;
            LineItem curve2Para = zGraphParameters.GraphPane.CurveList[1] as LineItem;
            if (curve2Para == null)
                return;
            IPointListEdit listPara = curvePara.Points as IPointListEdit;   //Khai báo danh sách dữ liệu cho đường cong đồ thị
            if (listPara == null)
                return;
            IPointListEdit list2Para = curve2Para.Points as IPointListEdit;
            if (list2Para == null)
                return;
            listPara.Add(Realtime, PWM);                          // Thêm điểm trên đồ thị
            //list2Para.Add(realtime, Thetaa2);                        // Thêm điểm trên đồ thị

            Scale xScalePara = zGraphParameters.GraphPane.XAxis.Scale;  //Giới hạn của đồ thị
            Scale yScalePara = zGraphParameters.GraphPane.YAxis.Scale;

            if (enScroll == GraphScroll.Scroll)
            {
                // Tự động Scale theo trục x
                if (Realtime > xScalePara.Max - xScalePara.MajorStep)       //Nếu realtime lớn hơn Max x trừ đi 1 MajorStep (2 vạch lớn)
                {
                    xScalePara.Min = xScalePara.Min + Realtime - (xScalePara.Max - xScalePara.MajorStep);
                    xScalePara.Max = Realtime + xScalePara.MajorStep;       //Tự dời đồ thị qua 1 MajorStep 
                    //xScale.Min = xScale.Max - 6;
                }
                // Tự động Scale theo trục y
                if (Setpoint > yScalePara.Max - yScalePara.MajorStep)          //Nếu datas vượt quá giới hạn trừ 1 MajorStep
                {
                    yScalePara.Max = Setpoint + yScalePara.MajorStep;          //Thì tăng giới hạn thêm 1 MajorStep
                }
                else if (Setpoint < yScalePara.Min + yScalePara.MajorStep)
                {
                    yScalePara.Min = Setpoint - yScalePara.MajorStep;
                }
            }

        }





        /*Text Box methods ------------------------------------------------------------------------------------------------------------------------*/
        private void tBoxDisplayGet_TextChanged(object sender, EventArgs e)
        {
            tBoxDisplayGet.SelectionStart = tBoxDisplayGet.Text.Length;
            tBoxDisplayGet.ScrollToCaret();
        }

        private void tBoxSetPoint_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == Keys.Enter)
                {
                    //Setpoint = float.Parse(tBoxSetPoint.Text);
                    byte[] setPointBuffer;
                    if (UartCom.TxHandshake(UartCom.ControlHeader.Setpoint, tBoxSetPoint.Text, out setPointBuffer))
                    {
                        sPort.Write(setPointBuffer, 0, 7);
                    }
                    ReTrans.Enabled = true;
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw;
            }
        }

        private void tBoxKp_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == Keys.Enter)
                {
                    byte[] setPointBuffer;
                    if (UartCom.TxHandshake(UartCom.ControlHeader.Kp, tBoxKp.Text, out setPointBuffer))
                    {
                        sPort.Write(setPointBuffer, 0, 7);
                    }
                    ReTrans.Enabled = true;
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw;
            }

        }

        private void tBoxKi_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == Keys.Enter)
                {
                    byte[] setPointBuffer;
                    if (UartCom.TxHandshake(UartCom.ControlHeader.Ki, tBoxKi.Text, out setPointBuffer))
                    {
                        sPort.Write(setPointBuffer, 0, 7);
                    }
                    ReTrans.Enabled = true;
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw;
            }
        }

        private void tBoxKd_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == Keys.Enter)
                {
                    byte[] setPointBuffer;
                    if (UartCom.TxHandshake(UartCom.ControlHeader.Kd, tBoxKd.Text, out setPointBuffer))
                    {
                        sPort.Write(setPointBuffer, 0, 7);
                    }
                    ReTrans.Enabled = true;
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw;
            }
        }

        private void tBoxTimeStep_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Realtimestep = Double.Parse(tBoxTimeStep.Text);
            }
        }

        private void tBoxCalib_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == Keys.Enter)
                {
                    byte[] setPointBuffer;
                    if (UartCom.TxHandshake(UartCom.ControlHeader.Calib, tbCalib.Text, out setPointBuffer))
                    {
                        sPort.Write(setPointBuffer, 0, 7);
                    }
                    ReTrans.Enabled = true;
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw;
            }
        }





        /*Radio Button methods*/
        private void rBtnVP_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                clearGraph();
                if (rBtnVelocity.Checked)
                {
                    lbVelocity.Text = "Velocity";
                    tbCalib.Text = "4.8";

                    GraphPane myPane = zGrphPlotData.GraphPane;
                    myPane.YAxis.Scale.Min = 0;                      //Tương tự cho trục y
                    myPane.YAxis.Scale.Max = 2500;
                    myPane.AxisChange();

                    //byte[] temp = new byte[1];
                    //temp[0] = (byte)UartCom.controlHeader.Velocity;
                    //sPort.Write(temp, 0, 1);
                    byte[] setPointBuffer;
                    if (UartCom.TxHandshake(UartCom.ControlHeader.Velocity, "0", out setPointBuffer))
                    {
                        sPort.Write(setPointBuffer, 0, 7);
                    }
                    ReTrans.Enabled = true;
                }
                else
                {
                    lbVelocity.Text = "Position";
                    tbCalib.Text = "40";

                    GraphPane myPane = zGrphPlotData.GraphPane;
                    myPane.YAxis.Scale.Min = 0;                      //Tương tự cho trục y
                    myPane.YAxis.Scale.Max = 600;
                    myPane.AxisChange();

                    //byte[] temp = new byte[1];
                    //temp[0] = (byte)UartCom.controlHeader.Position;
                    //sPort.Write(temp, 0, 1);      
                    byte[] setPointBuffer;
                    if (UartCom.TxHandshake(UartCom.ControlHeader.Position, "0", out setPointBuffer))
                    {
                        sPort.Write(setPointBuffer, 0, 7);
                    }
                    ReTrans.Enabled = true;
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw;
            }
        }



        /*Other methods ---------------------------------------------------------------------------------------------------------------------------*/
        private void clearGraph()
        {
            zGrphPlotData.GraphPane.CurveList.Clear();                  // Xóa đường
            zGrphPlotData.GraphPane.GraphObjList.Clear();               // Xóa đối tượng
            zGrphPlotData.AxisChange();
            zGrphPlotData.Invalidate();

            Realtime = 0;
            //setpoint = 0;
            Measure = 0;

            // Khởi tạo ZedGraph            
            GraphPane myPane = zGrphPlotData.GraphPane;      //Tác động các thành phần của Control, (GraphPane)
            myPane.Title.Text = "Giá trị đặt - giá trị đo";
            myPane.XAxis.Title.Text = "Thời gian (s)";
            myPane.YAxis.Title.Text = "Dữ liệu";

            RollingPointPairList list = new RollingPointPairList(60000);        //Tạo mới danh sách dữ liệu 60000 phần tử, có khả năng cuốn chiếu
            LineItem curve = myPane.AddCurve("Giá trị đặt", list, Color.Red, SymbolType.None);         //Tạo mới đường cong của đồ thị trên GraphPane dựa vào danh sách dữ liệu
            RollingPointPairList list2 = new RollingPointPairList(60000);
            LineItem curve2 = myPane.AddCurve("Giá trị đo", list2, Color.MediumSlateBlue, SymbolType.None);

            myPane.XAxis.Scale.Min = 0;                         //Đặt giới hạn đồ thị
            myPane.XAxis.Scale.Max = 6;
            myPane.XAxis.Scale.MinorStep = 0.1;                   //Đặt các bước độ chia
            myPane.XAxis.Scale.MajorStep = 1;
            //myPane.YAxis.Scale.Min = 0;                      //Tương tự cho trục y
            //myPane.YAxis.Scale.Max = 100;

            myPane.AxisChange();

            zGraphParameters.GraphPane.CurveList.Clear();                  // Xóa đường
            zGraphParameters.GraphPane.GraphObjList.Clear();               // Xóa đối tượng
            zGraphParameters.AxisChange();
            zGraphParameters.Invalidate();

            // Khởi tạo ZedGraph            
            GraphPane myPanePara = zGraphParameters.GraphPane;      //Tác động các thành phần của Control, (GraphPane)
            myPanePara.Title.Text = "Tham số";
            myPanePara.XAxis.Title.Text = "Thời gian (s)";
            myPanePara.YAxis.Title.Text = "Dữ liệu";

            RollingPointPairList listPara = new RollingPointPairList(60000);        //Tạo mới danh sách dữ liệu 60000 phần tử, có khả năng cuốn chiếu
            LineItem curvePara = myPanePara.AddCurve("Giá trị đặt", listPara, Color.Red, SymbolType.None);         //Tạo mới đường cong của đồ thị trên GraphPane dựa vào danh sách dữ liệu
            RollingPointPairList list2Para = new RollingPointPairList(60000);
            LineItem curve2Para = myPanePara.AddCurve("Giá trị đo", list2Para, Color.MediumSlateBlue, SymbolType.None);

            myPanePara.XAxis.Scale.Min = 0;                         //Đặt giới hạn đồ thị
            myPanePara.XAxis.Scale.Max = 6;
            myPanePara.XAxis.Scale.MinorStep = 0.1;                   //Đặt các bước độ chia
            myPanePara.XAxis.Scale.MajorStep = 1;
            myPanePara.YAxis.Scale.Min = 0;                      //Tương tự cho trục y
            myPanePara.YAxis.Scale.Max = 10;

            myPanePara.AxisChange();

        }

        private void GuiRefresh_Tick(object sender, EventArgs e)
        {
            try
            {
                this.Invoke(new EventHandler(reFresh));
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw;
            }
        }

        private void reFresh(object sender, EventArgs e)
        {
            tBoxTime.Text = Realtime.ToString();
            tBoxMeasure.Text = Measure.ToString();

            if (status == GraphStatus.GraphRun)
            {
                zGrphPlotData.AxisChange();                      //Thay đổi trục theo giá trị Scale
                zGrphPlotData.Invalidate();                      //Mở khoá để và vẽ lại

                zGraphParameters.AxisChange();                      //Thay đổi trục theo giá trị Scale
                zGraphParameters.Invalidate();                      //Mở khoá để và vẽ lại
            }

        }


        private void ReTrans_Tick(object sender, EventArgs e)
        {
            byte[] buffer;
            if (UartCom.ReHandshake(out buffer))
            {
                sPort.Write(buffer, 0, 7);
                tBoxDisplayGet.Text += ("NAK" + Environment.NewLine);
            }
            else
            {
                ReTrans.Enabled = false;
            }
        }
    }
}


